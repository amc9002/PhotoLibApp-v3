import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { PhotoListItemDto } from '../../models/photoLisrItem.dto';
import { PhotoCarouselComponent } from '../photo-carousel/photo-carousel.component';
import { PhotoViewerMainComponent } from '../photo-viewer-main/photo-viewer-main.component';

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

  onBackdropClick() {
    this.close.emit();
  }
}
