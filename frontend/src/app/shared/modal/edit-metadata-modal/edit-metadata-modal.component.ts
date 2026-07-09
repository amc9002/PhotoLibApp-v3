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
import { PhotoApiService } from '../../../services/photo-api.service';
import { parseTagsInput } from '../../utils/tags';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { DescriptionLength, DescriptionStyle } from '../../../models/generate-description.dto';

/**
 * Title + description + tags editor, shared by the photo and gallery
 * "edit" flows (only the heading and the emitted context differ).
 */
@Component({
  selector: 'app-edit-metadata-modal',
  standalone: true,
  imports: [CommonModule, FormsModule, ButtonGroupNavDirective, TranslatePipe],
  templateUrl: './edit-metadata-modal.component.html',
  styleUrls: ['./edit-metadata-modal.component.css'],
})
export class EditMetadataModalComponent implements OnInit {
  @Input() heading = 'editMetadata.heading';
  @Input() title = '';
  @Input() description = '';
  @Input() tags: string[] = [];
  @Input() isSaving = false;
  @Input() saveError: string | null = null;
  /** Only set when editing a photo - gates the "Generate with AI" section, which doesn't apply to galleries. */
  @Input() photoId?: string;

  @Output() close = new EventEmitter<void>();
  @Output() save = new EventEmitter<{
    title: string;
    description: string;
    tags: string[];
  }>();

  tagsText = '';
  availableTags: string[] = [];

  aiStyle: DescriptionStyle = 'informative';
  aiLength: DescriptionLength = 'medium';
  isGenerating = false;
  generateError = false;

  private initialTitle = '';
  private initialDescription = '';
  private initialTags: string[] = [];

  constructor(
    private tagApi: TagApiService,
    private photoApi: PhotoApiService,
  ) {}

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

  trackByTag(index: number, tag: string): string {
    return tag;
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

  @HostListener('keydown.escape', ['$event'])
  onEscape(event: KeyboardEvent) {
    // Stop the key from bubbling to any window/document-level shortcut
    // handler (e.g. the photo viewer's Escape-to-close) - only this modal
    // should react to it.
    event.stopPropagation();
    this.close.emit();
  }

  onSave() {
    this.save.emit({
      title: this.title,
      description: this.description,
      tags: parseTagsInput(this.tagsText),
    });
  }

  generateWithAi() {
    if (!this.photoId || this.isGenerating) return;

    this.isGenerating = true;
    this.generateError = false;

    this.photoApi.generateDescription(this.photoId, this.aiStyle, this.aiLength).subscribe({
      next: (draft) => {
        this.title = draft.title;
        this.description = draft.description;
        const existing = parseTagsInput(this.tagsText);
        const merged = [...existing, ...draft.suggestedTags].filter(
          (tag, i, arr) => arr.findIndex((t) => t.toLowerCase() === tag.toLowerCase()) === i,
        );
        this.tagsText = merged.join(', ');
        this.isGenerating = false;
      },
      error: (err) => {
        console.error('Failed to generate photo description', err);
        this.isGenerating = false;
        this.generateError = true;
      },
    });
  }
}
