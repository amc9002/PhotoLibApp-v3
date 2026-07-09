import {
  Component,
  EventEmitter,
  HostBinding,
  Input,
  OnChanges,
  OnDestroy,
  Output,
  SimpleChanges,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { Subject, Subscription, catchError, of, switchMap } from 'rxjs';
import { ImageCacheService } from '../../../../core/offline/image-cache.service';
import { PhotoListItemDto } from '../../../../models/photoLisrItem.dto';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';

/**
 * Loads and displays one photo's original image inside the scroll feed,
 * plus a collapsible info panel (title/description/tags) to its right.
 * A dedicated component (rather than inline in the feed's `*cdkVirtualFor`
 * template) so Angular's own change-detection lifecycle
 * (`ngOnChanges`/`ngOnDestroy`) drives the fetch/cancel/revoke - correct
 * whether the CDK virtual-scroll viewport recycles this instance for a
 * different photo as it scrolls (`ngOnChanges`) or actually destroys it
 * (`ngOnDestroy`). Mirrors `PhotoViewerMainComponent`'s load pattern minus
 * the crossfade machinery, which doesn't apply to a scrolling feed.
 */
@Component({
  selector: 'app-photo-feed-item',
  standalone: true,
  imports: [CommonModule, TranslatePipe],
  templateUrl: './photo-feed-item.component.html',
  styleUrls: ['./photo-feed-item.component.css'],
})
export class PhotoFeedItemComponent implements OnChanges, OnDestroy {
  @Input({ required: true }) photo!: PhotoListItemDto;
  /**
   * Row height in px, matching the virtual-scroll viewport's fixed
   * `itemSize`. Applied as an inline style on the host - `<app-photo-feed-item>`
   * has no default `display: block`, so without an explicit, definite pixel
   * height here, descendants using `height: 100%` (e.g. the info panel)
   * silently collapse to 0: the percentage has nothing definite to resolve
   * against. (`.feed-item-media` only "worked" without this because flex
   * `align-items: stretch` sizes it independently of the percentage chain.)
   */
  @Input() height = 640;
  @HostBinding('style.height.px') get hostHeight() {
    return this.height;
  }
  @HostBinding('style.display') readonly hostDisplay = 'block';

  @Output() menu = new EventEmitter<MouseEvent>();

  url: string | null = null;
  infoOpen = false;

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
        this.url = url;
      });
  }

  get hasDescription(): boolean {
    return !!this.photo.description?.trim();
  }

  ngOnChanges(changes: SimpleChanges) {
    if (!changes['photo'] || !this.photo) return;

    // A recycled row can receive a new `photo` object reference for the
    // *same* id (e.g. after editing metadata elsewhere refreshes the list) -
    // that's just a data patch, not a different photo, so the image/expanded
    // state shouldn't reset.
    const previous = changes['photo'].previousValue as PhotoListItemDto | undefined;
    if (previous?.id === this.photo.id) return;

    // Release the previously-shown blob (if any) right away rather than
    // waiting for the new one to arrive - this row is switching to a
    // different photo (CDK virtual-scroll reused this instance), so the
    // old blob URL is immediately stale.
    this.releaseUrl();
    this.url = null;
    this.infoOpen = false;
    this.photoId$.next(this.photo.id);
  }

  toggleInfo() {
    this.infoOpen = !this.infoOpen;
  }

  ngOnDestroy() {
    this.sub.unsubscribe();
    this.releaseUrl();
  }

  private releaseUrl() {
    if (this.url) URL.revokeObjectURL(this.url);
  }
}
