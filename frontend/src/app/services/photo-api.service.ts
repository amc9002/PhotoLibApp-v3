import { Injectable } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { Observable, defer, firstValueFrom, from } from 'rxjs';
import { ApiService } from '../core/api/api.service';
import { ConnectivityService } from '../core/offline/connectivity.service';
import { LocalDbService } from '../core/offline/local-db.service';
import { PhotoDto } from '../models/photo.dto';
import { PhotoListItemDto } from '../models/photoLisrItem.dto';
import {
  DescriptionLength,
  DescriptionStyle,
  GenerateDescriptionResponse,
} from '../models/generate-description.dto';

@Injectable({
  providedIn: 'root',
})
export class PhotoApiService {
  constructor(
    private api: ApiService,
    private connectivity: ConnectivityService,
    private localDb: LocalDbService,
  ) {}

  getAll() {
    return this.api.get<PhotoDto[]>('photos');
  }

  getById(id: string) {
    return this.api.get<PhotoDto>(`Photo/${id}`);
  }

  /**
   * Online (and nothing pending offline): fetches fresh and refreshes the
   * local mirror for this gallery. Offline, unreachable, or there's an
   * unconfirmed offline change sitting in the outbox: serves the mirror.
   */
  getByGallery(galleryId: string): Observable<PhotoListItemDto[]> {
    return defer(() => from(this.resolveByGallery(galleryId)));
  }

  /**
   * Creates photo metadata only - see `upload()` for what happens offline.
   * The two are always called back-to-back by callers (create, then
   * upload the file for the id it returns), so `upload()` is what actually
   * decides and commits to the online/offline path for the pair.
   */
  create(dto: Partial<PhotoDto>): Observable<PhotoDto> {
    return defer(() => from(this.resolveCreate(dto)));
  }

  upload(photoId: string, file: File): Observable<void> {
    return defer(() => from(this.resolveUpload(photoId, file)));
  }

  update(
    id: string,
    dto: { title: string; description?: string },
  ): Observable<{ updatedAtUtc: string } | void> {
    return defer(() => from(this.resolveUpdate(id, dto)));
  }

  delete(id: string): Observable<void> {
    return defer(() => from(this.resolveDelete(id)));
  }

  move(id: string, galleryId: string): Observable<void> {
    return defer(() => from(this.resolveMove(id, galleryId)));
  }

  copy(id: string, galleryId: string): Observable<PhotoDto | void> {
    return defer(() => from(this.resolveCopy(id, galleryId)));
  }

  setTags(id: string, tagNames: string[]): Observable<string[]> {
    return defer(() => from(this.resolveSetTags(id, tagNames)));
  }

  reorder(galleryId: string, photoIds: string[]): Observable<void> {
    return defer(() => from(this.resolveReorder(galleryId, photoIds)));
  }

  /**
   * Asks the AI to draft a title, description and tags for a photo. Online-only
   * and on-demand - nothing is persisted by this call (so unlike the other
   * methods here, there's no offline mirror/outbox path to fall back to).
   */
  generateDescription(
    photoId: string,
    style: DescriptionStyle,
    length: DescriptionLength,
    additionalInstructions?: string,
  ): Observable<GenerateDescriptionResponse> {
    return this.api.post<GenerateDescriptionResponse>(`Photo/${photoId}/generate-description`, {
      style,
      length,
      additionalInstructions,
    });
  }

  // ---------------- implementation ----------------

  private async resolveByGallery(galleryId: string): Promise<PhotoListItemDto[]> {
    const outboxCount = await this.localDb.countOutboxEntries();

    if (this.connectivity.isOnline && outboxCount === 0) {
      try {
        const photos = await firstValueFrom(
          this.api.get<PhotoListItemDto[]>(`Photo/by-gallery/${galleryId}`),
        );
        await this.localDb.replaceGalleryPhotos(galleryId, photos);
        return photos;
      } catch (err) {
        if (!isConnectivityError(err)) throw err;
      }
    }

    return this.localDb.getMirroredPhotos(galleryId);
  }

  private async resolveCreate(dto: Partial<PhotoDto>): Promise<PhotoDto> {
    if (this.connectivity.isOnline) {
      try {
        const photo = await firstValueFrom(this.api.post<PhotoDto>('Photo', dto));
        await this.localDb.upsertMirroredPhoto(toListItem(photo), photo.galleryId!);
        return photo;
      } catch (err) {
        if (!isConnectivityError(err)) throw err;
      }
    }

    // Offline draft only - no outbox entry yet. upload() (always called
    // right after, by every caller) queues the linked create+upload once
    // it has the file bytes to attach.
    const clientTempId = crypto.randomUUID();
    const now = new Date().toISOString();
    const draft: PhotoDto = {
      id: clientTempId,
      galleryId: dto.galleryId,
      title: dto.title ?? '',
      description: dto.description,
      createdAtUtc: now,
      hasOriginal: false,
      hasThumbnail: false,
      isDeleted: false,
      tags: [],
    };

    await this.localDb.upsertMirroredPhoto(toListItem(draft), dto.galleryId!, 'create');
    return draft;
  }

