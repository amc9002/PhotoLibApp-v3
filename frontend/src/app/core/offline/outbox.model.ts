/**
 * One queued offline mutation. Entries are replayed (in Phase 4) in
 * strict chronological order (their IndexedDB autoincrement key), which is
 * automatically causally correct - an entry can never be queued against an
 * entity before the entry that created it.
 */
export type OutboxEntityType = 'gallery' | 'photo';

interface OutboxEntryBase {
  /** Autoincrement primary key, assigned by IndexedDB. Absent until inserted. */
  opId?: number;
  entityType: OutboxEntityType;
  /** The id this operation targets - a real server id, or a clientTempId for a not-yet-synced create. */
  entityId: string;
  createdAt: number;
  /**
   * The entity's `updatedAtUtc` as last known to this client, captured when
   * the entry was queued. Undefined for creates. Used in Phase 4 to detect
   * whether the server moved since this operation was queued.
   */
  baseUpdatedAtUtc?: string;
}

export interface CreateGalleryOutboxEntry extends OutboxEntryBase {
  type: 'create-gallery';
  entityType: 'gallery';
  payload: { title: string; clientTempId: string };
}

export interface CreatePhotoOutboxEntry extends OutboxEntryBase {
  type: 'create-photo';
  entityType: 'photo';
  payload: { galleryId: string; title: string; clientTempId: string; file: File };
}

export interface UpdateOutboxEntry extends OutboxEntryBase {
  type: 'update';
  payload: { title: string; description?: string };
}

export interface DeleteOutboxEntry extends OutboxEntryBase {
  type: 'delete';
  payload: Record<string, never>;
}

export interface MovePhotoOutboxEntry extends OutboxEntryBase {
  type: 'move';
  entityType: 'photo';
  payload: { galleryId: string };
}

export interface CopyPhotoOutboxEntry extends OutboxEntryBase {
  type: 'copy';
  entityType: 'photo';
  payload: { galleryId: string; newClientTempId: string };
}

export interface SetTagsOutboxEntry extends OutboxEntryBase {
  type: 'setTags';
  payload: { tagNames: string[] };
}

export interface ReorderOutboxEntry extends OutboxEntryBase {
  type: 'reorder';
  /** entityId is the gallery id being reordered for photos, or the fixed string 'all' for the gallery list itself. */
  payload: { ids: string[] };
}

export type OutboxEntry =
  | CreateGalleryOutboxEntry
  | CreatePhotoOutboxEntry
  | UpdateOutboxEntry
  | DeleteOutboxEntry
  | MovePhotoOutboxEntry
  | CopyPhotoOutboxEntry
  | SetTagsOutboxEntry
  | ReorderOutboxEntry;

/** Fixed entityId used for the (single, coalesced) gallery-list reorder entry. */
export const GALLERY_REORDER_ENTITY_ID = 'all';
