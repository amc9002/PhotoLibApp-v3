import { Injectable } from '@angular/core';
import { EMPTY, Observable, catchError, from, mergeMap, switchMap, tap } from 'rxjs';
import { PhotoApiService } from './photo-api.service';

/**
 * Creates photo metadata and uploads the file for a batch of files, with
 * bounded concurrency - pulled out of `AppComponent` since it's a
 * self-contained pipeline that doesn't need any of that component's UI
 * state, unlike the rest of what used to live there.
 */
@Injectable({
  providedIn: 'root',
})
export class PhotoUploadService {
  /**
   * Caps how many files a multi-select upload sends to the server at once.
   * Each upload makes the backend decode the full-resolution original into
   * memory to build a thumbnail, so firing all of them in parallel (as a
   * plain `forEach` once did) could hold dozens of decoded images in memory
   * simultaneously and exhaust it; capping concurrency bounds that to a
   * handful without meaningfully slowing a normal-sized batch down.
   */
  private static readonly MAX_CONCURRENT_UPLOADS = 3;

  constructor(private photoApi: PhotoApiService) {}

  /**
   * Creates metadata and uploads the file for each of `files` into
   * `galleryId`, running up to `MAX_CONCURRENT_UPLOADS` at once. Calls
   * `onUploaded` after each file's upload succeeds, passing its new photo
   * id and whether this was a single-file batch. A file that fails to
   * create or upload is logged and skipped, rather than aborting the rest
   * of the batch.
   */
  uploadAll(
    files: File[],
    galleryId: string,
    onUploaded: (photoId: string, singleFile: boolean) => void,
  ): void {
    const singleFile = files.length === 1;

    from(files)
      .pipe(
        mergeMap(
          (file) => this.createAndUploadOne(file, galleryId, singleFile, onUploaded),
          PhotoUploadService.MAX_CONCURRENT_UPLOADS,
        ),
      )
      .subscribe();
  }

  private createAndUploadOne(
    file: File,
    galleryId: string,
    singleFile: boolean,
    onUploaded: (photoId: string, singleFile: boolean) => void,
  ): Observable<void> {
    return this.photoApi.create({ galleryId, title: file.name }).pipe(
      switchMap((photo) =>
        this.photoApi.upload(photo.id, file).pipe(
          tap(() => onUploaded(photo.id, singleFile)),
          // withRetry (in PhotoApiService) already absorbs transient server
          // hiccups, and a real connectivity error is queued for background
          // sync rather than rejected here - so a rejection reaching this
          // point is a genuine, non-retryable failure worth logging rather
          // than failing silently.
          catchError((err) => {
            console.error('Failed to upload photo file', file.name, err);
            return EMPTY;
          }),
        ),
      ),
      catchError((err) => {
        console.error('Failed to create photo', file.name, err);
        return EMPTY;
      }),
    );
  }
}
