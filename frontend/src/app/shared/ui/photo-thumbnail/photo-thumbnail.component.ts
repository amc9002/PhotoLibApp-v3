import { Component, Input, OnChanges, OnDestroy, SimpleChanges } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Subject, Subscription, catchError, of, switchMap } from 'rxjs';
import { ImageCacheService } from '../../../core/offline/image-cache.service';

@Component({
  selector: 'app-photo-thumbnail',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './photo-thumbnail.component.html',
  styleUrls: ['./photo-thumbnail.component.css'],
})
export class PhotoThumbnailComponent implements OnChanges, OnDestroy {
  @Input({ required: true }) photoId!: string;
  @Input() title = '';
  @Input() hasThumbnail = true;
  @Input() alt = 'Photo thumbnail';
  @Input() loading: 'lazy' | 'eager' = 'lazy';

  objectUrl: string | null = null;

  // switchMap so a component instance reused for a different photoId (e.g.
  // recycled by trackBy) cancels its stale in-flight load instead of racing
  // to overwrite a newer objectUrl without revoking it.
  private request$ = new Subject<string | null>();
  private sub: Subscription;

  constructor(private imageCache: ImageCacheService) {
    this.sub = this.request$
      .pipe(
        switchMap((id) => {
          if (!id) return of(null);
          return this.imageCache.getThumbnailUrl$(id).pipe(
            catchError((err) => {
              console.error('Failed to load thumbnail', id, err);
              return of(null);
            }),
          );
        }),
      )
      .subscribe((url) => {
        this.releaseUrl();
        this.objectUrl = url;
      });
  }

  ngOnChanges(changes: SimpleChanges) {
    if (changes['photoId'] || changes['hasThumbnail']) {
      this.load();
    }
  }

  ngOnDestroy() {
    this.sub.unsubscribe();
    this.releaseUrl();
  }

  private load() {
    this.request$.next(this.hasThumbnail && this.photoId ? this.photoId : null);
  }

  private releaseUrl() {
    if (this.objectUrl) {
      URL.revokeObjectURL(this.objectUrl);
      this.objectUrl = null;
    }
  }
}
