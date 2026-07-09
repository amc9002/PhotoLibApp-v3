import { Component, EventEmitter, HostListener, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ConfirmModalComponent } from '../../shared/modal/confirm-modal/confirm-modal.component';
import { TranslatePipe } from '../../core/i18n/translate.pipe';
import { I18nService } from '../../core/i18n/i18n.service';

@Component({
  selector: 'app-create-gallery',
  standalone: true,
  imports: [CommonModule, FormsModule, ConfirmModalComponent, TranslatePipe],
  templateUrl: './create-gallery.component.html',
  styleUrls: ['./create-gallery.component.css'],
})
export class CreateGalleryComponent {
  title = '';
  confirmOpen = false;

  @Output() create = new EventEmitter<string>();
  @Output() cancel = new EventEmitter<void>();

  constructor(private i18n: I18nService) {}

  get confirmMessage(): string {
    return (
      this.i18n.translate('createGallery.confirmMessagePrefix') +
      this.title.trim() +
      this.i18n.translate('createGallery.confirmMessageSuffix')
    );
  }

  @HostListener('keydown.escape')
  onEscape() {
    if (this.confirmOpen) return; // let the confirm dialog handle its own Escape
    this.cancel.emit();
  }

  requestCreate() {
    const value = this.title.trim();
    if (!value) return;

    this.confirmOpen = true;
  }

  confirmCreate() {
    this.confirmOpen = false;
    this.create.emit(this.title.trim());
  }

  cancelConfirm() {
    this.confirmOpen = false;
  }
}
