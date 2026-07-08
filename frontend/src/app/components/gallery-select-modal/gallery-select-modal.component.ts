import { Component, EventEmitter, HostListener, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Gallery } from '../../models/gallery.model';

@Component({
  selector: 'app-gallery-select-modal',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './gallery-select-modal.component.html',
  styleUrls: ['./gallery-select-modal.component.css'],
})
export class GallerySelectModalComponent {
  @Input() mode: 'copy' | 'move' = 'copy';
  @Input() galleries: Gallery[] = [];
  @Input() count = 1;
  @Input() isBusy = false;

  @Output() selectExisting = new EventEmitter<string>();
  @Output() createNew = new EventEmitter<string>();
  @Output() cancel = new EventEmitter<void>();

  newTitle = '';

  @HostListener('keydown.escape', ['$event'])
  onEscape(event: KeyboardEvent) {
    // This modal is rendered as a sibling of app-photo-viewer, not nested
    // inside it - without stopping propagation here, Escape would also
    // bubble to the viewer's window:keydown handler and close it too.
    event.stopPropagation();
    this.cancel.emit();
  }

  get title() {
    const action = this.mode === 'copy' ? 'Copy' : 'Move';
    const subject = this.count > 1 ? `${this.count} photos` : 'photo';
    return `${action} ${subject} to gallery`;
  }

  get actionLabel() {
    return this.mode === 'copy' ? 'Copy' : 'Move';
  }

  pick(galleryId: string) {
    if (this.isBusy) return;
    this.selectExisting.emit(galleryId);
  }

  submitNew() {
    const value = this.newTitle.trim();
    if (!value || this.isBusy) return;
    this.createNew.emit(value);
  }
}