  private async resolveUpload(photoId: string, file: File): Promise<void> {
    const mirrored = await this.localDb.getMirroredPhoto(photoId);
    const isPendingCreate = mirrored?.pendingOp === 'create';

    if (this.connectivity.isOnline && !isPendingCreate) {
      const formData = new FormData();
      formData.append('file', file);

      try {
        await withRetry(() =>
          firstValueFrom(this.api.post<void>(`Photo/${photoId}/upload`, formData)),
        );
        await this.localDb.markMirroredPhotoHasFiles(photoId);
        return;
      } catch (err) {
        if (!isConnectivityError(err)) throw err;
      }
    }

    if (!mirrored) return;

    await this.localDb.appendOutboxEntry({
      type: 'create-photo',
      entityType: 'photo',
      entityId: photoId,
      createdAt: Date.now(),
      payload: { galleryId: mirrored.galleryId, title: mirrored.title, clientTempId: photoId, file },
    });

    // Cache the raw bytes locally (as both thumbnail and original - there's
    // no client-side resize) so the draft is visible before it ever syncs.
    await this.localDb.putThumbnail(photoId, file);
    await this.localDb.putOriginal(photoId, file);
    await this.localDb.markMirroredPhotoHasFiles(photoId);
  }

  private async resolveUpdate(
    id: string,
    dto: { title: string; description?: string },
  ): Promise<{ updatedAtUtc: string } | void> {
    if (this.connectivity.isOnline) {
      try {
        const result = await firstValueFrom(
          this.api.put<{ updatedAtUtc: string }>(`photo/${id}`, dto),
        );
        await this.patchMirroredPhoto(id, {
          title: dto.title,
          description: dto.description,
          updatedAtUtc: result.updatedAtUtc,
        });
        return result;
      } catch (err) {
        if (!isConnectivityError(err)) throw err;
      }
    }

    const mirrored = await this.localDb.getMirroredPhoto(id);
    const isPendingCreate = mirrored?.pendingOp === 'create';

    await this.patchMirroredPhoto(
      id,
      { title: dto.title, description: dto.description },
      isPendingCreate ? 'create' : 'update',
    );

    if (isPendingCreate) return;

    await this.localDb.upsertOutboxEntry({
      type: 'update',
      entityType: 'photo',
      entityId: id,
      createdAt: Date.now(),
      baseUpdatedAtUtc: mirrored?.updatedAtUtc,
      payload: { title: dto.title, description: dto.description },
    });
  }

  private async resolveDelete(id: string): Promise<void> {
    if (this.connectivity.isOnline) {
      try {
        await firstValueFrom(this.api.delete<void>(`Photo/${id}`));
        await this.localDb.tombstoneMirroredPhoto(id);
        return;
      } catch (err) {
        if (!isConnectivityError(err)) throw err;
      }
    }

    const existing = await this.localDb.getMirroredPhoto(id);

    if (existing?.pendingOp === 'create') {
      await this.localDb.removeMirroredPhoto(id);
      await this.removeOutboxEntriesFor(id);
      return;
    }

    await this.localDb.tombstoneMirroredPhoto(id);
    await this.localDb.appendOutboxEntry({
      type: 'delete',
      entityType: 'photo',
      entityId: id,
      createdAt: Date.now(),
      baseUpdatedAtUtc: existing?.updatedAtUtc,
      payload: {},
    });
  }

  private async resolveMove(id: string, galleryId: string): Promise<void> {
    if (this.connectivity.isOnline) {
      try {
        await firstValueFrom(this.api.post<void>(`Photo/${id}/move`, { galleryId }));
        await this.localDb.moveMirroredPhoto(id, galleryId);
        return;
      } catch (err) {
        if (!isConnectivityError(err)) throw err;
      }
    }

    const existing = await this.localDb.getMirroredPhoto(id);
    const isPendingCreate = existing?.pendingOp === 'create';

    await this.localDb.moveMirroredPhoto(id, galleryId, isPendingCreate ? 'create' : 'update');

    if (isPendingCreate) {
      // Nothing synced yet - just retarget the queued create's gallery.
      const entries = await this.localDb.getOutboxEntries();
      const createEntry = entries.find(
        (e) => e.type === 'create-photo' && e.entityId === id,
      );
      if (createEntry?.opId !== undefined) {
        await this.localDb.removeOutboxEntry(createEntry.opId);
        await this.localDb.appendOutboxEntry({ ...createEntry, payload: { ...createEntry.payload, galleryId } });
      }
      return;
    }

    await this.localDb.upsertOutboxEntry({
      type: 'move',
      entityType: 'photo',
      entityId: id,
      createdAt: Date.now(),
      baseUpdatedAtUtc: existing?.updatedAtUtc,
      payload: { galleryId },
    });
  }

