import {
  Component,
  EventEmitter,
  HostListener,
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
import { PhotoSelectionService } from '../../services/photo-selection.service';

@Component({
  selector: 'app-gallery-view',
  standalone: true,
  imports: [CommonModule, GalleryGridComponent, PhotoViewerComponent],
  templateUrl: './gallery-view.component.html',
  styleUrls: ['./gallery-view.component.css'],
})
export class GalleryViewComponent implements OnChanges {
  @Input() gallery!: Gallery;
  @Output() photoSelected = new EventEmitter<string>();
  @Output() photosLoaded = new EventEmitter<PhotoListItemDto[]>();

  photos: PhotoListItemDto[] = [];

  constructor(
    private photoApi: PhotoApiService,
    private photoSelection: PhotoSelectionService,
  ) {}

  ngOnChanges(changes: SimpleChanges) {
    if (changes['gallery'] && this.gallery?.id) {
      this.photoSelection.clear();
      this.loadPhotos();
    }
  }

  // Photos can be added from outside the app (the "Add from internet"
  // bookmarklet posts straight to the API from a page/popup we don't
  // control), so refresh whenever the tab becomes visible again rather
  // than requiring a manual gallery switch to notice new photos.
  @HostListener('document:visibilitychange')
  onVisibilityChange() {
    if (document.visibilityState === 'visible' && this.gallery?.id) {
      this.loadPhotos();
    }
  }

  private loadPhotos() {
    this.photoApi.getByGallery(this.gallery.id).subscribe((photos) => {
      this.photos = photos.slice().reverse();
      this.photosLoaded.emit(this.photos);
    });
  }

  onPhotoClicked(photoId: string) {
    this.photoSelected.emit(photoId);
  }

  removePhoto(photoId: string) {
    this.photos = this.photos.filter((p) => p.id !== photoId);
    this.photosLoaded.emit(this.photos);
  }
}
