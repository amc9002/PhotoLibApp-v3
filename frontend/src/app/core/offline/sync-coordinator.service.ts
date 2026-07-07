import { Injectable } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { BehaviorSubject, Subject, filter, firstValueFrom, pairwise } from 'rxjs';
import { ApiService } from '../api/api.service';
import { ConnectivityService } from './connectivity.service';
import { LocalDbService } from './local-db.service';
import { OutboxEntry } from './outbox.model';
import { SyncReviewItem, SyncReviewSummary } from './sync-review.model';
import { Gallery } from '../../models/gallery.model';
import { PhotoDto } from '../../models/photo.dto';

const LOCK_NAME = 'photolib-sync';
const BROADCAST_CHANNEL_NAME = 'photolib-sync';

interface LiveEntityState {
  updatedAtUtc: string;
  isDeleted: boolean;
  title: string;
}

/**
 * Runs on the offline -> online transition: reconciles the local outbox
 * against the server's current state and drives the review/replay flow.
 * Only one browser tab actually replays (Web Locks leader election) -
 * other tabs just wait for a "sync-completed" broadcast and refresh.
 */
@Injectable({ providedIn: 'root' })
export class SyncCoordinatorService {
  /** Non-null while there's a review pending user confirmation, in this tab. */
  readonly reviewSummary$ = new BehaviorSubject<SyncReviewSummary | null>(null);

  /** Fires when a sync pass (in this tab or another) has finished, so views can refresh. */
  readonly syncCompleted$ = new Subject<void>();

  private readonly channel: BroadcastChannel | null =
    typeof BroadcastChannel !== 'undefined' ? new BroadcastChannel(BROADCAST_CHANNEL_NAME) : null;

  private resolveReviewDone: (() => void) | null = null;

  constructor(
    private connectivity: ConnectivityService,
    private localDb: LocalDbService,
    private api: ApiService,
  ) {
    this.connectivity.isOnline$
      .pipe(
        pairwise(),
        filter(([wasOnline, isOnline]) => !wasOnline && isOnline),
      )
      .subscribe(() => void this.onReconnect());

    if (this.channel) {
      this.channel.onmessage = (event) => {
        if (event.data === 'sync-completed') {
          this.syncCompleted$.next();
        }
      };
    }
  }

  /** Called by the review modal once every item has been confirmed or discarded. */
  async finishReview(): Promise<void> {
    this.reviewSummary$.next(null);
    this.channel?.postMessage('sync-completed');
    this.syncCompleted$.next();
    this.resolveReviewDone?.();
    this.resolveReviewDone = null;
  }

  async confirmItem(item: SyncReviewItem): Promise<void> {
    const entry = await this.findOutboxEntry(item.outboxOpId);
    if (!entry) return;

    await this.replayEntry(entry);
    await this.localDb.removeOutboxEntry(item.outboxOpId);
  }

  async discardItem(item: SyncReviewItem): Promise<void> {
    const entry = await this.findOutboxEntry(item.outboxOpId);
    if (!entry) return;

    if (entry.type === 'create-gallery') {
      await this.localDb.removeMirroredGallery(entry.entityId);
    } else if (entry.type === 'create-photo') {
      await this.localDb.removeMirroredPhoto(entry.entityId);
    } else if (entry.type === 'delete') {
      if (entry.entityType === 'gallery') await this.localDb.untombstoneMirroredGallery(entry.entityId);
      else await this.localDb.untombstoneMirroredPhoto(entry.entityId);
    } else {
      // update/setTags/move/copy: re-sync the mirror to the live server
      // value instead of leaving the discarded local edit on screen.
      await this.refreshMirrorFromServer(entry.entityType, entry.entityId);
    }

    await this.localDb.removeOutboxEntry(item.outboxOpId);
  }

  private async refreshMirrorFromServer(entityType: 'gallery' | 'photo', id: string): Promise<void> {
    try {
      if (entityType === 'gallery') {
        const g = await firstValueFrom(this.api.get<Gallery>(`Gallery/${id}`));
        if (g.isDeleted) await this.localDb.removeMirroredGallery(id);
        else await this.localDb.upsertMirroredGallery(g);
      } else {
        const p = await firstValueFrom(this.api.get<PhotoDto>(`Photo/${id}`));
        if (p.isDeleted) {
          await this.localDb.removeMirroredPhoto(id);
        } else {
          await this.localDb.upsertMirroredPhoto(
            {
              id: p.id,
              title: p.title,
              description: p.description,
              hasThumbnail: p.hasThumbnail,
              updatedAtUtc: p.updatedAtUtc ?? p.createdAtUtc,
              tags: p.tags,
            },
            p.galleryId!,
          );
        }
      }
    } catch (err) {
      if (!(err instanceof HttpErrorResponse) || err.status !== 404) throw err;
    }
  }

