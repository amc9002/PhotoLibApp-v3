import { Injectable } from '@angular/core';

const MIN_SIZE = 100;
const MAX_SIZE = 260;
const STEP = 20;
const DEFAULT_SIZE = 160;

/**
 * Thumbnail size is per-gallery: each gallery id keeps its own value so
 * resizing one gallery's grid doesn't affect any other gallery.
 */
@Injectable({ providedIn: 'root' })
export class ThumbnailSizeService {
  private sizes = new Map<string, number>();
  private currentGalleryId: string | null = null;

  setGallery(galleryId: string | null) {
    this.currentGalleryId = galleryId;
  }

  /** Drops a deleted gallery's stored size so the map doesn't grow forever. */
  forgetGallery(galleryId: string) {
    this.sizes.delete(galleryId);
  }

  get size(): number {
    if (!this.currentGalleryId) return DEFAULT_SIZE;
    return this.sizes.get(this.currentGalleryId) ?? DEFAULT_SIZE;
  }

  get canIncrease() {
    return this.size < MAX_SIZE;
  }

  get canDecrease() {
    return this.size > MIN_SIZE;
  }

  increase() {
    this.setSize(Math.min(MAX_SIZE, this.size + STEP));
  }

  decrease() {
    this.setSize(Math.max(MIN_SIZE, this.size - STEP));
  }

  private setSize(value: number) {
    if (!this.currentGalleryId) return;
    this.sizes.set(this.currentGalleryId, value);
  }
}
