import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { PhotoListItemDto } from '../../models/photoLisrItem.dto';
import { PhotoCarouselComponent } from '../photo-carousel/photo-carousel.component';
import { PhotoViewerMainComponent } from '../photo-viewer-main/photo-viewer-main.component';
import { HostListener } from '@angular/core';

@Component({
  selector: 'app-photo-viewer',
  standalone: true,
  imports: [CommonModule, PhotoCarouselComponent, PhotoViewerMainComponent],
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

  onBackdropClick() {
    this.close.emit();
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