  // ---------------- leader election + orchestration ----------------

  private async onReconnect(): Promise<void> {
    const outboxCount = await this.localDb.countOutboxEntries();
    if (outboxCount === 0) return;

    if (!('locks' in navigator)) {
      await this.runAsLeader();
      return;
    }

    await navigator.locks.request(LOCK_NAME, { ifAvailable: true }, async (lock) => {
      if (!lock) return; // another tab is already the leader
      await this.runAsLeader();
    });
  }

  private async runAsLeader(): Promise<void> {
    // Reorders are low-stakes (pure ordering, no data loss risk) and are
    // applied without asking. Any that reference a not-yet-synced create
    // get fixed up automatically once that create replays (remapping
    // rewrites any still-pending reorder payloads), so this can run before
    // the rest of the queue too - the backend already skips unresolved ids.
    await this.replayReorders();

    const entries = await this.localDb.getOutboxEntries();
    if (entries.length === 0) return;

    const items = await this.buildReviewItems(entries);
    if (items.length === 0) {
      await this.finishReview();
      return;
    }

    await new Promise<void>((resolve) => {
      this.resolveReviewDone = resolve;
      this.reviewSummary$.next({ items });
    });
  }

  private async replayReorders(): Promise<void> {
    const entries = await this.localDb.getOutboxEntries();

    for (const entry of entries) {
      if (entry.type !== 'reorder' || entry.opId === undefined) continue;

      try {
        if (entry.entityType === 'gallery') {
          await firstValueFrom(this.api.put<void>('Gallery/reorder', { galleryIds: entry.payload.ids }));
        } else {
          await firstValueFrom(
            this.api.put<void>('Photo/reorder', { galleryId: entry.entityId, photoIds: entry.payload.ids }),
          );
        }
        await this.localDb.removeOutboxEntry(entry.opId);
      } catch (err) {
        if (!isConnectivityError(err)) throw err;
        return; // lost connectivity again mid-replay - leave the rest queued
      }
    }
  }

  // ---------------- conflict check ----------------

  private async buildReviewItems(entries: OutboxEntry[]): Promise<SyncReviewItem[]> {
    const relevant = entries.filter((e) => e.type !== 'reorder');
    const liveState = new Map<string, LiveEntityState | null>();

    for (const entry of relevant) {
      if (entry.type === 'create-gallery' || entry.type === 'create-photo') continue;
      if (liveState.has(entry.entityId)) continue;

      liveState.set(entry.entityId, await this.fetchLiveState(entry.entityType, entry.entityId));
    }

    const items: SyncReviewItem[] = [];
    for (const entry of relevant) {
      items.push(await this.toReviewItem(entry, liveState.get(entry.entityId) ?? null));
    }
    return items;
  }

  private async fetchLiveState(
    entityType: 'gallery' | 'photo',
    id: string,
  ): Promise<LiveEntityState | null> {
    try {
      const path = entityType === 'gallery' ? `Gallery/${id}` : `Photo/${id}`;
      return await firstValueFrom(this.api.get<LiveEntityState>(path));
    } catch (err) {
      if (err instanceof HttpErrorResponse && err.status === 404) return null;
      throw err;
    }
  }

