import { Component, Input, OnChanges, OnDestroy, SimpleChanges } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Subject, Subscription, catchError, of, switchMap } from 'rxjs';
import { ImageCacheService } from '../../../core/offline/image-cache.service';

@Component({
  selector: 'app-photo-viewer-main',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './photo-viewer-main.component.html',
  styleUrls: ['./photo-viewer-main.component.css'],
})
export class PhotoViewerMainComponent implements OnChanges, OnDestroy {
  @Input({ required: true }) photoId!: string;
  @Input() open = false;

  objectUrl: string | null = null;

  // switchMap so rapid next/prev navigation cancels stale in-flight loads
  // instead of racing to display whichever happens to resolve last.
  private photoId$ = new Subject<string>();
  private sub: Subscription;

  constructor(private imageCache: ImageCacheService) {
    this.sub = this.photoId$
      .pipe(
        switchMap((id) =>
          this.imageCache.getOriginalUrl$(id).pipe(
            catchError((err) => {
              console.error('Failed to load photo', id, err);
              return of(null);
            }),
          ),
        ),
      )
      .subscribe((url) => {
        this.releaseUrl();
        this.objectUrl = url;
      });
  }

  ngOnChanges(changes: SimpleChanges) {
    if (changes['photoId'] && this.photoId) {
      this.photoId$.next(this.photoId);
    }
  }

  ngOnDestroy() {
    this.sub.unsubscribe();
    this.releaseUrl();
  }

  private releaseUrl() {
    if (this.objectUrl) {
      URL.revokeObjectURL(this.objectUrl);
      this.objectUrl = null;
    }
  }
}
