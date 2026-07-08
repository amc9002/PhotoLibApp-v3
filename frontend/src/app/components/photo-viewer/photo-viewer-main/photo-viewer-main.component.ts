import { Component, EventEmitter, Input, OnChanges, OnDestroy, Output, SimpleChanges } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Subject, Subscription, catchError, of, switchMap } from 'rxjs';
import { ImageCacheService } from '../../../core/offline/image-cache.service';

/** How long the outgoing photo stays visible under the incoming one during a crossfade. */
const CROSSFADE_MS = 800;

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
  /** When true (slideshow playback), dissolves between photos instead of an instant swap. */
  @Input() crossfade = false;

  /**
   * Fires once the request for the current `photoId` has settled (loaded or
   * failed) - the slideshow uses this to pace itself off actual load
   * completion instead of a fixed timer, since a fixed timer can outrun a
   * slow load and keep cancelling it before anything ever displays.
   */
  @Output() loaded = new EventEmitter<void>();

  /** Currently shown photo. */
  currentUrl: string | null = null;
  /** Previous photo, kept visible underneath while `currentUrl` fades in over it. */
  previousUrl: string | null = null;
  /** Drives the fade-in transition on `currentUrl` once it's in the DOM at opacity 0. */
  fadingIn = false;

  // switchMap so rapid next/prev navigation cancels stale in-flight loads
  // instead of racing to display whichever happens to resolve last.
  private photoId$ = new Subject<string>();
  private sub: Subscription;
  private fadeTimeout?: ReturnType<typeof setTimeout>;

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
      .subscribe((url) => this.applyNewUrl(url));
  }

  ngOnChanges(changes: SimpleChanges) {
    if (changes['photoId'] && this.photoId) {
      this.photoId$.next(this.photoId);
    }
  }

  ngOnDestroy() {
    this.sub.unsubscribe();
    if (this.fadeTimeout) clearTimeout(this.fadeTimeout);
    this.releaseUrl(this.currentUrl);
    this.releaseUrl(this.previousUrl);
  }

  private applyNewUrl(url: string | null) {
    if (this.fadeTimeout) {
      clearTimeout(this.fadeTimeout);
      this.fadeTimeout = undefined;
      // A previous crossfade was interrupted by a new photo arriving early -
      // its outgoing layer is done being shown, so release it now.
      this.releaseUrl(this.previousUrl);
      this.previousUrl = null;
    }

    if (this.crossfade && this.currentUrl) {
      this.previousUrl = this.currentUrl;
      this.currentUrl = url;
      this.fadingIn = false;

      // Let the new <img> paint at opacity 0 first, then transition to 1 -
      // setting both in the same tick would skip the transition entirely.
      requestAnimationFrame(() => {
        this.fadingIn = true;
      });

      this.fadeTimeout = setTimeout(() => {
        this.releaseUrl(this.previousUrl);
        this.previousUrl = null;
        this.fadeTimeout = undefined;
      }, CROSSFADE_MS);
    } else if (this.crossfade) {
      // First photo of a slideshow - nothing to cross from yet, but it
      // should still fade in rather than pop straight to full opacity.
      this.releaseUrl(this.currentUrl);
      this.currentUrl = url;
      this.fadingIn = false;
      requestAnimationFrame(() => {
        this.fadingIn = true;
      });
    } else {
      this.releaseUrl(this.currentUrl);
      this.currentUrl = url;
      this.fadingIn = true;
    }

    // The request for `photoId` has settled (successfully or not) - since
    // switchMap only ever lets the latest request's result reach here, this
    // always corresponds to the current `photoId`.
    this.loaded.emit();
  }

  private releaseUrl(url: string | null) {
    if (url) {
      URL.revokeObjectURL(url);
    }
  }
}
