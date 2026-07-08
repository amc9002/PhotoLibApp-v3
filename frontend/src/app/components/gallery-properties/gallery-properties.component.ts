import {
  Component,
  Input,
  Output,
  EventEmitter,
  HostListener,
  NgZone,
  OnDestroy,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { Gallery } from '../../models/gallery.model';
import { TranslatePipe } from '../../core/i18n/translate.pipe';

@Component({
  selector: 'app-gallery-properties',
  standalone: true,
  imports: [CommonModule, TranslatePipe],
  templateUrl: './gallery-properties.component.html',
  styleUrls: ['./gallery-properties.component.css'],
})
export class GalleryPropertiesComponent implements OnDestroy {
  @Input() gallery!: Gallery;
  @Output() close = new EventEmitter<void>();

  x = 100;
  y = 100;

  private dragging = false;
  private offsetX = 0;
  private offsetY = 0;

  // Attached outside Angular's zone - without this, every mousemove over
  // the whole page triggers a full change-detection pass just so this
  // panel can ignore it while not being dragged.
  private readonly onMouseMove = (event: MouseEvent) => {
    if (!this.dragging) return;
    this.zone.run(() => {
      this.x = event.clientX - this.offsetX;
      this.y = event.clientY - this.offsetY;
    });
  };

  constructor(private zone: NgZone) {
    this.zone.runOutsideAngular(() => {
      document.addEventListener('mousemove', this.onMouseMove);
    });
  }

  ngOnDestroy() {
    document.removeEventListener('mousemove', this.onMouseMove);
  }

  startDrag(event: MouseEvent) {
    this.dragging = true;
    this.offsetX = event.clientX - this.x;
    this.offsetY = event.clientY - this.y;
    event.preventDefault();
  }

  @HostListener('document:mouseup')
  stopDrag() {
    this.dragging = false;
  }
}
