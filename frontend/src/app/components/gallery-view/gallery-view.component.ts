import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Gallery } from '../../models/gallery.model';

@Component({
  selector: 'app-gallery-view',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './gallery-view.component.html',
  styleUrls: ['./gallery-view.component.css'],
})
export class GalleryViewComponent {
  @Input() gallery!: Gallery;

  // Часовыя плэйсхолдары
  placeholders = Array.from({ length: 18 }).map((_, i) => ({
    variant: i % 7 === 0 ? 'large' : i % 5 === 0 ? 'wide' : 'normal',
  }));
}
