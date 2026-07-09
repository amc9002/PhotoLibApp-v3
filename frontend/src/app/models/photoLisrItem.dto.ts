export interface PhotoListItemDto {
  id: string;
  title: string;
  description?: string;
  hasThumbnail: boolean;
  updatedAtUtc: string;
  tags: string[];
}
