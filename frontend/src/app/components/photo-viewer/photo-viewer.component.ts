import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { PhotoListItemDto } from '../../models/photoLisrItem.dto';
import { PhotoCarouselComponent } from './photo-carousel/photo-carousel.component';
import { PhotoViewerMainComponent } from './photo-viewer-main/photo-viewer-main.component';
import { HostListener } from '@angular/core';
import { PhotoActionsComponent } from './photo-actions/photo-actions.component';
import { EditPhotoMetadataModalComponent } from '../edit-photo-metadata-modal/edit-photo-metadata-modal.component';
import { PhotoApiService } from '../../services/photo-api.service';

@Component({
  selector: 'app-photo-viewer',
  standalone: true,
  imports: [
    CommonModule,
    PhotoCarouselComponent,
    PhotoViewerMainComponent,
    PhotoActionsComponent,
    EditPhotoMetadataModalComponent,
  ],
  templateUrl: './photo-viewer.component.html',
  styleUrls: ['./photo-viewer.component.css'],
})
export class PhotoViewerComponent {
  @Input({ required: true }) photos!: PhotoListItemDto[];
  @Input({ required: true }) activePhotoId!: string;

  @Output() close = new EventEmitter<void>();
  @Output() photoSelected = new EventEmitter<string>();

  controlsVisible = false;
  controlsHovered = false;
  photoMenuOpen = false;
  editMetadataOpen = false;
  isSavingMetadata = false;

  private hideControlsTimer?: number;

  @HostListener('window:keydown', ['$event'])
  onKeydown(event: KeyboardEvent) {
    console.log('keydown in viewer:', event.key);
    if (event.key === 'Escape') {
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

    if (!this.editMetadataOpen) {
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

  get activePhoto() {
    if (!this.photos || !this.activePhotoId) {
      return null;
    }

    return this.photos.find((p) => p.id === this.activePhotoId);
  }

  // Backdrop closes viewer; overlay actions must stop event bubbling
  onBackdropClick() {
    this.close.emit();
  }

  togglePhotoMenu() {
    this.photoMenuOpen = !this.photoMenuOpen;
  }

  openEditMetadata() {
    console.log('OPEN EDIT MODAL');
    this.editMetadataOpen = true;
  }

  closeEditMetadata() {
    this.editMetadataOpen = false;
  }

  private applyLocalPhotoUpdate(
    photoId: string,
    data: { title: string; description: string },
  ) {
    const photo = this.photos.find((p) => p.id === photoId);
    if (!photo) {
      return;
    }

    photo.title = data.title;
    photo.description = data.description;
  }

  onSaveMetadata(data: { title: string; description: string }) {
    if (!this.activePhotoId) {
      return;
    }
    this.isSavingMetadata = true;

    this.photoApi.update(this.activePhotoId, data).subscribe({
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
