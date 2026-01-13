import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { PhotoListItemDto } from '../../../models/photoLisrItem.dto';
import { PhotoThumbnailComponent } from '../../photo-thumbnail/photo-thumbnail.component';

@Component({
  selector: 'app-gallery-grid',
  standalone: true,
  imports: [CommonModule, PhotoThumbnailComponent],
  templateUrl: './gallery-grid.component.html',
  styleUrls: ['./gallery-grid.component.css'],
})
export class GalleryGridComponent {
  @Input({ required: true }) photos!: PhotoListItemDto[];
  @Output() photoClicked = new EventEmitter<string>();
  @Input() activePhotoId: string | null = null;

  onPhotoClick(photoId: string) {
    this.photoClicked.emit(photoId);
  }

  trackById(index: number, photo: PhotoListItemDto): string {
    return photo.id;
  }
}
