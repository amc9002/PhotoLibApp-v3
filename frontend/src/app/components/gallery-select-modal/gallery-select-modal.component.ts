import { Component, EventEmitter, HostListener, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Gallery } from '../../models/gallery.model';
import { I18nService } from '../../core/i18n/i18n.service';
import { TranslatePipe } from '../../core/i18n/translate.pipe';
import { copyMoveToGalleryTitle } from '../../core/i18n/plurals';

@Component({
  selector: 'app-gallery-select-modal',
  standalone: true,
  imports: [CommonModule, FormsModule, TranslatePipe],
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

  constructor(private i18n: I18nService) {}

  @HostListener('keydown.escape', ['$event'])
  onEscape(event: KeyboardEvent) {
    // This modal is rendered as a sibling of app-photo-viewer, not nested
    // inside it - without stopping propagation here, Escape would also
    // bubble to the viewer's window:keydown handler and close it too.
    event.stopPropagation();
    this.cancel.emit();
  }

  get title() {
    return copyMoveToGalleryTitle(this.mode, this.count, this.i18n.lang);
  }

  get actionLabel() {
    return this.i18n.translate(this.mode === 'copy' ? 'gallerySelect.copy' : 'gallerySelect.move');
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
