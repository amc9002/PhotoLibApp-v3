export interface PhotoDto {
  id: string;
  title: string;
  description?: string;
  galleryId?: string;
  createdAtUtc: string;
  updatedAtUtc?: string;
  hasOriginal: boolean;
  hasThumbnail: boolean;
  isDeleted: boolean;
  exifJson?: string;
  latitude?: number;
  longitude?: number;
  tags: string[];
}

export interface ExifTag {
  name: string;
  description: string;
}

export interface ExifGroup {
  name: string;
  tags: ExifTag[];
}
