export interface Photo {
  /** Local IndexedDB id (temporary) */
  localId?: number;

  /** Server GUID (permanent) */
  serverId?: string;

  title: string;
  description?: string;

  galleryId?: string;

  createdAt: string;
  updatedAt?: string;

  hasThumbnail?: boolean;

  /** Sync flags */
  isDirty?: boolean;
  isDeleted?: boolean;
}
