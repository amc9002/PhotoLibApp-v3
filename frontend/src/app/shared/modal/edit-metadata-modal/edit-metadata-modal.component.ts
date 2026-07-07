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
  @Input() saveError: string | null = null;

  @Output() close = new EventEmitter<void>();
  @Output() save = new EventEmitter<{
    title: string;
    description: string;
    tags: string[];
  }>();

  tagsText = '';
  availableTags: string[] = [];

  private initialTitle = '';
  private initialDescription = '';
  private initialTags: string[] = [];

  constructor(private tagApi: TagApiService) {}

  ngOnInit() {
    this.tagsText = this.tags.join(', ');
    this.initialTitle = this.title;
    this.initialDescription = this.description;
    this.initialTags = [...this.tags];

    this.tagApi.getAll().subscribe({
      next: (tags) => (this.availableTags = tags.map((t) => t.name)),
      error: () => {},
    });
  }

  get isDirty(): boolean {
    return (
      this.title !== this.initialTitle ||
      this.description !== this.initialDescription ||
      !this.sameTags(parseTagsInput(this.tagsText), this.initialTags)
    );
  }

  private sameTags(a: string[], b: string[]): boolean {
    if (a.length !== b.length) return false;
    const sortedA = [...a].sort();
    const sortedB = [...b].sort();
    return sortedA.every((tag, i) => tag === sortedB[i]);
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
