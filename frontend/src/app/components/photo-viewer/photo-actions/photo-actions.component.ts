import { Component, EventEmitter, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';

@Component({
  selector: 'app-photo-actions',
  standalone: true,
  imports: [CommonModule, TranslatePipe],
  templateUrl: './photo-actions.component.html',
  styleUrls: ['./photo-actions.component.css'],
})
export class PhotoActionsComponent {
  @Output() actionClick = new EventEmitter<void>();
  @Output() editMetadata = new EventEmitter<void>();
  @Output() deletePhoto = new EventEmitter<void>();
  @Output() copyPhoto = new EventEmitter<void>();
  @Output() movePhoto = new EventEmitter<void>();
  @Output() showInfo = new EventEmitter<void>();

  menuOpen = false;

  onActionClick() {
    this.actionClick.emit();
  }

  toggleMenu() {
    this.menuOpen = !this.menuOpen;
  }

  onEditMetadataClick() {
    this.menuOpen = false;
    this.editMetadata.emit();
  }

  onShowInfoClick() {
    this.menuOpen = false;
    this.showInfo.emit();
  }

  onCopyClick() {
    this.menuOpen = false;
    this.copyPhoto.emit();
  }

  onMoveClick() {
    this.menuOpen = false;
    this.movePhoto.emit();
  }

  onDeleteClick() {
    this.menuOpen = false;
    this.deletePhoto.emit();
  }
}
