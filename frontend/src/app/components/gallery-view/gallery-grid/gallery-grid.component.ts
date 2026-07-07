import {
  Component,
  ElementRef,
  EventEmitter,
  HostListener,
  Input,
  Output,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { CdkDragDrop, DragDropModule, moveItemInArray } from '@angular/cdk/drag-drop';
import { PhotoListItemDto } from '../../../models/photoLisrItem.dto';
import { PhotoThumbnailComponent } from '../../../shared/ui/photo-thumbnail/photo-thumbnail.component';
import { ThumbnailSizeService } from '../../../services/thumbnail-size.service';
import { PhotoSelectionService } from '../../../services/photo-selection.service';

const MIN_DRAG_DISTANCE = 4;

@Component({
  selector: 'app-gallery-grid',
  standalone: true,
  imports: [CommonModule, DragDropModule, PhotoThumbnailComponent],
  templateUrl: './gallery-grid.component.html',
  styleUrls: ['./gallery-grid.component.css'],
})
export class GalleryGridComponent {
  @Input({ required: true }) photos!: PhotoListItemDto[];
  @Output() photoClicked = new EventEmitter<string>();
  @Output() photosReordered = new EventEmitter<PhotoListItemDto[]>();
  @Input() activePhotoId: string | null = null;

  dragging = false;
  // All drag coordinates are kept in viewport (client) space; only
  // `marqueeStyle` converts to host-relative for CSS positioning.
  private dragStartX = 0;
  private dragStartY = 0;
  private dragCurrentX = 0;
  private dragCurrentY = 0;

  constructor(
    public thumbnailSize: ThumbnailSizeService,
    public selection: PhotoSelectionService,
    private host: ElementRef<HTMLElement>,
  ) {}

  onPhotoClick(event: MouseEvent, photoId: string) {
    if (event.ctrlKey || event.metaKey) {
      this.selection.toggle(photoId);
      return;
    }

    if (event.shiftKey) {
      this.selection.selectRange(this.photos, photoId);
      return;
    }

    if (this.selection.count > 0) {
      this.selection.toggle(photoId);
      return;
    }

    this.photoClicked.emit(photoId);
  }

  trackById(index: number, photo: PhotoListItemDto): string {
    return photo.id;
  }

  onPhotoDropped(event: CdkDragDrop<PhotoListItemDto[]>) {
    if (event.previousIndex === event.currentIndex) return;

    moveItemInArray(this.photos, event.previousIndex, event.currentIndex);
    this.photosReordered.emit(this.photos);
  }

  onGridMouseDown(event: MouseEvent) {
    if (event.button !== 0) return;
    if ((event.target as HTMLElement).closest('.photo-item')) return;

    event.preventDefault();

    this.dragStartX = this.dragCurrentX = event.clientX;
    this.dragStartY = this.dragCurrentY = event.clientY;
    this.dragging = true;

    if (!event.ctrlKey && !event.metaKey && !event.shiftKey) {
      this.selection.clear();
    }
  }

  @HostListener('document:mousemove', ['$event'])
  onDocumentMouseMove(event: MouseEvent) {
    if (!this.dragging) return;
    this.dragCurrentX = event.clientX;
    this.dragCurrentY = event.clientY;
  }

  @HostListener('document:mouseup')
  onDocumentMouseUp() {
    if (!this.dragging) return;
    this.dragging = false;
    this.applyMarqueeSelection();
  }

  get marqueeStyle() {
    const hostRect = this.host.nativeElement.getBoundingClientRect();
    return {
      left: `${Math.min(this.dragStartX, this.dragCurrentX) - hostRect.left}px`,
      top: `${Math.min(this.dragStartY, this.dragCurrentY) - hostRect.top}px`,
      width: `${Math.abs(this.dragCurrentX - this.dragStartX)}px`,
      height: `${Math.abs(this.dragCurrentY - this.dragStartY)}px`,
    };
  }

  private applyMarqueeSelection() {
    const left = Math.min(this.dragStartX, this.dragCurrentX);
    const top = Math.min(this.dragStartY, this.dragCurrentY);
    const right = Math.max(this.dragStartX, this.dragCurrentX);
    const bottom = Math.max(this.dragStartY, this.dragCurrentY);

    if (right - left < MIN_DRAG_DISTANCE && bottom - top < MIN_DRAG_DISTANCE) {
      return;
    }

    const hitIds: string[] = [];
    this.host.nativeElement
      .querySelectorAll<HTMLElement>('.photo-item')
      .forEach((el) => {
        const r = el.getBoundingClientRect();
        const intersects =
          r.left < right && r.right > left && r.top < bottom && r.bottom > top;
        if (intersects) {
          const id = el.getAttribute('data-photo-id');
          if (id) hitIds.push(id);
        }
      });

    if (hitIds.length) this.selection.selectMany(hitIds);
  }
}
