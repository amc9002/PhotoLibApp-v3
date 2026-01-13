import { Component, EventEmitter, Output } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-photo-actions',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './photo-actions.component.html',
  styleUrls: ['./photo-actions.component.css'],
})
export class PhotoActionsComponent {
  @Output() actionClick = new EventEmitter<void>();
  @Output() editMetadata = new EventEmitter<void>();

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
}
