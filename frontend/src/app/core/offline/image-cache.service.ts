import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { defer, firstValueFrom, from, Observable } from 'rxjs';
import { LocalDbService } from './local-db.service';

/**
 * Resolves photo images through a local IndexedDB blob cache, falling back
 * to the network on a miss. Thumbnails are cached purely lazily (only the
 * galleries/photos actually rendered ever get fetched+cached - there is no
 * eager pre-warming). Originals are capped at MAX_CACHED_ORIGINALS via LRU
 * eviction in LocalDbService, touched every time a photo is viewed.
 *
 * Callers get an object URL back and are responsible for calling
 * `URL.revokeObjectURL` once they're done with it (e.g. in ngOnDestroy) to
 * avoid leaking memory over a long session.
 */
@Injectable({ providedIn: 'root' })
export class ImageCacheService {
  constructor(
    private http: HttpClient,
    private localDb: LocalDbService,
  ) {}

  getThumbnailUrl$(photoId: string): Observable<string> {
    return defer(() => from(this.resolveThumbnail(photoId)));
  }

  getOriginalUrl$(photoId: string): Observable<string> {
    return defer(() => from(this.resolveOriginal(photoId)));
  }

  private async resolveThumbnail(photoId: string): Promise<string> {
    const cached = await this.localDb.getThumbnail(photoId);
    if (cached) return URL.createObjectURL(cached);

    const blob = await this.fetchBlob(`/api/Photo/${photoId}/thumbnail`);
    // Caching is an optimization, not a display requirement - if the write
    // fails (e.g. WebKit rejecting the Blob), still show the fetched image
    // instead of losing it.
    await this.localDb.putThumbnail(photoId, blob).catch((err) => {
      console.error('Failed to cache thumbnail', photoId, err);
    });
    return URL.createObjectURL(blob);
  }

  private async resolveOriginal(photoId: string): Promise<string> {
    const cached = await this.localDb.getOriginal(photoId);
    if (cached) {
      // Touching is fire-and-forget from the caller's perspective - the
      // object URL doesn't need to wait on it.
      void this.localDb.touchOriginal(photoId);
      return URL.createObjectURL(cached);
    }

    const blob = await this.fetchBlob(`/api/Photo/${photoId}/file`);
    await this.localDb.putOriginal(photoId, blob).catch((err) => {
      console.error('Failed to cache original', photoId, err);
    });
    return URL.createObjectURL(blob);
  }

  private fetchBlob(url: string): Promise<Blob> {
    return firstValueFrom(this.http.get(url, { responseType: 'blob' }));
  }
}
