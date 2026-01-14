import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-edit-photo-metadata-modal',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './edit-photo-metadata-modal.component.html',
  styleUrls: ['./edit-photo-metadata-modal.component.css'],
})
export class EditPhotoMetadataModalComponent {
  @Input() title = '';
  @Input() description = '';

  @Output() close = new EventEmitter<void>();
  @Output() save = new EventEmitter<{
    title: string;
    description: string;
  }>();

  debugSave() {
    console.log('MODAL: save clicked');
    this.save.emit({ title: this.title, description: this.description });
  }
}
