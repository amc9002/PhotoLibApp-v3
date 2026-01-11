import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { PhotoListItemDto } from '../../models/photoLisrItem.dto';
import { PhotoThumbnailComponent } from '../photo-thumbnail/photo-thumbnail.component';

@Component({
  selector: 'app-photo-carousel',
  standalone: true,
  imports: [CommonModule, PhotoThumbnailComponent],
  templateUrl: './photo-carousel.component.html',
  styleUrls: ['./photo-carousel.component.css'],
})
export class PhotoCarouselComponent {
  @Input({ required: true }) photos!: PhotoListItemDto[];
  @Input({ required: true }) activePhotoId!: string;

  @Output() photoSelected = new EventEmitter<string>();

  selectPhoto(photoId: string) {
    this.photoSelected.emit(photoId);
  }
}
