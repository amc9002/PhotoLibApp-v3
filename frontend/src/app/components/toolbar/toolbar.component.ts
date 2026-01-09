import {
  Component,
  Input,
  Output,
  EventEmitter,
  ElementRef,
  HostListener,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { Gallery } from '../../models/gallery.model';

@Component({
  selector: 'app-toolbar',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './toolbar.component.html',
  styleUrls: ['./toolbar.component.css'],
})
export class ToolbarComponent {
  @Input() galleries: Gallery[] = [];
  @Input() selectedGallery?: Gallery;

  @Input() layout: 'left' | 'right' = 'left';
  @Output() gallerySelected = new EventEmitter<Gallery>();
  @Output() properties = new EventEmitter<void>();

  constructor(private elementRef: ElementRef) {}

  @HostListener('document:click', ['$event'])
  onDocumentClick(event: MouseEvent) {
    const clickedInside = this.elementRef.nativeElement.contains(event.target);

    if (!clickedInside) {
      this.serviceMenuOpen = false;
      this.dropdownOpen = false;
    }
  }

  dropdownOpen = false;

  toggleDropdown() {
    this.dropdownOpen = !this.dropdownOpen;
  }

  select(gallery: Gallery) {
    this.gallerySelected.emit(gallery);
    this.dropdownOpen = false;
  }

  serviceMenuOpen = false;

  toggleServiceMenu() {
    this.serviceMenuOpen = !this.serviceMenuOpen;
  }

  closeServiceMenu() {
    this.serviceMenuOpen = false;
  }
}
