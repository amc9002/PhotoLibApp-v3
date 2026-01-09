import { Component, OnInit } from '@angular/core';
import { Gallery } from './models/gallery.model';
import { GalleryApiService } from './services/gallery-api.service';
import { CommonModule } from '@angular/common';
import { ToolbarComponent } from './components/toolbar/toolbar.component';
import { GalleryPropertiesComponent } from './components/gallery-properties/gallery-properties.component';
import { GalleryViewComponent } from './components/gallery-view/gallery-view.component';
import { CreateGalleryComponent } from './components/create-gallery/create-gallery.component';

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

  constructor(private galleryApi: GalleryApiService) {}

  ngOnInit(): void {
    this.galleryApi.getAll().subscribe({
      next: (g) => (this.galleries = g),
      error: (e) => console.error(e),
    });
  }

  selectGallery(gallery: Gallery) {
    this.selectedGallery = gallery;
  }

  showGalleryProperties = false;

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
    console.log('CREATE GALLERY:', title);
    this.showCreateGallery = false;
  }
}
