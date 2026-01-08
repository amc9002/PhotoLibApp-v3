import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { GalleryApiService } from '../../../services/gallery-api.service';
import { GalleryDto } from '../../../models/gallery.dto';

@Component({
  standalone: true,
  selector: 'app-gallery-test-page',
  imports: [CommonModule],
  template: `
    <h2>Galleries</h2>

    <button (click)="create()">+ Gallery</button>

    <ul>
      <li *ngFor="let g of galleries">
        <strong>{{ g.title }}</strong>
        <span>({{ g.photoCount ?? 0 }} photos)</span>
        <button (click)="remove(g.id)">✖</button>
      </li>
    </ul>
  `,
})
export class GalleryTestPageComponent {
  galleries: any[] = [];
  private i = 1;

  constructor(private api: GalleryApiService) {
    this.api.getAll().subscribe((g) => (this.galleries = g));
  }

  create() {
    this.api
      .create({ title: 'Gallery ' + this.i++ })
      .subscribe(() =>
        this.api.getAll().subscribe((g) => (this.galleries = g)),
      );
  }

  remove(id: string) {
    this.api
      .delete(id)
      .subscribe(() =>
        this.api.getAll().subscribe((g) => (this.galleries = g)),
      );
  }
}
