import { Component, EventEmitter, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

@Component({
  selector: 'app-create-gallery',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './create-gallery.component.html',
  styleUrls: ['./create-gallery.component.css'],
})
export class CreateGalleryComponent {
  title = '';

  @Output() create = new EventEmitter<string>();
  @Output() cancel = new EventEmitter<void>();

  submit() {
    const value = this.title.trim();
    if (!value) return;

    this.create.emit(value);
  }
}
