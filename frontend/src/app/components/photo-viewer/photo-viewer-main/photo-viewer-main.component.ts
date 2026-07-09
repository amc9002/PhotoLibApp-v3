import { Component, EventEmitter, Input, OnChanges, OnDestroy, Output, SimpleChanges } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Subject, Subscription, catchError, of, switchMap } from 'rxjs';
import { ImageCacheService } from '../../../core/offline/image-cache.service';

/** Fallback crossfade duration when `crossfadeMs` isn't set (e.g. outside a slideshow). */
const DEFAULT_CROSSFADE_MS = 800;

/** A shown photo's blob URL plus an identity distinct from every other layer ever shown. */
interface PhotoLayer {
  id: number;
  url: string;
}

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
  /** Duration of that dissolve, configurable from the slideshow settings. */
  @Input() crossfadeMs = DEFAULT_CROSSFADE_MS;

  /**
   * Fires once the request for the current `photoId` has settled (loaded or
   * failed) - the slideshow uses this to pace itself off actual load
   * completion instead of a fixed timer, since a fixed timer can outrun a
   * slow load and keep cancelling it before anything ever displays.
   */
  @Output() loaded = new EventEmitter<void>();

  /** Currently shown photo. */
  currentLayer: PhotoLayer | null = null;
  /** Previous photo, faded out simultaneously with `currentLayer` fading in over it. */
  previousLayer: PhotoLayer | null = null;
  /** Drives the fade-in transition on `currentLayer` once it's in the DOM at opacity 0. */
  fadingIn = false;
  /** Drives the fade-out transition on `previousLayer`, in lockstep with `fadingIn`. */
  fadingOut = false;

  // switchMap so rapid next/prev navigation cancels stale in-flight loads
  // instead of racing to display whichever happens to resolve last.
  private photoId$ = new Subject<string>();
  private sub: Subscription;
  private fadeTimeout?: ReturnType<typeof setTimeout>;
  private nextLayerId = 0;

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
    this.releaseUrl(this.currentLayer);
    this.releaseUrl(this.previousLayer);
  }

  /**
   * `*ngFor`'d (with `trackByLayerId`) instead of bound via a plain `*ngIf`
   * so the <img> is a genuinely new DOM element every time the photo
   * changes. Reusing the same element and just toggling a CSS class would
   * *retarget* the opacity transition already sitting at 1 from the last
   * cycle rather than actually restart it from 0, so only the very first
   * photo would ever visibly fade in.
   */
  get currentLayerList(): PhotoLayer[] {
    return this.currentLayer ? [this.currentLayer] : [];
  }

  trackByLayerId(index: number, layer: PhotoLayer): number {
    return layer.id;
  }

  /**
   * Runs `callback` after a real paint of whatever state is on screen right
   * now. A single `requestAnimationFrame` is enough in Chromium, but
   * Firefox can coalesce a class change made there with the state that was
   * just set synchronously - the transition never gets a "before" frame to
   * animate from, so it jumps straight to its end value instead of
   * animating. A second rAF forces that paint to actually happen first.
   */
  private afterPaint(callback: () => void) {
    requestAnimationFrame(() => {
      requestAnimationFrame(callback);
    });
  }

  private applyNewUrl(url: string | null) {
    if (this.fadeTimeout) {
      clearTimeout(this.fadeTimeout);
      this.fadeTimeout = undefined;
      // A previous crossfade was interrupted by a new photo arriving early -
      // its outgoing layer is done being shown, so release it now.
      this.releaseUrl(this.previousLayer);
      this.previousLayer = null;
    }

    const newLayer: PhotoLayer | null = url ? { id: this.nextLayerId++, url } : null;

    if (this.crossfade && this.currentLayer && newLayer) {
      this.previousLayer = this.currentLayer;
      this.currentLayer = newLayer;
      this.fadingIn = false;
      this.fadingOut = false;

      // Let both <img>s paint at their starting opacity first, then flip
      // together - the two need to start in the same frame to stay in
      // lockstep (outgoing fading out exactly as incoming fades in).
      this.afterPaint(() => {
        this.fadingIn = true;
        this.fadingOut = true;
      });

      this.fadeTimeout = setTimeout(() => {
        this.releaseUrl(this.previousLayer);
        this.previousLayer = null;
        this.fadeTimeout = undefined;
      }, this.crossfadeMs);
    } else if (this.crossfade) {
      // First photo of a slideshow - nothing to cross from yet, but it
      // should still fade in rather than pop straight to full opacity.
      this.releaseUrl(this.currentLayer);
      this.currentLayer = newLayer;
      this.fadingIn = false;
      this.afterPaint(() => {
        this.fadingIn = true;
      });
    } else {
      this.releaseUrl(this.currentLayer);
      this.currentLayer = newLayer;
      this.fadingIn = true;
    }

    // The request for `photoId` has settled (successfully or not) - since
    // switchMap only ever lets the latest request's result reach here, this
    // always corresponds to the current `photoId`.
    this.loaded.emit();
  }

  private releaseUrl(layer: PhotoLayer | null) {
    if (layer) {
      URL.revokeObjectURL(layer.url);
    }
  }
}
