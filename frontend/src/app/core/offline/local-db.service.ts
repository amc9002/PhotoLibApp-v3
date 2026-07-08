import { Injectable } from '@angular/core';
import { DBSchema, IDBPDatabase, openDB } from 'idb';
import { Gallery } from '../../models/gallery.model';
import { PhotoListItemDto } from '../../models/photoLisrItem.dto';
import { OutboxEntry } from './outbox.model';

type PendingOp = 'create' | 'update' | 'delete';

/** A gallery as mirrored locally, with its position in the last known server order. */
type MirroredGallery = Gallery & { order: number; pendingOp?: PendingOp };

/**
 * A photo as mirrored locally, with its gallery and position within that
 * gallery. `localDeleted` is separate from the DTO's own fields (the DTO
 * returned by `by-gallery` never carries a deleted flag at all, since the
 * server already excludes deleted photos from that response) and marks an
 * offline tombstone not yet confirmed by the server.
 */
type MirroredPhoto = PhotoListItemDto & {
  galleryId: string;
  order: number;
  pendingOp?: PendingOp;
  localDeleted?: boolean;
};

/**
 * IndexedDB schema for offline data: the image-blob caches (thumbnails,
 * originals), a local read mirror of galleries/photos, and the outbox of
 * pending offline writes.
 */
interface PhotoLibCacheSchema extends DBSchema {
  thumbnails: {
    key: string; // photoId
    value: { photoId: string; blob: Blob; cachedAt: number };
  };
  originals: {
    key: string; // photoId
    value: { photoId: string; blob: Blob; viewedAt: number };
    indexes: { viewedAt: number };
  };
  galleries: {
    key: string; // gallery id
    value: MirroredGallery;
    indexes: { order: number };
  };
  photos: {
    key: string; // photo id
    value: MirroredPhoto;
    indexes: { galleryId: string; galleryId_order: [string, number] };
  };
  outbox: {
    key: number; // opId, autoincrement
    value: OutboxEntry;
  };
}

const DB_NAME = 'photolib-cache';
const DB_VERSION = 3;

/** Hard cap on how many full-size originals are kept locally; oldest-viewed is evicted first. */
export const MAX_CACHED_ORIGINALS = 10;

/**
 * Thin promise-based wrapper around the app's IndexedDB cache. Owns the
 * database connection/upgrade lifecycle; callers go through the typed
 * methods below rather than touching `idb` directly.
 */
@Injectable({ providedIn: 'root' })
export class LocalDbService {
  private readonly dbPromise: Promise<IDBPDatabase<PhotoLibCacheSchema>>;

  constructor() {
    // Best-effort: without this, a disk-pressure eviction could silently
    // wipe the cache, including unconfirmed offline edits sitting in the
    // outbox, not just the disposable image caches.
    navigator.storage?.persist?.().catch(() => {});

    this.dbPromise = openDB<PhotoLibCacheSchema>(DB_NAME, DB_VERSION, {
      upgrade(db) {
        if (!db.objectStoreNames.contains('thumbnails')) {
          db.createObjectStore('thumbnails', { keyPath: 'photoId' });
        }
        if (!db.objectStoreNames.contains('originals')) {
          const originals = db.createObjectStore('originals', { keyPath: 'photoId' });
          originals.createIndex('viewedAt', 'viewedAt');
        }
        if (!db.objectStoreNames.contains('galleries')) {
          const galleries = db.createObjectStore('galleries', { keyPath: 'id' });
          galleries.createIndex('order', 'order');
        }
        if (!db.objectStoreNames.contains('photos')) {
          const photos = db.createObjectStore('photos', { keyPath: 'id' });
          photos.createIndex('galleryId', 'galleryId');
          photos.createIndex('galleryId_order', ['galleryId', 'order']);
        }
        if (!db.objectStoreNames.contains('outbox')) {
          db.createObjectStore('outbox', { keyPath: 'opId', autoIncrement: true });
        }
      },
    });
  }

  // ---------------- image blob caches (Phase 1) ----------------

  async getThumbnail(photoId: string): Promise<Blob | undefined> {
    const db = await this.dbPromise;
    return (await db.get('thumbnails', photoId))?.blob;
  }

  async putThumbnail(photoId: string, blob: Blob): Promise<void> {
    const db = await this.dbPromise;
    await db.put('thumbnails', { photoId, blob, cachedAt: Date.now() });
  }

  async getOriginal(photoId: string): Promise<Blob | undefined> {
    const db = await this.dbPromise;
    return (await db.get('originals', photoId))?.blob;
  }

