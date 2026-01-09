import {
  Component,
  Input,
  Output,
  EventEmitter,
  HostListener,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { Gallery } from '../../models/gallery.model';

@Component({
  selector: 'app-gallery-properties',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './gallery-properties.component.html',
  styleUrls: ['./gallery-properties.component.css'],
})
export class GalleryPropertiesComponent {
  @Input() gallery!: Gallery;
  @Output() close = new EventEmitter<void>();

  x = 100;
  y = 100;

  private dragging = false;
  private offsetX = 0;
  private offsetY = 0;

  startDrag(event: MouseEvent) {
    this.dragging = true;
    this.offsetX = event.clientX - this.x;
    this.offsetY = event.clientY - this.y;
    event.preventDefault();
  }

  @HostListener('document:mousemove', ['$event'])
  onMouseMove(event: MouseEvent) {
    if (!this.dragging) return;
    this.x = event.clientX - this.offsetX;
    this.y = event.clientY - this.offsetY;
  }

  @HostListener('document:mouseup')
  stopDrag() {
    this.dragging = false;
  }
}
