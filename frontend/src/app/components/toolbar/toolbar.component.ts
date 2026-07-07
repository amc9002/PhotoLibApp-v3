import {
  Component,
  Input,
  Output,
  EventEmitter,
  ElementRef,
  HostListener,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { CdkDragDrop, DragDropModule, moveItemInArray } from '@angular/cdk/drag-drop';
import { Gallery } from '../../models/gallery.model';
import { ThumbnailSizeService } from '../../services/thumbnail-size.service';
import { PhotoSelectionService } from '../../services/photo-selection.service';

@Component({
  selector: 'app-toolbar',
  standalone: true,
  imports: [CommonModule, DragDropModule],
  templateUrl: './toolbar.component.html',
  styleUrls: ['./toolbar.component.css'],
})
export class ToolbarComponent {
  @Input() galleries: Gallery[] = [];
  @Input() selectedGallery?: Gallery;

  @Input() layout: 'left' | 'right' = 'left';
  @Output() gallerySelected = new EventEmitter<Gallery>();
  @Output() galleriesReordered = new EventEmitter<Gallery[]>();
  @Output() addPhotos = new EventEmitter<void>();
  @Output() addFromInternet = new EventEmitter<void>();
  @Output() properties = new EventEmitter<void>();
  @Output() newGallery = new EventEmitter<void>();
  @Output() deleteGallery = new EventEmitter<void>();
  @Output() editGallery = new EventEmitter<void>();
  @Output() copySelected = new EventEmitter<void>();
  @Output() moveSelected = new EventEmitter<void>();
  @Output() deleteSelected = new EventEmitter<void>();

  constructor(
    private elementRef: ElementRef,
    public thumbnailSize: ThumbnailSizeService,
    public photoSelection: PhotoSelectionService,
  ) {}

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

  onGalleryDropped(event: CdkDragDrop<Gallery[]>) {
    if (event.previousIndex === event.currentIndex) return;

    moveItemInArray(this.galleries, event.previousIndex, event.currentIndex);
    this.galleriesReordered.emit(this.galleries);
  }

  onAddPhotos() {
    this.closeServiceMenu();
    this.addPhotos.emit();
  }

  onAddFromInternet() {
    this.closeServiceMenu();
    if (!this.selectedGallery) return;
    this.addFromInternet.emit();
  }

  onNewGallery() {
    this.closeServiceMenu();
    this.newGallery.emit();
  }

  serviceMenuOpen = false;

  toggleServiceMenu() {
    this.serviceMenuOpen = !this.serviceMenuOpen;
  }

  closeServiceMenu() {
    this.serviceMenuOpen = false;
  }

  onDeleteGallery() {
    this.closeServiceMenu();
    if (!this.selectedGallery) return;
    this.deleteGallery.emit();
  }

  onEditGallery() {
    this.closeServiceMenu();
    if (!this.selectedGallery) return;
    this.editGallery.emit();
  }

  onCopySelected() {
    this.closeServiceMenu();
    if (this.photoSelection.count === 0) return;
    this.copySelected.emit();
  }

  onMoveSelected() {
    this.closeServiceMenu();
    if (this.photoSelection.count === 0) return;
    this.moveSelected.emit();
  }

  onDeleteSelected() {
    this.closeServiceMenu();
    if (this.photoSelection.count === 0) return;
    this.deleteSelected.emit();
  }
}
