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
    return this.api.get<PhotoDto>(`photos/${id}`);
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

  update(id: string, dto: Partial<PhotoDto>) {
    return this.api.put<PhotoDto>(`photos/${id}`, dto);
  }

  delete(id: string) {
    return this.api.delete<void>(`photos/${id}`);
  }
}
