import { Component, ElementRef, OnInit, ViewChild } from '@angular/core';
import { Gallery } from './models/gallery.model';
import { GalleryApiService } from './services/gallery-api.service';
import { CommonModule } from '@angular/common';
import { ToolbarComponent } from './components/toolbar/toolbar.component';
import { GalleryPropertiesComponent } from './components/gallery-properties/gallery-properties.component';
import { GalleryViewComponent } from './components/gallery-view/gallery-view.component';
import { CreateGalleryComponent } from './components/create-gallery/create-gallery.component';
import { PhotoApiService } from './services/photo-api.service';
import { PhotoListItemDto } from './models/photoLisrItem.dto';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [
    CommonModule,
    ToolbarComponent,
    GalleryPropertiesComponent,
    GalleryViewComponent,
    CreateGalleryComponent,
  ],
  templateUrl: './app.component.html',
  styleUrls: ['./app.component.css'],
})
export class AppComponent implements OnInit {
  galleries: Gallery[] = [];
  selectedGallery?: Gallery;
  isViewerOpen = false;
  viewerPhotoId?: string;
  photos: PhotoListItemDto[] = [];

  @ViewChild('fileInput') fileInput!: ElementRef<HTMLInputElement>;

  constructor(
    private galleryApi: GalleryApiService,
    private photoApi: PhotoApiService,
  ) {}

  ngOnInit(): void {
    this.galleryApi.getAll().subscribe({
      next: (g) => (this.galleries = g),
      error: (e) => console.error(e),
    });
  }

  selectGallery(gallery: Gallery) {
    // прымусова "змяняем" значэнне
    this.selectedGallery = undefined;

    setTimeout(() => {
      this.selectedGallery = gallery;
    });
  }

  showGalleryProperties = false;

  openFilePicker() {
    if (!this.selectedGallery) return;
    this.fileInput.nativeElement.click();
  }

  onFilesSelected(event: Event) {
    const input = event.target as HTMLInputElement;
    if (!input.files || !this.selectedGallery) return;

    const files = Array.from(input.files);

    files.forEach((file) => {
      this.photoApi
        .create({
          galleryId: this.selectedGallery!.id,
          title: file.name,
        })
        .subscribe((photo) => {
          console.log('Photo metadata created:', photo);

          this.photoApi.upload(photo.id, file).subscribe(() => {
            console.log('File uploaded for photo:', photo.id);

            // абнаўляем галерэю
            this.selectGallery(this.selectedGallery!);
          });
        });
    });

    input.value = '';
  }

  openGalleryProperties() {
    this.showGalleryProperties = true;
  }

  closeGalleryProperties() {
    this.showGalleryProperties = false;
  }

  showCreateGallery = false;

  openCreateGallery() {
    this.showCreateGallery = true;
  }

  closeCreateGallery() {
    this.showCreateGallery = false;
  }

  createGallery(title: string) {
    this.galleryApi.create({ title }).subscribe({
      next: (gallery) => {
        this.galleries = [...this.galleries, gallery];
        this.selectedGallery = gallery;
        this.showCreateGallery = false;
      },
      error: (err) => {
        console.error('create gallery error:', err);
      },
    });
  }
}
