import { Component, Input, OnChanges, SimpleChanges } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Gallery } from '../../models/gallery.model';
import { PhotoListItemDto } from '../../models/photoLisrItem.dto';
import { PhotoApiService } from '../../services/photo-api.service';

@Component({
  selector: 'app-gallery-view',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './gallery-view.component.html',
  styleUrls: ['./gallery-view.component.css'],
})
export class GalleryViewComponent implements OnChanges {
  @Input() gallery!: Gallery;

  photos: PhotoListItemDto[] = [];

  constructor(private photoApi: PhotoApiService) {}

  ngOnChanges(changes: SimpleChanges) {
    if (changes['gallery'] && this.gallery?.id) {
      this.loadPhotos();
    }
  }

  private loadPhotos() {
    this.photoApi.getByGallery(this.gallery.id).subscribe((photos) => {
      this.photos = photos.slice().reverse();
    });
  }

  getThumbnailUrl(photoId: string): string {
    return `/api/Photo/${photoId}/thumbnail`;
  }

  getTileClass(index: number): string {
    const i = index + 1;

    if (index % 11 === 0) return 'big';
    if (index % 7 === 0) return 'wide';
    if (index % 5 === 0) return 'large';
    return '';
  }
}
