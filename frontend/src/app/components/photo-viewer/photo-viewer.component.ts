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
import { PhotoListItemDto } from '../../models/photoLisrItem.dto';
import { PhotoCarouselComponent } from './photo-carousel/photo-carousel.component';
import { PhotoViewerMainComponent } from './photo-viewer-main/photo-viewer-main.component';
import { HostListener } from '@angular/core';
import { PhotoActionsComponent } from './photo-actions/photo-actions.component';
import { EditMetadataModalComponent } from '../../shared/modal/edit-metadata-modal/edit-metadata-modal.component';
import { PhotoInfoModalComponent } from './photo-actions/photo-info-modal/photo-info-modal.component';
import { PhotoMetadataEditingService } from '../../services/photo-metadata-editing.service';
import { SlideshowPlayerService } from './slideshow-player.service';
import { SlideshowConfig } from '../../models/slideshow-config';
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
  providers: [PhotoMetadataEditingService, SlideshowPlayerService],
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

  /** Whether the title/description side panel is folded away to give the photo full width. */
  infoPanelCollapsed = false;

  /** True while the viewer occupies the real (browser-chrome-free) Fullscreen API state. */
  isFullscreen = false;
  private readonly onFullscreenChange = () => {
    this.isFullscreen = document.fullscreenElement === this.viewerRootRef.nativeElement;
  };

  private infoPanelCollapsedBeforeSlideshow = false;

  @HostListener('window:keydown', ['$event'])
  onKeydown(event: KeyboardEvent) {
    if (event.key === 'Escape') {
      if (this.metadataEditing.editOpen || this.metadataEditing.infoOpen) {
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

    // No photos means there's nothing to navigate to.
    if (!this.photos?.length || !this.activePhotoId) {
      return;
    }

    // Find the current photo's index.
    const currentIndex = this.photos.findIndex(
      (photo) => photo.id === this.activePhotoId,
    );

    if (currentIndex === -1) {
      return;
    }

    if (!this.metadataEditing.editOpen && !this.metadataEditing.infoOpen) {
      // Next photo (→)
      if (event.key === 'ArrowRight') {
        event.preventDefault();

        const nextIndex = Math.min(currentIndex + 1, this.photos.length - 1);

        this.manualSelect(this.photos[nextIndex].id);
      }

      // Previous photo (←)
      if (event.key === 'ArrowLeft') {
        event.preventDefault();

        const prevIndex = Math.max(currentIndex - 1, 0);

        this.manualSelect(this.photos[prevIndex].id);
      }
    }
  }

  constructor(
    public metadataEditing: PhotoMetadataEditingService,
    public slideshow: SlideshowPlayerService,
  ) {}

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
    this.slideshow.clearTimer();
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
    if (this.metadataEditing.editOpen || this.metadataEditing.infoOpen) {
      return;
    }
    this.close.emit();
  }

  togglePhotoMenu() {
    this.photoMenuOpen = !this.photoMenuOpen;
  }

  openEditMetadata() {
    if (!this.activePhotoId) return;
    this.metadataEditing.openEditFor(this.activePhotoId);
  }

  onSaveMetadata(data: { title: string; description: string; tags: string[] }) {
    this.metadataEditing.save(this.photos, data);
  }

  onShowInfo() {
    if (!this.activePhotoId) return;
    this.metadataEditing.showInfoFor(this.activePhotoId);
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
    if (this.slideshow.active) {
      this.stopSlideshow();
    }
    this.photoSelected.emit(photoId);
  }

  // ---------------- slideshow ----------------

  private startSlideshow(config: SlideshowConfig) {
    if (!this.photos?.length) return;

    this.infoPanelCollapsedBeforeSlideshow = this.infoPanelCollapsed;
    this.infoPanelCollapsed = true;

    this.slideshow.start(this.photos, config, this.activePhotoId, (photoId) =>
      this.photoSelected.emit(photoId),
    );
  }

  /** Called once the currently displayed photo has actually finished loading (or failed). */
  onMainPhotoLoaded() {
    this.slideshow.onPhotoLoaded(this.activePhotoId);
  }

  stopSlideshow() {
    if (!this.slideshow.active) return;

    this.slideshow.stop();
    this.infoPanelCollapsed = this.infoPanelCollapsedBeforeSlideshow;
  }
}

function isTextEntryTarget(target: EventTarget | null): boolean {
  if (!(target instanceof HTMLElement)) return false;
  const tag = target.tagName;
  return tag === 'INPUT' || tag === 'TEXTAREA' || tag === 'SELECT' || target.isContentEditable;
}
