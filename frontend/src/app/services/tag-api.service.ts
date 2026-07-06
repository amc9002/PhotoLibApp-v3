import { Injectable } from '@angular/core';
import { ApiService } from '../core/api/api.service';

export interface TagDto {
  id: string;
  name: string;
}

@Injectable({ providedIn: 'root' })
export class TagApiService {
  constructor(private api: ApiService) {}

  getAll() {
    return this.api.get<TagDto[]>('Tag');
  }
}
