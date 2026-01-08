import { Component, EventEmitter, OnInit, Output } from '@angular/core';
import { GalleryApiService } from '../../services/gallery-api.service';
import { Gallery } from '../../models/gallery.model';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-gallery-list',
  styleUrls: ['./gallery-list.component.css'],
  standalone: true,
  imports: [CommonModule],
  templateUrl: './gallery-list.component.html',
})
export class GalleryListComponent implements OnInit {
  galleries: Gallery[] = [];

  @Output()
  gallerySelected = new EventEmitter<Gallery>();

  constructor(private galleryApi: GalleryApiService) {}

  ngOnInit(): void {
    this.galleryApi.getAll().subscribe({
      next: (g) => (this.galleries = g),
      error: (e) => console.error(e),
    });
  }

  onDelete(g: Gallery, event: MouseEvent) {
    event.stopPropagation(); // 🔑 ключавы радок
    console.log('DELETE CLICKED', g);
  }

  selectedGalleryId?: string;

  select(g: Gallery) {
    this.selectedGalleryId = g.id;
    this.gallerySelected.emit(g);
  }
}