  /** Marks an already-cached original as freshly viewed, without re-fetching it. */
  async touchOriginal(photoId: string): Promise<void> {
    const db = await this.dbPromise;
    const existing = await db.get('originals', photoId);
    if (!existing) return;

    existing.viewedAt = Date.now();
    await db.put('originals', existing);
  }

  /** Stores a newly-fetched original and evicts the least-recently-viewed entries over the cap. */
  async putOriginal(photoId: string, blob: Blob): Promise<void> {
    const db = await this.dbPromise;
    await db.put('originals', { photoId, blob, viewedAt: Date.now() });
    await this.evictExcessOriginals(db);
  }

  /** Moves cached blobs from a clientTempId key to the real id the server assigned. */
  async remapImageCacheId(oldId: string, newId: string): Promise<void> {
    const db = await this.dbPromise;

    const thumb = await db.get('thumbnails', oldId);
    if (thumb) {
      await db.delete('thumbnails', oldId);
      await db.put('thumbnails', { ...thumb, photoId: newId });
    }

    const original = await db.get('originals', oldId);
    if (original) {
      await db.delete('originals', oldId);
      await db.put('originals', { ...original, photoId: newId });
    }
  }

  private async evictExcessOriginals(db: IDBPDatabase<PhotoLibCacheSchema>): Promise<void> {
    const count = await db.count('originals');
    let excess = count - MAX_CACHED_ORIGINALS;
    if (excess <= 0) return;

    const tx = db.transaction('originals', 'readwrite');
    // No direction = ascending by viewedAt, i.e. oldest-viewed first.
    let cursor = await tx.store.index('viewedAt').openCursor();

    while (cursor && excess > 0) {
      await cursor.delete();
      excess--;
      cursor = await cursor.continue();
    }

    await tx.done;
  }

  // ---------------- gallery mirror (Phase 2 + 3) ----------------

  /** Replaces the entire mirrored gallery list with a freshly-fetched one, in server order. */
  async replaceGalleries(galleries: Gallery[]): Promise<void> {
    const db = await this.dbPromise;
    const tx = db.transaction('galleries', 'readwrite');

    await tx.store.clear();
    await Promise.all(galleries.map((g, order) => tx.store.put({ ...g, order })));
    await tx.done;
  }

  /** Returns the mirrored gallery list in the last known server order, excluding tombstoned entries. */
  async getMirroredGalleries(): Promise<Gallery[]> {
    const db = await this.dbPromise;
    const records = await db.getAllFromIndex('galleries', 'order');
    return records.filter((g) => !g.isDeleted).map(stripOrder);
  }

  async getMirroredGallery(id: string): Promise<MirroredGallery | undefined> {
    const db = await this.dbPromise;
    return db.get('galleries', id);
  }

  /** Inserts or patches one gallery in the mirror, appending at the end of the order if new. */
  async upsertMirroredGallery(gallery: Gallery, pendingOp?: PendingOp): Promise<void> {
    const db = await this.dbPromise;
    const tx = db.transaction('galleries', 'readwrite');
    const existing = await tx.store.get(gallery.id);
    const order = existing?.order ?? (await tx.store.index('order').count());

    await tx.store.put({ ...gallery, order, pendingOp: pendingOp ?? existing?.pendingOp });
    await tx.done;
  }

  /** Marks a gallery deleted locally without removing its mirror row (server hasn't confirmed yet). */
  async tombstoneMirroredGallery(id: string): Promise<void> {
    const db = await this.dbPromise;
    const existing = await db.get('galleries', id);
    if (!existing) return;

    await db.put('galleries', { ...existing, isDeleted: true, pendingOp: 'delete' });
  }

  /** Removes a gallery's mirror row outright - only correct for cancelling a not-yet-synced create. */
  async removeMirroredGallery(id: string): Promise<void> {
    const db = await this.dbPromise;
    await db.delete('galleries', id);
  }

  /** Restores local visibility after the user discards a pending offline delete. */
  async untombstoneMirroredGallery(id: string): Promise<void> {
    const db = await this.dbPromise;
    const existing = await db.get('galleries', id);
    if (!existing) return;

    const { pendingOp, ...rest } = existing;
    await db.put('galleries', { ...rest, isDeleted: false });
  }

  async patchMirroredGalleryOrder(orderedIds: string[]): Promise<void> {
    const db = await this.dbPromise;
    const tx = db.transaction('galleries', 'readwrite');

    await Promise.all(
      orderedIds.map(async (id, order) => {
        const existing = await tx.store.get(id);
        if (existing) await tx.store.put({ ...existing, order });
      }),
    );
    await tx.done;
  }

