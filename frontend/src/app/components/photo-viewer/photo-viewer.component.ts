import {
  Component,
  ElementRef,
  EventEmitter,
  Input,
  OnDestroy,
  OnInit,
  Output,
  ViewChild,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { forkJoin } from 'rxjs';
import { PhotoListItemDto } from '../../models/photoLisrItem.dto';
import { PhotoCarouselComponent } from './photo-carousel/photo-carousel.component';
import { PhotoViewerMainComponent } from './photo-viewer-main/photo-viewer-main.component';
import { HostListener } from '@angular/core';
import { PhotoActionsComponent } from './photo-actions/photo-actions.component';
import { EditMetadataModalComponent } from '../../shared/modal/edit-metadata-modal/edit-metadata-modal.component';
import { PhotoInfoModalComponent } from './photo-actions/photo-info-modal/photo-info-modal.component';
import { PhotoApiService } from '../../services/photo-api.service';
import { PhotoDto } from '../../models/photo.dto';
import { SlideshowConfig, SlideshowOrder } from '../../models/slideshow-config';
import { TranslatePipe } from '../../core/i18n/translate.pipe';

@Component({
  selector: 'app-photo-viewer',
  standalone: true,
  imports: [
    CommonModule,
    PhotoCarouselComponent,
    PhotoViewerMainComponent,
    PhotoActionsComponent,
    EditMetadataModalComponent,
    PhotoInfoModalComponent,
    TranslatePipe,
  ],
  templateUrl: './photo-viewer.component.html',
  styleUrls: ['./photo-viewer.component.css'],
})
export class PhotoViewerComponent implements OnInit, OnDestroy {
  @Input({ required: true }) photos!: PhotoListItemDto[];
  @Input({ required: true }) activePhotoId!: string;
  /** Opens the edit-details modal as soon as the viewer loads, e.g. right after a single upload. */
  @Input() openEditOnLoad = false;
  /** Set to start playing a slideshow as soon as the viewer loads. */
  @Input() slideshowConfig: SlideshowConfig | null = null;

  @Output() close = new EventEmitter<void>();
  @Output() photoSelected = new EventEmitter<string>();
  @Output() requestDelete = new EventEmitter<string>();
  @Output() requestCopy = new EventEmitter<string>();
  @Output() requestMove = new EventEmitter<string>();

  @ViewChild('viewerRoot', { static: true }) viewerRootRef!: ElementRef<HTMLElement>;

  photoMenuOpen = false;
  editMetadataOpen = false;
  isSavingMetadata = false;
  metadataSaveError: string | null = null;
  infoOpen = false;
  infoPhoto?: PhotoDto;

  /** Whether the title/description side panel is folded away to give the photo full width. */
  infoPanelCollapsed = false;

  /** True while the viewer occupies the real (browser-chrome-free) Fullscreen API state. */
  isFullscreen = false;
  private readonly onFullscreenChange = () => {
    this.isFullscreen = document.fullscreenElement === this.viewerRootRef.nativeElement;
  };

  /** True while auto-advancing through `slideshowSequence`. */
  slideshowActive = false;
  private slideshowSequence: PhotoListItemDto[] = [];
  private slideshowIntervalMs = 0;
  /**
   * Paced off `PhotoViewerMainComponent`'s `(loaded)` event rather than a
   * fixed `setInterval` - a metronome that fires regardless of load status
   * would, for a slow-loading original, keep requesting the next photo
   * before the previous request ever resolves. Each request cancels the one
   * before it (see photo-viewer-main's switchMap), so a metronome faster
   * than the load time means nothing ever finishes loading - the carousel
   * advances but the photo itself never appears.
   */
  private slideshowAdvanceTimeout?: ReturnType<typeof setTimeout>;
  private infoPanelCollapsedBeforeSlideshow = false;

  @HostListener('window:keydown', ['$event'])
  onKeydown(event: KeyboardEvent) {
    if (event.key === 'Escape') {
      if (this.editMetadataOpen || this.infoOpen) {
        return;
      }
      if (this.isFullscreen) {
        // The browser already exits fullscreen natively on Escape - just
        // don't also close the viewer on the same keypress.
        return;
      }
      event.preventDefault();
      event.stopPropagation();
      this.close.emit();
      return;
    }

    // Some modals (gallery-select, confirm) render as siblings of the
    // viewer rather than inside it, so the viewer can't see their open
    // state - but it can see where keyboard focus actually is. Any text
    // entry (e.g. typing a new gallery name) should never be hijacked as
    // photo navigation.
    if (isTextEntryTarget(event.target)) {
      return;
    }

    // 2️⃣ Калі няма фота — навігацыя немагчымая
    if (!this.photos?.length || !this.activePhotoId) {
      return;
    }

    // 3️⃣ Знаходзім індэкс бягучага фота
    const currentIndex = this.photos.findIndex(
      (photo) => photo.id === this.activePhotoId,
    );

    if (currentIndex === -1) {
      return;
    }

    if (!this.editMetadataOpen && !this.infoOpen) {
      // 4️⃣ Наступнае фота (→)
      if (event.key === 'ArrowRight') {
        event.preventDefault();

        const nextIndex = Math.min(currentIndex + 1, this.photos.length - 1);

        this.manualSelect(this.photos[nextIndex].id);
      }

      // 5️⃣ Папярэдняе фота (←)
      if (event.key === 'ArrowLeft') {
        event.preventDefault();

        const prevIndex = Math.max(currentIndex - 1, 0);

        this.manualSelect(this.photos[prevIndex].id);
      }
    }
  }

  constructor(private photoApi: PhotoApiService) {}

  ngOnInit() {
    if (this.openEditOnLoad) {
      this.openEditMetadata();
    }
    if (this.slideshowConfig) {
      this.startSlideshow(this.slideshowConfig);
    }
    document.addEventListener('fullscreenchange', this.onFullscreenChange);
  }

  ngOnDestroy() {
    this.clearSlideshowTimer();
    document.removeEventListener('fullscreenchange', this.onFullscreenChange);
    if (this.isFullscreen) {
      document.exitFullscreen?.().catch(() => {});
    }
  }

  get activePhoto() {
    if (!this.photos || !this.activePhotoId) {
      return null;
    }

    return this.photos.find((p) => p.id === this.activePhotoId);
  }

  get activePhotoHasDescription(): boolean {
    return !!this.activePhoto?.description?.trim();
  }

  toggleInfoPanel() {
    this.infoPanelCollapsed = !this.infoPanelCollapsed;
  }

  toggleFullscreen() {
    if (!document.fullscreenElement) {
      this.viewerRootRef.nativeElement.requestFullscreen?.().catch(() => {});
    } else {
      document.exitFullscreen?.().catch(() => {});
    }
  }

  // Backdrop closes viewer; overlay actions must stop event bubbling
  onBackdropClick() {
    if (this.editMetadataOpen || this.infoOpen) {
      return;
    }
    this.close.emit();
  }

  togglePhotoMenu() {
    this.photoMenuOpen = !this.photoMenuOpen;
  }

  openEditMetadata() {
    this.metadataSaveError = null;
    this.editMetadataOpen = true;
  }

  closeEditMetadata() {
    this.editMetadataOpen = false;
  }

  private applyLocalPhotoUpdate(
    photoId: string,
    data: { title: string; description: string; tags: string[] },
  ) {
    const photo = this.photos.find((p) => p.id === photoId);
    if (!photo) {
      return;
    }

    photo.title = data.title;
    photo.description = data.description;
    photo.tags = data.tags;
  }

  onSaveMetadata(data: { title: string; description: string; tags: string[] }) {
    if (!this.activePhotoId) {
      return;
    }
    this.isSavingMetadata = true;
    this.metadataSaveError = null;

    forkJoin([
      this.photoApi.update(this.activePhotoId, data),
      this.photoApi.setTags(this.activePhotoId, data.tags),
    ]).subscribe({
      next: () => {
        this.applyLocalPhotoUpdate(this.activePhotoId!, data);
        this.isSavingMetadata = false;
        this.closeEditMetadata();
      },
      error: (err) => {
        console.error('Failed to update photo metadata', err);
        this.isSavingMetadata = false;
        this.metadataSaveError = 'photoViewer.saveFailed';
      },
    });
  }

  onShowInfo() {
    if (!this.activePhotoId) return;

    this.photoApi.getById(this.activePhotoId).subscribe({
      next: (photo) => {
        this.infoPhoto = photo;
        this.infoOpen = true;
      },
      error: (err) => console.error('Failed to load photo info', err),
    });
  }

  closeInfo() {
    this.infoOpen = false;
    this.infoPhoto = undefined;
  }

  onDeleteRequested() {
    this.requestDelete.emit(this.activePhotoId);
  }

  onCopyRequested() {
    this.requestCopy.emit(this.activePhotoId);
  }

  onMoveRequested() {
    this.requestMove.emit(this.activePhotoId);
  }

  selectNext() {
    if (!this.photos?.length) return;

    const index = this.photos.findIndex((p) => p.id === this.activePhotoId);
    if (index < this.photos.length - 1) {
      this.manualSelect(this.photos[index + 1].id);
    }
  }

  selectPrev() {
    if (!this.photos?.length) return;

    const index = this.photos.findIndex((p) => p.id === this.activePhotoId);
    if (index > 0) {
      this.manualSelect(this.photos[index - 1].id);
    }
  }

  /** Photo selection driven by the user (arrows, keyboard, carousel click) - stops any running slideshow. */
  manualSelect(photoId: string) {
    if (this.slideshowActive) {
      this.stopSlideshow();
    }
    this.photoSelected.emit(photoId);
  }

  // ---------------- slideshow ----------------

  private buildSlideshowSequence(order: SlideshowOrder): PhotoListItemDto[] {
    const sequence = [...this.photos];

    if (order === 'reverse') {
      sequence.reverse();
    } else if (order === 'random') {
      for (let i = sequence.length - 1; i > 0; i--) {
        const j = Math.floor(Math.random() * (i + 1));
        [sequence[i], sequence[j]] = [sequence[j], sequence[i]];
      }
    }

    return sequence;
  }

  private startSlideshow(config: SlideshowConfig) {
    if (!this.photos?.length) return;

    this.slideshowSequence = this.buildSlideshowSequence(config.order);
    this.slideshowIntervalMs = config.intervalMs;
    this.slideshowActive = true;
    this.infoPanelCollapsedBeforeSlideshow = this.infoPanelCollapsed;
    this.infoPanelCollapsed = true;

    const firstId = this.slideshowSequence[0].id;
    if (firstId !== this.activePhotoId) {
      // Deferred: this runs from ngOnInit, still inside the parent's
      // change-detection pass. Emitting synchronously here would update the
      // parent-bound `activePhotoId` input mid-cycle and trigger Angular's
      // ExpressionChangedAfterItHasBeenCheckedError in dev mode.
      setTimeout(() => this.photoSelected.emit(firstId));
    }
    // If firstId === activePhotoId, photo-viewer-main's initial `ngOnChanges`
    // (which fires on first binding too, not just on later changes) still
    // loads it and reports back via `onMainPhotoLoaded`, so pacing starts
    // either way without needing a separate kick here.
  }

  /** Called once the currently displayed photo has actually finished loading (or failed). */
  onMainPhotoLoaded() {
    if (!this.slideshowActive) return;

    this.clearSlideshowTimer();
    // The interval is "how long to look at the photo", separate from and
    // in addition to however long the crossfade itself takes - without
    // adding the transition duration here, a transition configured longer
    // than (or close to) the interval never gets to finish before the next
    // advance interrupts it and restarts it from scratch, which looks like
    // it's barely fading at all no matter how long it's set to.
    const crossfadeMs = this.slideshowConfig?.transitionMs ?? 0;
    const delay = this.slideshowIntervalMs + crossfadeMs;
    this.slideshowAdvanceTimeout = setTimeout(() => this.advanceSlideshow(), delay);
  }

  private advanceSlideshow() {
    if (!this.slideshowSequence.length) return;

    const currentIndex = this.slideshowSequence.findIndex((p) => p.id === this.activePhotoId);
    const nextIndex = (currentIndex + 1) % this.slideshowSequence.length;
    this.photoSelected.emit(this.slideshowSequence[nextIndex].id);
  }

  stopSlideshow() {
    if (!this.slideshowActive) return;

    this.slideshowActive = false;
    this.clearSlideshowTimer();
    this.infoPanelCollapsed = this.infoPanelCollapsedBeforeSlideshow;
  }

  private clearSlideshowTimer() {
    if (this.slideshowAdvanceTimeout !== undefined) {
      clearTimeout(this.slideshowAdvanceTimeout);
      this.slideshowAdvanceTimeout = undefined;
    }
  }
}

function isTextEntryTarget(target: EventTarget | null): boolean {
  if (!(target instanceof HTMLElement)) return false;
  const tag = target.tagName;
  return tag === 'INPUT' || tag === 'TEXTAREA' || tag === 'SELECT' || target.isContentEditable;
}
