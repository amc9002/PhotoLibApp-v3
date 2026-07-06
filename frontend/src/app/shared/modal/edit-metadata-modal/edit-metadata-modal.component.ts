import {
  Component,
  EventEmitter,
  HostListener,
  Input,
  OnInit,
  Output,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ButtonGroupNavDirective } from '../../directives/button-group-nav.directive';
import { TagApiService } from '../../../services/tag-api.service';
import { parseTagsInput } from '../../utils/tags';

/**
 * Title + description + tags editor, shared by the photo and gallery
 * "edit" flows (only the heading and the emitted context differ).
 */
@Component({
  selector: 'app-edit-metadata-modal',
  standalone: true,
  imports: [CommonModule, FormsModule, ButtonGroupNavDirective],
  templateUrl: './edit-metadata-modal.component.html',
  styleUrls: ['./edit-metadata-modal.component.css'],
})
export class EditMetadataModalComponent implements OnInit {
  @Input() heading = 'Edit details';
  @Input() title = '';
  @Input() description = '';
  @Input() tags: string[] = [];
  @Input() isSaving = false;

  @Output() close = new EventEmitter<void>();
  @Output() save = new EventEmitter<{
    title: string;
    description: string;
    tags: string[];
  }>();

  tagsText = '';
  availableTags: string[] = [];

  constructor(private tagApi: TagApiService) {}

  ngOnInit() {
    this.tagsText = this.tags.join(', ');
    this.tagApi.getAll().subscribe({
      next: (tags) => (this.availableTags = tags.map((t) => t.name)),
      error: () => {},
    });
  }

  @HostListener('keydown.escape')
  onEscape() {
    this.close.emit();
  }

  onSave() {
    this.save.emit({
      title: this.title,
      description: this.description,
      tags: parseTagsInput(this.tagsText),
    });
  }
}