  // ---------------- photo mirror (Phase 2 + 3) ----------------

  /** Replaces one gallery's mirrored photo list, in server order. Other galleries' photos are untouched. */
  async replaceGalleryPhotos(galleryId: string, photos: PhotoListItemDto[]): Promise<void> {
    const db = await this.dbPromise;
    const tx = db.transaction('photos', 'readwrite');
    const index = tx.store.index('galleryId');

    let cursor = await index.openCursor(IDBKeyRange.only(galleryId));
    while (cursor) {
      await cursor.delete();
      cursor = await cursor.continue();
    }

    await Promise.all(photos.map((p, order) => tx.store.put({ ...p, galleryId, order })));
    await tx.done;
  }

  /** Returns one gallery's mirrored photos, in the last known server order, excluding tombstoned entries. */
  async getMirroredPhotos(galleryId: string): Promise<PhotoListItemDto[]> {
    const db = await this.dbPromise;
    const range = IDBKeyRange.bound([galleryId, -Infinity], [galleryId, +Infinity]);
    const records = await db.getAllFromIndex('photos', 'galleryId_order', range);
    return records.filter((p) => !p.localDeleted).map(stripPhotoExtras);
  }

  async getMirroredPhoto(id: string): Promise<MirroredPhoto | undefined> {
    const db = await this.dbPromise;
    return db.get('photos', id);
  }

  /** Inserts or patches one photo in the mirror, appending at the end of its gallery's order if new. */
  async upsertMirroredPhoto(
    photo: PhotoListItemDto,
    galleryId: string,
    pendingOp?: PendingOp,
  ): Promise<void> {
    const db = await this.dbPromise;
    const tx = db.transaction('photos', 'readwrite');
    const existing = await tx.store.get(photo.id);
    const order =
      existing?.order ??
      (await tx.store.index('galleryId').count(IDBKeyRange.only(galleryId)));

    await tx.store.put({
      ...photo,
      galleryId,
      order,
      pendingOp: pendingOp ?? existing?.pendingOp,
      localDeleted: existing?.localDeleted,
    });
    await tx.done;
  }

  async markMirroredPhotoHasFiles(id: string): Promise<void> {
    const db = await this.dbPromise;
    const existing = await db.get('photos', id);
    if (!existing) return;

    await db.put('photos', { ...existing, hasThumbnail: true });
  }

  /** Marks a photo deleted locally without removing its mirror row (server hasn't confirmed yet). */
  async tombstoneMirroredPhoto(id: string): Promise<void> {
    const db = await this.dbPromise;
    const existing = await db.get('photos', id);
    if (!existing) return;

    await db.put('photos', { ...existing, localDeleted: true, pendingOp: 'delete' });
  }

  /** Restores local visibility after the user discards a pending offline delete. */
  async untombstoneMirroredPhoto(id: string): Promise<void> {
    const db = await this.dbPromise;
    const existing = await db.get('photos', id);
    if (!existing) return;

    const { pendingOp, ...rest } = existing;
    await db.put('photos', { ...rest, localDeleted: false });
  }

  /** Replaces a mirror row's id (e.g. a clientTempId once the server assigns a real id), keeping its data and order. */
  async remapMirroredPhotoId(oldId: string, newId: string): Promise<void> {
    const db = await this.dbPromise;
    const tx = db.transaction('photos', 'readwrite');
    const existing = await tx.store.get(oldId);
    if (existing) {
      const { pendingOp, ...rest } = existing;
      await tx.store.delete(oldId);
      await tx.store.put({ ...rest, id: newId });
    }
    await tx.done;
  }

  async remapMirroredGalleryId(oldId: string, newId: string): Promise<void> {
    const db = await this.dbPromise;
    const tx = db.transaction('galleries', 'readwrite');
    const existing = await tx.store.get(oldId);
    if (existing) {
      const { pendingOp, ...rest } = existing;
      await tx.store.delete(oldId);
      await tx.store.put({ ...rest, id: newId });
    }
    await tx.done;
  }

  /** Rewrites every remaining outbox entry that references `oldId` (as entityId or inside its payload) to `newId`. */
  async remapOutboxReferences(oldId: string, newId: string): Promise<void> {
    const db = await this.dbPromise;
    const tx = db.transaction('outbox', 'readwrite');
    let cursor = await tx.store.openCursor();

    while (cursor) {
      const entry = cursor.value;
      let changed = false;

      if (entry.entityId === oldId) {
        entry.entityId = newId;
        changed = true;
      }
      if (entry.type === 'reorder' && entry.payload.ids.includes(oldId)) {
        entry.payload = { ids: entry.payload.ids.map((id) => (id === oldId ? newId : id)) };
        changed = true;
      }

      if (changed) await cursor.update(entry);
      cursor = await cursor.continue();
    }

    await tx.done;
  }

