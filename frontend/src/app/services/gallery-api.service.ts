import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { ApiService } from '../core/api/api.service';
import { GalleryDto } from '../models/gallery.dto';
import { Gallery } from '../models/gallery.model';

@Injectable({
  providedIn: 'root',
})
export class GalleryApiService {
  constructor(
    private api: ApiService,
    private http: HttpClient,
  ) {}

  getAll() {
    return this.api.get<Gallery[]>('Gallery');
  }

  create(dto: Partial<GalleryDto>) {
    return this.api.post<GalleryDto>('Gallery', dto);
  }

  delete(id: string) {
    return this.api.delete<void>(`galleries/${id}`);
  }
}
