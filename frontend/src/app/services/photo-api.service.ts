import { Injectable } from '@angular/core';
import { ApiService } from '../core/api/api.service';
import { PhotoDto } from '../models/photo.dto';
import { PhotoListItemDto } from '../models/photoLisrItem.dto';

@Injectable({
  providedIn: 'root',
})
export class PhotoApiService {
  constructor(private api: ApiService) {}

  getAll() {
    return this.api.get<PhotoDto[]>('photos');
  }

  getById(id: string) {
    return this.api.get<PhotoDto>(`Photo/${id}`);
  }

  getByGallery(galleryId: string) {
    return this.api.get<PhotoListItemDto[]>(`Photo/by-gallery/${galleryId}`);
  }

  create(dto: Partial<PhotoDto>) {
    return this.api.post<PhotoDto>('Photo', dto);
  }

  upload(photoId: string, file: File) {
    const formData = new FormData();
    formData.append('file', file);

    return this.api.post<void>(`Photo/${photoId}/upload`, formData);
  }

  update(id: string, dto: { title: string; description?: string }) {
    return this.api.put<PhotoDto>(`photo/${id}`, dto);
  }

  delete(id: string) {
    return this.api.delete<void>(`Photo/${id}`);
  }

  move(id: string, galleryId: string) {
    return this.api.post<void>(`Photo/${id}/move`, { galleryId });
  }

  copy(id: string, galleryId: string) {
    return this.api.post<PhotoDto>(`Photo/${id}/copy`, { galleryId });
  }

  setTags(id: string, tagNames: string[]) {
    return this.api.put<string[]>(`Photo/${id}/tags`, { tagNames });
  }

  reorder(galleryId: string, photoIds: string[]) {
    return this.api.put<void>('Photo/reorder', { galleryId, photoIds });
  }
}
