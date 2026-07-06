import { Component, EventEmitter, Input, OnDestroy, Output } from '@angular/core';
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
  ],
  templateUrl: './photo-viewer.component.html',
  styleUrls: ['./photo-viewer.component.css'],
})
export class PhotoViewerComponent implements OnDestroy {
  @Input({ required: true }) photos!: PhotoListItemDto[];
  @Input({ required: true }) activePhotoId!: string;

  @Output() close = new EventEmitter<void>();
  @Output() photoSelected = new EventEmitter<string>();
  @Output() requestDelete = new EventEmitter<string>();
  @Output() requestCopy = new EventEmitter<string>();
  @Output() requestMove = new EventEmitter<string>();

  controlsVisible = false;
  controlsHovered = false;
  photoMenuOpen = false;
  editMetadataOpen = false;
  isSavingMetadata = false;
  infoOpen = false;
  infoPhoto?: PhotoDto;

  private hideControlsTimer?: number;

  @HostListener('window:keydown', ['$event'])
  onKeydown(event: KeyboardEvent) {
    if (event.key === 'Escape') {
      if (this.editMetadataOpen || this.infoOpen) {
        return;
      }
      event.preventDefault();
      event.stopPropagation();
      this.close.emit();
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

        this.photoSelected.emit(this.photos[nextIndex].id);
      }

      // 5️⃣ Папярэдняе фота (←)
      if (event.key === 'ArrowLeft') {
        event.preventDefault();

        const prevIndex = Math.max(currentIndex - 1, 0);

        this.photoSelected.emit(this.photos[prevIndex].id);
      }
    }
  }

  constructor(private photoApi: PhotoApiService) {}

  ngOnDestroy() {
    if (this.hideControlsTimer) {
      clearTimeout(this.hideControlsTimer);
    }
  }

  get activePhoto() {
    if (!this.photos || !this.activePhotoId) {
      return null;
    }

    return this.photos.find((p) => p.id === this.activePhotoId);
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
      this.photoSelected.emit(this.photos[index + 1].id);
    }
  }

  selectPrev() {
    if (!this.photos?.length) return;

    const index = this.photos.findIndex((p) => p.id === this.activePhotoId);
    if (index > 0) {
      this.photoSelected.emit(this.photos[index - 1].id);
    }
  }

  @HostListener('document:mousemove')
  onMouseMove() {
    this.controlsVisible = true;

    if (this.hideControlsTimer) {
      clearTimeout(this.hideControlsTimer);
    }

    this.hideControlsTimer = window.setTimeout(() => {
      if (!this.controlsHovered) {
        this.controlsVisible = false;
      }
    }, 2000);
  }
}
