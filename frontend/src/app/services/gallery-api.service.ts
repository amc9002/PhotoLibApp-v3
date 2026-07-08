import { Injectable } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { Observable, defer, firstValueFrom, from } from 'rxjs';
import { ApiService } from '../core/api/api.service';
import { ConnectivityService } from '../core/offline/connectivity.service';
import { LocalDbService } from '../core/offline/local-db.service';
import { GALLERY_REORDER_ENTITY_ID } from '../core/offline/outbox.model';
import { GalleryDto } from '../models/gallery.dto';
import { Gallery } from '../models/gallery.model';

@Injectable({
  providedIn: 'root',
})
export class GalleryApiService {
  constructor(
    private api: ApiService,
    private connectivity: ConnectivityService,
    private localDb: LocalDbService,
  ) {}

  /**
   * Online (and nothing pending offline): fetches fresh and refreshes the
   * local mirror. Offline, unreachable, or there's an unconfirmed offline
   * change sitting in the outbox: serves the mirror, so a reconnect doesn't
   * make a pending create/delete flicker before the user has reviewed it.
   */
  getAll(): Observable<Gallery[]> {
    return defer(() => from(this.resolveAll()));
  }

  create(dto: Partial<GalleryDto>): Observable<Gallery> {
    return defer(() => from(this.resolveCreate(dto)));
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

  setTags(id: string, tagNames: string[]): Observable<string[]> {
    return defer(() => from(this.resolveSetTags(id, tagNames)));
  }

  reorder(galleryIds: string[]): Observable<void> {
    return defer(() => from(this.resolveReorder(galleryIds)));
  }

  // ---------------- implementation ----------------

  private async resolveAll(): Promise<Gallery[]> {
    const outboxCount = await this.localDb.countOutboxEntries();

    if (this.connectivity.isOnline && outboxCount === 0) {
      try {
        const galleries = await firstValueFrom(this.api.get<Gallery[]>('Gallery'));
        await this.localDb.replaceGalleries(galleries);
        return galleries;
      } catch (err) {
        if (!isConnectivityError(err)) throw err;
      }
    }

    return this.localDb.getMirroredGalleries();
  }

  private async resolveCreate(dto: Partial<GalleryDto>): Promise<Gallery> {
    if (this.connectivity.isOnline) {
      try {
        const gallery = await firstValueFrom(this.api.post<Gallery>('Gallery', dto));
        await this.localDb.upsertMirroredGallery(gallery);
        return gallery;
      } catch (err) {
        if (!isConnectivityError(err)) throw err;
      }
    }

    const clientTempId = crypto.randomUUID();
    const now = new Date().toISOString();
    const draft: Gallery = {
      id: clientTempId,
      title: dto.title ?? '',
      description: dto.description,
      createdAtUtc: now,
      updatedAtUtc: now,
      isDeleted: false,
      tags: [],
    };

    await this.localDb.upsertMirroredGallery(draft, 'create');
    await this.localDb.appendOutboxEntry({
      type: 'create-gallery',
      entityType: 'gallery',
      entityId: clientTempId,
      createdAt: Date.now(),
      payload: { title: draft.title, clientTempId },
    });

    return draft;
  }

  private async resolveUpdate(
    id: string,
    dto: { title: string; description?: string },
  ): Promise<{ updatedAtUtc: string } | void> {
    if (this.connectivity.isOnline) {
      try {
        const result = await firstValueFrom(
          this.api.put<{ updatedAtUtc: string }>(`Gallery/${id}`, dto),
        );
        const existing = await this.localDb.getMirroredGallery(id);
        if (existing) {
          await this.localDb.upsertMirroredGallery({
            ...existing,
            title: dto.title,
            description: dto.description,
            updatedAtUtc: result.updatedAtUtc,
          });
        }
        return result;
      } catch (err) {
        if (!isConnectivityError(err)) throw err;
      }
    }

    const existing = await this.localDb.getMirroredGallery(id);
    const isPendingCreate = existing?.pendingOp === 'create';

    await this.localDb.upsertMirroredGallery(
      {
        ...(existing ?? blankGallery(id)),
        title: dto.title,
        description: dto.description,
        updatedAtUtc: new Date().toISOString(),
      },
      isPendingCreate ? 'create' : 'update',
    );

    // A not-yet-synced create just gets its own queued payload updated in
    // place - no separate "update" to replay once it's eventually created.
    if (isPendingCreate) return;

    await this.localDb.upsertOutboxEntry({
      type: 'update',
      entityType: 'gallery',
      entityId: id,
      createdAt: Date.now(),
      baseUpdatedAtUtc: existing?.updatedAtUtc,
      payload: { title: dto.title, description: dto.description },
    });
  }

  private async resolveDelete(id: string): Promise<void> {
    if (this.connectivity.isOnline) {
      try {
        await firstValueFrom(this.api.delete<void>(`Gallery/${id}`));
        await this.localDb.tombstoneMirroredGallery(id);
        return;
      } catch (err) {
        if (!isConnectivityError(err)) throw err;
      }
    }

    const existing = await this.localDb.getMirroredGallery(id);

    if (existing?.pendingOp === 'create') {
      // Never made it to the server - cancelling locally is enough.
      await this.localDb.removeMirroredGallery(id);
      await this.removeOutboxEntriesFor('gallery', id);
      return;
    }

    await this.localDb.tombstoneMirroredGallery(id);
    await this.localDb.appendOutboxEntry({
      type: 'delete',
      entityType: 'gallery',
      entityId: id,
      createdAt: Date.now(),
      baseUpdatedAtUtc: existing?.updatedAtUtc,
      payload: {},
    });
  }

  private async resolveSetTags(id: string, tagNames: string[]): Promise<string[]> {
    if (this.connectivity.isOnline) {
      try {
        const result = await firstValueFrom(
          this.api.put<{ tagNames: string[]; updatedAtUtc: string }>(`Gallery/${id}/tags`, { tagNames }),
        );
        const existing = await this.localDb.getMirroredGallery(id);
        if (existing) {
          await this.localDb.upsertMirroredGallery({
            ...existing,
            tags: result.tagNames,
            updatedAtUtc: result.updatedAtUtc,
          });
        }
        return result.tagNames;
      } catch (err) {
        if (!isConnectivityError(err)) throw err;
      }
    }

    const existing = await this.localDb.getMirroredGallery(id);
    const isPendingCreate = existing?.pendingOp === 'create';

    await this.localDb.upsertMirroredGallery(
      { ...(existing ?? blankGallery(id)), tags: tagNames },
      isPendingCreate ? 'create' : 'update',
    );

    if (!isPendingCreate) {
      await this.localDb.upsertOutboxEntry({
        type: 'setTags',
        entityType: 'gallery',
        entityId: id,
        createdAt: Date.now(),
        baseUpdatedAtUtc: existing?.updatedAtUtc,
        payload: { tagNames },
      });
    }

    return tagNames;
  }

  private async resolveReorder(galleryIds: string[]): Promise<void> {
    if (this.connectivity.isOnline) {
      try {
        await firstValueFrom(this.api.put<void>('Gallery/reorder', { galleryIds }));
        await this.localDb.patchMirroredGalleryOrder(galleryIds);
        return;
      } catch (err) {
        if (!isConnectivityError(err)) throw err;
      }
    }

    await this.localDb.patchMirroredGalleryOrder(galleryIds);
    await this.localDb.upsertReorderOutboxEntry('gallery', GALLERY_REORDER_ENTITY_ID, galleryIds);
  }

  private async removeOutboxEntriesFor(entityType: 'gallery' | 'photo', entityId: string): Promise<void> {
    const entries = await this.localDb.getOutboxEntries();
    await Promise.all(
      entries
        .filter((e) => e.entityType === entityType && e.entityId === entityId && e.opId !== undefined)
        .map((e) => this.localDb.removeOutboxEntry(e.opId!)),
    );
  }
}

function isConnectivityError(err: unknown): boolean {
  return err instanceof HttpErrorResponse && err.status === 0;
}

function blankGallery(id: string): Gallery {
  const now = new Date().toISOString();
  return { id, title: '', createdAtUtc: now, updatedAtUtc: now, isDeleted: false, tags: [] };
}
