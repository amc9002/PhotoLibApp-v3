export interface GalleryDto {
  id: string;
  title: string;
  description?: string;
  createdAtUtc: string;
  updatedAtUtc: string;
  isDeleted: boolean;
}
