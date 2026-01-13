import {
  Component,
  EventEmitter,
  Input,
  OnChanges,
  Output,
  SimpleChanges,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { Gallery } from '../../models/gallery.model';
import { PhotoListItemDto } from '../../models/photoLisrItem.dto';
import { PhotoApiService } from '../../services/photo-api.service';
import { GalleryGridComponent } from './gallery-grid/gallery-grid.component';
import { PhotoViewerComponent } from '../photo-viewer/photo-viewer.component';

@Component({
  selector: 'app-gallery-view',
  standalone: true,
  imports: [CommonModule, GalleryGridComponent, PhotoViewerComponent],
  templateUrl: './gallery-view.component.html',
  styleUrls: ['./gallery-view.component.css'],
})
export class GalleryViewComponent implements OnChanges {
  @Input() gallery!: Gallery;
  @Output() viewerOpened = new EventEmitter<void>();
  @Output() viewerClosed = new EventEmitter<void>();

  photos: PhotoListItemDto[] = [];
  viewerOpen = false;
  viewerPhotoId?: string;
  activePhotoId: string | null = null;

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

  openViewer(photoId: string) {
    this.activePhotoId = photoId;
    this.viewerPhotoId = this.activePhotoId;
    this.viewerOpen = true;
    this.viewerOpened.emit();
  }

  closeViewer() {
    this.viewerOpen = false;
    this.viewerPhotoId = undefined;
    this.viewerClosed.emit();
  }
}
