import { Component, HostBinding, Input, OnDestroy, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Subscription } from 'rxjs';
import { DiscoveryPhotoDto } from '../../models/discovery-photo.dto';
import { DiscoveryApiService } from '../../services/discovery-api.service';
import { ConnectivityService } from '../../core/offline/connectivity.service';
import { PhotoThumbnailComponent } from '../../shared/ui/photo-thumbnail/photo-thumbnail.component';
import { PhotoViewerComponent } from '../photo-viewer/photo-viewer.component';
import { TranslatePipe } from '../../core/i18n/translate.pipe';

/** How many photos to request per batch, full or follow-up. */
const BATCH_SIZE = 40;
/** Fetch the next batch once the scroll position is within this many px of the bottom. */
const PRELOAD_DISTANCE_PX = 600;

/**
 * Endless, live-sampled feed of the user's own photos, weighted toward
 * tags they've viewed most (see `DiscoveryFeedService` on the backend).
 * Looks like a regular gallery grid by default (same square-thumbnail tile
 * as `GalleryGridComponent`) rather than a one-photo-at-a-time feed -
 * scrolling and loading more is the only thing that's Discovery-specific.
 * Rendered full-width when no gallery is selected, or as a narrow
 * right-side strip alongside a selected gallery - see `mode`.
 */
@Component({
  selector: 'app-discovery-feed',
  standalone: true,
  imports: [CommonModule, PhotoThumbnailComponent, PhotoViewerComponent, TranslatePipe],
  templateUrl: './discovery-feed.component.html',
  styleUrls: ['./discovery-feed.component.css'],
})
export class DiscoveryFeedComponent implements OnInit, OnDestroy {
  @Input() mode: 'full' | 'collapsed' = 'full';

  /**
   * Width/flex-sizing lives on the host (see discovery-feed.component.css)
   * rather than an inner wrapper div, so this component behaves as a single
   * well-behaved flex item of AppComponent's `.app-content` regardless of
   * whether it's sitting alone (full) or beside `app-gallery-page` (collapsed).
   */
  @HostBinding('class.collapsed') get hostCollapsed() {
    return this.mode === 'collapsed';
  }

  photos: DiscoveryPhotoDto[] = [];
  loadingMore = false;

  viewerOpen = false;
  viewerPhotoId?: string;

  private cameBackOnlineSub: Subscription;

  constructor(
    private discoveryApi: DiscoveryApiService,
    public connectivity: ConnectivityService,
  ) {
    // If Discovery loaded while offline (empty state shown), pick it up
    // automatically once the connection comes back rather than leaving the
    // user stuck on the placeholder until they navigate away and back.
    this.cameBackOnlineSub = this.connectivity.isOnline$.subscribe((online) => {
      if (online && this.photos.length === 0) {
        this.loadInitial();
      }
    });
  }

  ngOnInit() {
    if (this.connectivity.isOnline) {
      this.loadInitial();
    }
  }

  ngOnDestroy() {
    this.cameBackOnlineSub.unsubscribe();
  }

  trackById(index: number, photo: DiscoveryPhotoDto): string {
    return photo.id;
  }

  private loadInitial() {
    this.discoveryApi.getFeed(BATCH_SIZE, []).subscribe({
      next: (photos) => (this.photos = photos),
      error: (err) => console.error('Failed to load Discovery feed', err),
    });
  }

  onScroll(event: Event) {
    const el = event.target as HTMLElement;
    const distanceToBottom = el.scrollHeight - el.scrollTop - el.clientHeight;
    if (distanceToBottom > PRELOAD_DISTANCE_PX) return;
    this.loadMore();
  }

  private loadMore() {
    if (this.loadingMore || this.photos.length === 0) return;

    this.loadingMore = true;
    this.discoveryApi.getFeed(BATCH_SIZE, this.photos.map((p) => p.id)).subscribe({
      next: (more) => {
        this.loadingMore = false;
        this.photos = [...this.photos, ...more];
      },
      error: (err) => {
        this.loadingMore = false;
        console.error('Failed to load more of the Discovery feed', err);
      },
    });
  }

  openViewer(photoId: string) {
    this.viewerPhotoId = photoId;
    this.viewerOpen = true;
  }

  closeViewer() {
    this.viewerOpen = false;
    this.viewerPhotoId = undefined;
  }
}
