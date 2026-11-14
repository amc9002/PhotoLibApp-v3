import {
  Component,
  ElementRef,
  EventEmitter,
  HostListener,
  Input,
  OnDestroy,
  OnInit,
  Output,
  ViewChild,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';

/** Side (in CSS px) of the square selection frame shown to the user. */
const FRAME_SIZE = 280;
/** Side (in px) of the square JPEG exported on save. */
const OUTPUT_SIZE = 512;
const MIN_ZOOM = 1;
const MAX_ZOOM = 3;
const WHEEL_ZOOM_STEP = 0.08;

/**
 * Lets the user pan and zoom a just-selected image within a square frame
 * before it's uploaded as an avatar, instead of relying on the server's
 * blind center-crop. Exports the visible square region to a JPEG blob on
 * save; the server still center-crops/resizes whatever it receives, which
 * is a no-op on an already-square image.
 */
@Component({
  selector: 'app-avatar-crop-modal',
  standalone: true,
  imports: [CommonModule, TranslatePipe],
  templateUrl: './avatar-crop-modal.component.html',
  styleUrls: ['./avatar-crop-modal.component.css'],
})
export class AvatarCropModalComponent implements OnInit, OnDestroy {
  @Input({ required: true }) file!: File;

  @Output() save = new EventEmitter<Blob>();
  @Output() cancel = new EventEmitter<void>();

  @ViewChild('imgEl') private imgRef?: ElementRef<HTMLImageElement>;

  readonly frameSize = FRAME_SIZE;
  readonly minZoom = MIN_ZOOM;
  readonly maxZoom = MAX_ZOOM;

  imageUrl = '';
  imageReady = false;
  isSaving = false;
  zoom = MIN_ZOOM;
  offsetX = 0;
  offsetY = 0;
  displayWidth = 0;
  displayHeight = 0;

  private naturalWidth = 0;
  private naturalHeight = 0;
  /** Scale at which the image's smaller natural dimension exactly fills the frame. */
  private baseScale = 1;
  private dragging = false;
  private dragStartX = 0;
  private dragStartY = 0;
  private dragOriginX = 0;
  private dragOriginY = 0;

  ngOnInit(): void {
    this.imageUrl = URL.createObjectURL(this.file);
  }

  ngOnDestroy(): void {
    if (this.imageUrl) URL.revokeObjectURL(this.imageUrl);
  }

  onImageLoad(): void {
    const img = this.imgRef!.nativeElement;
    this.naturalWidth = img.naturalWidth;
    this.naturalHeight = img.naturalHeight;
    this.baseScale = FRAME_SIZE / Math.min(this.naturalWidth, this.naturalHeight);
    this.applyZoom(MIN_ZOOM, true);
    this.imageReady = true;
  }

  onZoomInput(event: Event): void {
    const value = Number((event.target as HTMLInputElement).value);
    this.applyZoom(value, false);
  }

  onWheel(event: WheelEvent): void {
    if (!this.imageReady) return;
    event.preventDefault();
    const delta = event.deltaY > 0 ? -WHEEL_ZOOM_STEP : WHEEL_ZOOM_STEP;
    this.applyZoom(this.zoom + delta, false);
  }

  onPointerDown(event: PointerEvent): void {
    if (!this.imageReady) return;
    event.preventDefault();
    this.dragging = true;
    this.dragStartX = event.clientX;
    this.dragStartY = event.clientY;
    this.dragOriginX = this.offsetX;
    this.dragOriginY = this.offsetY;
  }

  @HostListener('document:pointermove', ['$event'])
  onPointerMove(event: PointerEvent): void {
    if (!this.dragging) return;
    this.offsetX = this.dragOriginX + (event.clientX - this.dragStartX);
    this.offsetY = this.dragOriginY + (event.clientY - this.dragStartY);
    this.clampOffsets();
  }

  @HostListener('document:pointerup')
  onPointerUp(): void {
    this.dragging = false;
  }

  @HostListener('keydown.escape', ['$event'])
  onEscape(event: KeyboardEvent): void {
    // Also rendered as a sibling of app-photo-viewer in some flows - stop
    // propagation so Escape doesn't also close the viewer underneath.
    event.stopPropagation();
    this.cancel.emit();
  }

  onSave(): void {
    if (!this.imageReady || !this.imgRef || this.isSaving) return;

    this.isSaving = true;

    // Map the visible frame window back to source-image pixel coordinates.
    const scale = this.baseScale * this.zoom;
    const sourceSize = FRAME_SIZE / scale;
    const sourceX = -this.offsetX / scale;
    const sourceY = -this.offsetY / scale;

    const canvas = document.createElement('canvas');
    canvas.width = OUTPUT_SIZE;
    canvas.height = OUTPUT_SIZE;
    const ctx = canvas.getContext('2d')!;
    ctx.drawImage(
      this.imgRef.nativeElement,
      sourceX,
      sourceY,
      sourceSize,
      sourceSize,
      0,
      0,
      OUTPUT_SIZE,
      OUTPUT_SIZE,
    );

    canvas.toBlob(
      (blob) => {
        this.isSaving = false;
        if (blob) this.save.emit(blob);
      },
      'image/jpeg',
      0.92,
    );
  }

  /**
   * Applies a new zoom level. When `recenter` is set (initial load), the
   * image is centered in the frame; otherwise the zoom re-anchors on
   * whatever point is currently at the frame's center, so zooming via the
   * slider or wheel feels like it zooms into the current view rather than
   * yanking the image back to its top-left corner.
   */
  private applyZoom(zoom: number, recenter: boolean): void {
    const clampedZoom = Math.min(MAX_ZOOM, Math.max(MIN_ZOOM, zoom));
    const scale = this.baseScale * clampedZoom;
    const newWidth = this.naturalWidth * scale;
    const newHeight = this.naturalHeight * scale;

    if (recenter) {
      this.offsetX = (FRAME_SIZE - newWidth) / 2;
      this.offsetY = (FRAME_SIZE - newHeight) / 2;
    } else {
      const prevWidth = this.displayWidth || newWidth;
      const prevHeight = this.displayHeight || newHeight;
      const centerXRatio = (FRAME_SIZE / 2 - this.offsetX) / prevWidth;
      const centerYRatio = (FRAME_SIZE / 2 - this.offsetY) / prevHeight;
      this.offsetX = FRAME_SIZE / 2 - centerXRatio * newWidth;
      this.offsetY = FRAME_SIZE / 2 - centerYRatio * newHeight;
    }

    this.displayWidth = newWidth;
    this.displayHeight = newHeight;
    this.zoom = clampedZoom;
    this.clampOffsets();
  }

  /** Keeps the image covering the frame - no empty gaps at the edges. */
  private clampOffsets(): void {
    const minX = Math.min(0, FRAME_SIZE - this.displayWidth);
    const minY = Math.min(0, FRAME_SIZE - this.displayHeight);
    this.offsetX = Math.min(0, Math.max(minX, this.offsetX));
    this.offsetY = Math.min(0, Math.max(minY, this.offsetY));
  }
}
