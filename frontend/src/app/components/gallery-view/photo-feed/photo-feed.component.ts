import { Component, EventEmitter, HostListener, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ScrollingModule } from '@angular/cdk/scrolling';
import { PhotoListItemDto } from '../../../models/photoLisrItem.dto';
import { PhotoContextMenuComponent } from '../../../shared/ui/photo-context-menu/photo-context-menu.component';
import { PhotoFeedItemComponent } from './photo-feed-item/photo-feed-item.component';

const CONTEXT_MENU_WIDTH = 210;
const CONTEXT_MENU_HEIGHT = 190;
const CONTEXT_MENU_MARGIN = 8;

/**
 * Vertical, freely-scrollable feed of full-size photos - the alternative
 * to `GalleryGridComponent`. Scrolling itself is the "viewing" experience
 * (no click-to-open modal, no next/prev arrows), so there's no
 * `photoClicked` output here, only the same action-menu outputs the grid's
 * right-click context menu emits.
 */
@Component({
  selector: 'app-photo-feed',
  standalone: true,
  imports: [CommonModule, ScrollingModule, PhotoContextMenuComponent, PhotoFeedItemComponent],
  templateUrl: './photo-feed.component.html',
  styleUrls: ['./photo-feed.component.css'],
})
export class PhotoFeedComponent {
  /**
   * Fixed row height (px) for the virtual-scroll viewport. A photo's real
   * image loads asynchronously after the row is already laid out; with
   * CDK's `autosize` strategy that late height change forced a remeasure
   * mid-scroll, which visibly snapped the scroll position back. A fixed
   * height sidesteps that entirely - every row is this tall from the
   * start, loaded or not, so nothing ever needs to be recomputed. Photos
   * of any aspect ratio still fit via `object-fit: contain` in the item's
   * own CSS.
   */
  readonly feedItemHeight = 640;

  @Input({ required: true }) photos!: PhotoListItemDto[];
  @Output() editPhotoRequest = new EventEmitter<string>();
  @Output() showPhotoInfoRequest = new EventEmitter<string>();
  @Output() copyPhotoRequest = new EventEmitter<string>();
  @Output() movePhotoRequest = new EventEmitter<string>();
  @Output() deletePhotoRequest = new EventEmitter<string>();

  contextMenuPhotoId: string | null = null;
  contextMenuX = 0;
  contextMenuY = 0;

  trackById(index: number, photo: PhotoListItemDto): string {
    return photo.id;
  }

  openContextMenu(event: MouseEvent, photoId: string) {
    event.stopPropagation();

    this.contextMenuPhotoId = photoId;
    this.contextMenuX = Math.min(event.clientX, window.innerWidth - CONTEXT_MENU_WIDTH - CONTEXT_MENU_MARGIN);
    this.contextMenuY = Math.min(event.clientY, window.innerHeight - CONTEXT_MENU_HEIGHT - CONTEXT_MENU_MARGIN);
  }

  closeContextMenu() {
    this.contextMenuPhotoId = null;
  }

  @HostListener('document:click', ['$event'])
  onDocumentClick(event: MouseEvent) {
    if (!this.contextMenuPhotoId) return;
    if (!(event.target as HTMLElement).closest('.context-menu')) {
      this.closeContextMenu();
    }
  }

  @HostListener('document:keydown.escape')
  onEscape() {
    this.closeContextMenu();
  }
}