  private async resolveCopy(id: string, galleryId: string): Promise<PhotoDto | void> {
    if (this.connectivity.isOnline) {
      try {
        const copy = await firstValueFrom(this.api.post<PhotoDto>(`Photo/${id}/copy`, { galleryId }));
        await this.localDb.upsertMirroredPhoto(toListItem(copy), galleryId);
        return copy;
      } catch (err) {
        if (!isConnectivityError(err)) throw err;
      }
    }

    // Copying while offline needs a real source photo to duplicate, and a
    // real server id for the copy is only known once it's synced - a
    // pending-create source (never synced) can't be copied yet either.
    const source = await this.localDb.getMirroredPhoto(id);
    if (!source || source.pendingOp === 'create') {
      throw new Error('Cannot copy a photo while offline unless it is already synced.');
    }

    const newClientTempId = crypto.randomUUID();
    const draft: PhotoListItemDto = { ...source, id: newClientTempId };
    await this.localDb.upsertMirroredPhoto(draft, galleryId, 'create');

    await this.localDb.appendOutboxEntry({
      type: 'copy',
      entityType: 'photo',
      entityId: id,
      createdAt: Date.now(),
      baseUpdatedAtUtc: source.updatedAtUtc,
      payload: { galleryId, newClientTempId },
    });
  }

  private async resolveSetTags(id: string, tagNames: string[]): Promise<string[]> {
    if (this.connectivity.isOnline) {
      try {
        const result = await firstValueFrom(
          this.api.put<{ tagNames: string[]; updatedAtUtc: string }>(`Photo/${id}/tags`, { tagNames }),
        );
        await this.patchMirroredPhoto(id, {
          tags: result.tagNames,
          updatedAtUtc: result.updatedAtUtc,
        });
        return result.tagNames;
      } catch (err) {
        if (!isConnectivityError(err)) throw err;
      }
    }

    const mirrored = await this.localDb.getMirroredPhoto(id);
    const isPendingCreate = mirrored?.pendingOp === 'create';

    await this.patchMirroredPhoto(id, { tags: tagNames }, isPendingCreate ? 'create' : 'update');

    if (!isPendingCreate) {
      await this.localDb.upsertOutboxEntry({
        type: 'setTags',
        entityType: 'photo',
        entityId: id,
        createdAt: Date.now(),
        baseUpdatedAtUtc: mirrored?.updatedAtUtc,
        payload: { tagNames },
      });
    }

    return tagNames;
  }

  private async resolveReorder(galleryId: string, photoIds: string[]): Promise<void> {
    if (this.connectivity.isOnline) {
      try {
        await firstValueFrom(this.api.put<void>('Photo/reorder', { galleryId, photoIds }));
        await this.localDb.patchMirroredPhotoOrder(galleryId, photoIds);
        return;
      } catch (err) {
        if (!isConnectivityError(err)) throw err;
      }
    }

    await this.localDb.patchMirroredPhotoOrder(galleryId, photoIds);
    await this.localDb.upsertReorderOutboxEntry('photo', galleryId, photoIds);
  }

  private async patchMirroredPhoto(
    id: string,
    patch: Partial<PhotoListItemDto>,
    pendingOp?: 'create' | 'update' | 'delete',
  ): Promise<void> {
    const existing = await this.localDb.getMirroredPhoto(id);
    if (!existing) return;

    await this.localDb.upsertMirroredPhoto({ ...existing, ...patch }, existing.galleryId, pendingOp);
  }

  private async removeOutboxEntriesFor(entityId: string): Promise<void> {
    const entries = await this.localDb.getOutboxEntries();
    await Promise.all(
      entries
        .filter((e) => e.entityType === 'photo' && e.entityId === entityId && e.opId !== undefined)
        .map((e) => this.localDb.removeOutboxEntry(e.opId!)),
    );
  }
}

function isConnectivityError(err: unknown): boolean {
  return err instanceof HttpErrorResponse && err.status === 0;
}

const UPLOAD_RETRY_ATTEMPTS = 3;
const UPLOAD_RETRY_DELAY_MS = 800;

/**
 * Retries a request a couple of times on transient server-side failures
 * (5xx, request timeout, rate limit) before giving up - a slow/overloaded
 * server on one attempt shouldn't immediately fall back to queuing the
 * whole upload for later sync. A true connectivity error (status 0) is
 * deliberately NOT retried here - that's handled by the existing offline
 * fallback right below the call site, which is the more appropriate
 * recovery path for "the network is actually down".
 */
async function withRetry<T>(fn: () => Promise<T>, attempts = UPLOAD_RETRY_ATTEMPTS): Promise<T> {
  for (let attempt = 1; ; attempt++) {
    try {
      return await fn();
    } catch (err) {
      if (attempt >= attempts || !isRetryableError(err)) throw err;
      await sleep(UPLOAD_RETRY_DELAY_MS * attempt);
    }
  }
}

function isRetryableError(err: unknown): boolean {
  if (!(err instanceof HttpErrorResponse)) return false;
  return err.status >= 500 || err.status === 408 || err.status === 429;
}

function sleep(ms: number): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

function toListItem(photo: PhotoDto): PhotoListItemDto {
  return {
    id: photo.id,
    title: photo.title,
    description: photo.description,
    hasThumbnail: photo.hasThumbnail,
    updatedAtUtc: photo.updatedAtUtc ?? photo.createdAtUtc,
    tags: photo.tags,
  };
}
