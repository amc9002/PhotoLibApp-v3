import { Injectable } from '@angular/core';
import { PhotoListItemDto } from '../models/photoLisrItem.dto';

@Injectable({ providedIn: 'root' })
export class PhotoSelectionService {
  selectedIds = new Set<string>();
  private anchorId: string | null = null;

  get count() {
    return this.selectedIds.size;
  }

  get ids(): string[] {
    return Array.from(this.selectedIds);
  }

  isSelected(id: string) {
    return this.selectedIds.has(id);
  }

  clear() {
    this.selectedIds.clear();
    this.anchorId = null;
  }

  /** Ctrl+click: toggle one photo, it becomes the new range anchor. */
  toggle(id: string) {
    if (this.selectedIds.has(id)) {
      this.selectedIds.delete(id);
    } else {
      this.selectedIds.add(id);
    }
    this.anchorId = id;
  }

  /** Shift+click: select the contiguous range from the last anchor to this photo. */
  selectRange(photos: PhotoListItemDto[], toId: string) {
    if (!this.anchorId) {
      this.toggle(toId);
      return;
    }

    const ids = photos.map((p) => p.id);
    const from = ids.indexOf(this.anchorId);
    const to = ids.indexOf(toId);

    if (from === -1 || to === -1) {
      this.toggle(toId);
      return;
    }

    const [start, end] = from < to ? [from, to] : [to, from];
    for (let i = start; i <= end; i++) {
      this.selectedIds.add(ids[i]);
    }
    this.anchorId = toId;
  }

  /** Drag-select: add a batch of ids covered by the marquee rectangle. */
  selectMany(ids: string[]) {
    ids.forEach((id) => this.selectedIds.add(id));
    if (ids.length) {
      this.anchorId = ids[ids.length - 1];
    }
  }
}