  /** Removes a photo's mirror row outright - only correct for cancelling a not-yet-synced create. */
  async removeMirroredPhoto(id: string): Promise<void> {
    const db = await this.dbPromise;
    await db.delete('photos', id);
  }

  /** Moves a photo to a different gallery in the mirror, appending it at the end there. */
  async moveMirroredPhoto(id: string, toGalleryId: string, pendingOp?: PendingOp): Promise<void> {
    const db = await this.dbPromise;
    const tx = db.transaction('photos', 'readwrite');
    const existing = await tx.store.get(id);
    if (!existing) {
      await tx.done;
      return;
    }

    const order = await tx.store.index('galleryId').count(IDBKeyRange.only(toGalleryId));
    await tx.store.put({
      ...existing,
      galleryId: toGalleryId,
      order,
      pendingOp: pendingOp ?? existing.pendingOp,
    });
    await tx.done;
  }

  async patchMirroredPhotoOrder(galleryId: string, orderedIds: string[]): Promise<void> {
    const db = await this.dbPromise;
    const tx = db.transaction('photos', 'readwrite');

    await Promise.all(
      orderedIds.map(async (id, order) => {
        const existing = await tx.store.get(id);
        if (existing && existing.galleryId === galleryId) {
          await tx.store.put({ ...existing, order });
        }
      }),
    );
    await tx.done;
  }

  // ---------------- outbox (Phase 3) ----------------

  async appendOutboxEntry(entry: Omit<OutboxEntry, 'opId'>): Promise<void> {
    const db = await this.dbPromise;
    await db.add('outbox', entry as OutboxEntry);
  }

  /**
   * Queues an `update`/`setTags`/`move` entry, replacing any earlier
   * not-yet-synced entry of the same type for the same entity instead of
   * piling up redundant ones - only the final payload matters. Without
   * this, repeated offline edits of one entity produced multiple
   * indistinguishable rows in the sync review, and discarding one left
   * the other queued to silently reapply part of the "discarded" edit.
   */
  async upsertOutboxEntry(entry: Omit<OutboxEntry, 'opId'>): Promise<void> {
    const db = await this.dbPromise;
    const tx = db.transaction('outbox', 'readwrite');

    let cursor = await tx.store.openCursor();
    while (cursor) {
      const existing = cursor.value;
      if (
        existing.type === entry.type &&
        existing.entityType === entry.entityType &&
        existing.entityId === entry.entityId
      ) {
        await cursor.delete();
      }
      cursor = await cursor.continue();
    }

    await tx.store.add(entry as OutboxEntry);
    await tx.done;
  }

  /** In insertion (chronological) order, since the key is an autoincrement counter. */
  async getOutboxEntries(): Promise<OutboxEntry[]> {
    const db = await this.dbPromise;
    return db.getAll('outbox');
  }

  async countOutboxEntries(): Promise<number> {
    const db = await this.dbPromise;
    return db.count('outbox');
  }

  async removeOutboxEntry(opId: number): Promise<void> {
    const db = await this.dbPromise;
    await db.delete('outbox', opId);
  }

  /**
   * Queues a reorder, replacing any earlier not-yet-synced reorder for the
   * same scope (a gallery's photos, or the gallery list itself when
   * `scope` is undefined) instead of piling up redundant entries - only
   * the final order matters.
   */
  async upsertReorderOutboxEntry(
    entityType: 'gallery' | 'photo',
    entityId: string,
    ids: string[],
  ): Promise<void> {
    const db = await this.dbPromise;
    const tx = db.transaction('outbox', 'readwrite');

    let cursor = await tx.store.openCursor();
    while (cursor) {
      const entry = cursor.value;
      if (entry.type === 'reorder' && entry.entityType === entityType && entry.entityId === entityId) {
        await cursor.delete();
      }
      cursor = await cursor.continue();
    }

    await tx.store.add({
      type: 'reorder',
      entityType,
      entityId,
      createdAt: Date.now(),
      payload: { ids },
    } as OutboxEntry);
    await tx.done;
  }
}

function stripOrder<T extends { order: number; pendingOp?: PendingOp }>(
  record: T,
): Omit<T, 'order' | 'pendingOp'> {
  const { order, pendingOp, ...rest } = record;
  return rest;
}

function stripPhotoExtras(record: MirroredPhoto): PhotoListItemDto {
  const { galleryId, order, pendingOp, localDeleted, ...photo } = record;
  return photo;
}
