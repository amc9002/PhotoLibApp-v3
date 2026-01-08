import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Gallery } from './models/gallery.model';
import { GalleryListComponent } from './components/gallery-list/gallery-list.component';
@Component({
  selector: 'app-root',
  imports: [GalleryListComponent, CommonModule],
  templateUrl: './app.component.html',
  styleUrl: './app.component.css',
})
export class AppComponent {
  title = 'frontend';

  selectedGallery?: Gallery;

  onGallerySelected(gallery: Gallery) {
    this.selectedGallery = gallery;
  }
}
