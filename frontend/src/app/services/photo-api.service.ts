import { Injectable } from '@angular/core';
import { ApiService } from '../core/api/api.service';
import { PhotoDto } from '../models/photo.dto';

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

  create(dto: Partial<PhotoDto>) {
    return this.api.post<PhotoDto>('photos', dto);
  }

  update(id: string, dto: Partial<PhotoDto>) {
    return this.api.put<PhotoDto>(`photos/${id}`, dto);
  }

  delete(id: string) {
    return this.api.delete<void>(`photos/${id}`);
  }
}
