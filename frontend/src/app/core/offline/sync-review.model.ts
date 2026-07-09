export type SyncReviewKind = 'added' | 'deleted' | 'changed';

export interface SyncReviewItem {
  outboxOpId: number;
  entityType: 'gallery' | 'photo';
  entityId: string;
  kind: SyncReviewKind;
  title: string;
  /** Set for photo entries so the modal can show a preview thumbnail. */
  thumbnailPhotoId?: string;
  conflict: boolean;
  conflictReason?: string;
}

export interface SyncReviewSummary {
  items: SyncReviewItem[];
}
