import { Component, EventEmitter, HostListener, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ConfirmModalComponent } from '../../shared/modal/confirm-modal/confirm-modal.component';

@Component({
  selector: 'app-create-gallery',
  standalone: true,
  imports: [CommonModule, FormsModule, ConfirmModalComponent],
  templateUrl: './create-gallery.component.html',
  styleUrls: ['./create-gallery.component.css'],
})
export class CreateGalleryComponent {
  title = '';
  confirmOpen = false;

  @Output() create = new EventEmitter<string>();
  @Output() cancel = new EventEmitter<void>();

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
