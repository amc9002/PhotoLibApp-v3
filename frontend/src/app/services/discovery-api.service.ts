import { Injectable } from '@angular/core';
import { ApiService } from '../core/api/api.service';
import { DiscoveryPhotoDto } from '../models/discovery-photo.dto';

/**
 * Discovery requires connectivity, full stop - unlike PhotoApiService, this
 * has no IndexedDB-mirror/outbox fallback. The feed is live-sampled server
 * side on every call, so there's nothing meaningful to mirror offline.
 */
@Injectable({ providedIn: 'root' })
export class DiscoveryApiService {
  constructor(private api: ApiService) {}

  getFeed(count: number, excludeIds: string[]) {
    const exclude = excludeIds.length ? `&exclude=${excludeIds.join(',')}` : '';
    return this.api.get<DiscoveryPhotoDto[]>(`Discovery/feed?count=${count}${exclude}`);
  }

  recordView(photoId: string) {
    return this.api.post<void>(`Photo/${photoId}/view`, {});
  }
}