  private async toReviewItem(entry: OutboxEntry, live: LiveEntityState | null): Promise<SyncReviewItem> {
    const base = { outboxOpId: entry.opId!, entityType: entry.entityType, entityId: entry.entityId };

    if (entry.type === 'create-gallery') {
      return { ...base, kind: 'added', conflict: false, title: entry.payload.title };
    }
    if (entry.type === 'create-photo') {
      return {
        ...base,
        kind: 'added',
        conflict: false,
        title: entry.payload.title,
        thumbnailPhotoId: entry.entityId,
      };
    }

    const kind = entry.type === 'delete' ? 'deleted' : 'changed';
    let conflict = false;
    let conflictReason: string | undefined;

    if (live === null) {
      if (entry.type !== 'delete') {
        conflict = true;
        conflictReason = "Гэты аб'ект ужо выдалены на серверы.";
      }
    } else if (live.isDeleted && entry.type !== 'delete') {
      conflict = true;
      conflictReason = "Гэты аб'ект ужо выдалены на серверы.";
    } else if (
      entry.baseUpdatedAtUtc &&
      live.updatedAtUtc &&
      live.updatedAtUtc !== entry.baseUpdatedAtUtc
    ) {
      conflict = true;
      conflictReason = 'Зменена на серверы пасля адключэння.';
    }

    const mirrorTitle = await this.mirrorTitle(entry.entityType, entry.entityId);

    return {
      ...base,
      kind,
      conflict,
      conflictReason,
      title: live?.title ?? mirrorTitle ?? '(без назвы)',
      thumbnailPhotoId: entry.entityType === 'photo' ? entry.entityId : undefined,
    };
  }

  private async mirrorTitle(entityType: 'gallery' | 'photo', id: string): Promise<string | undefined> {
    if (entityType === 'gallery') return (await this.localDb.getMirroredGallery(id))?.title;
    return (await this.localDb.getMirroredPhoto(id))?.title;
  }

  // ---------------- replay ----------------

  private async replayEntry(entry: OutboxEntry): Promise<void> {
    switch (entry.type) {
      case 'create-gallery': {
        const created = await firstValueFrom(
          this.api.post<Gallery>('Gallery', {
            title: entry.payload.title,
            clientTempId: entry.payload.clientTempId,
          }),
        );
        await this.remapId('gallery', entry.payload.clientTempId, created.id);
        break;
      }
      case 'create-photo': {
        const created = await firstValueFrom(
          this.api.post<PhotoDto>('Photo', {
            galleryId: entry.payload.galleryId,
            title: entry.payload.title,
            clientTempId: entry.payload.clientTempId,
          }),
        );
        const formData = new FormData();
        formData.append('file', entry.payload.file);
        await firstValueFrom(this.api.post<void>(`Photo/${created.id}/upload`, formData));
        await this.remapId('photo', entry.payload.clientTempId, created.id);
        break;
      }
      case 'update':
        if (entry.entityType === 'gallery') {
          await firstValueFrom(this.api.put<void>(`Gallery/${entry.entityId}`, entry.payload));
        } else {
          await firstValueFrom(this.api.put<PhotoDto>(`photo/${entry.entityId}`, entry.payload));
        }
        break;
      case 'delete':
        if (entry.entityType === 'gallery') {
          await firstValueFrom(this.api.delete<void>(`Gallery/${entry.entityId}`));
        } else {
          await firstValueFrom(this.api.delete<void>(`Photo/${entry.entityId}`));
        }
        break;
      case 'move':
        await firstValueFrom(
          this.api.post<void>(`Photo/${entry.entityId}/move`, { galleryId: entry.payload.galleryId }),
        );
        break;
      case 'copy': {
        const copy = await firstValueFrom(
          this.api.post<PhotoDto>(`Photo/${entry.entityId}/copy`, { galleryId: entry.payload.galleryId }),
        );
        await this.remapId('photo', entry.payload.newClientTempId, copy.id);
        break;
      }
      case 'setTags':
        if (entry.entityType === 'gallery') {
          await firstValueFrom(
            this.api.put<string[]>(`Gallery/${entry.entityId}/tags`, { tagNames: entry.payload.tagNames }),
          );
        } else {
          await firstValueFrom(
            this.api.put<string[]>(`Photo/${entry.entityId}/tags`, { tagNames: entry.payload.tagNames }),
          );
        }
        break;
      case 'reorder':
        break; // handled by replayReorders()
    }
  }

  private async remapId(entityType: 'gallery' | 'photo', oldId: string, newId: string): Promise<void> {
    await this.localDb.remapOutboxReferences(oldId, newId);

    if (entityType === 'gallery') {
      await this.localDb.remapMirroredGalleryId(oldId, newId);
    } else {
      await this.localDb.remapMirroredPhotoId(oldId, newId);
      await this.localDb.remapImageCacheId(oldId, newId);
    }
  }

  private async findOutboxEntry(opId: number): Promise<OutboxEntry | undefined> {
    const entries = await this.localDb.getOutboxEntries();
    return entries.find((e) => e.opId === opId);
  }
}

function isConnectivityError(err: unknown): boolean {
  return err instanceof HttpErrorResponse && err.status === 0;
}
