import { Component, HostListener, Input, Output, EventEmitter } from '@angular/core';
import { CommonModule } from '@angular/common';
import { DomSanitizer, SafeUrl } from '@angular/platform-browser';
import { environment } from '../../../../environments/environment';
import { buildAddFromInternetBookmarklet } from '../../utils/bookmarklet';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';

/**
 * Explains and hands out the "Add from internet" bookmarklet for a single
 * gallery. The bookmarklet is regenerated (and must be re-dragged) whenever
 * the target gallery changes, since the gallery id is baked into its code.
 */
@Component({
  selector: 'app-add-from-internet-modal',
  standalone: true,
  imports: [CommonModule, TranslatePipe],
  templateUrl: './add-from-internet-modal.component.html',
  styleUrls: ['./add-from-internet-modal.component.css'],
})
export class AddFromInternetModalComponent {
  @Input({ required: true }) galleryId!: string;
  @Input() galleryTitle = '';

  @Output() close = new EventEmitter<void>();

  constructor(private sanitizer: DomSanitizer) {}

  // Angular sanitizes `javascript:` hrefs by default; this one is generated
  // entirely by us (no user input in the URL), so bypassing is safe.
  get bookmarkletHref(): SafeUrl {
    const href = buildAddFromInternetBookmarklet(environment.apiBaseUrl, this.galleryId);
    return this.sanitizer.bypassSecurityTrustUrl(href);
  }

  @HostListener('keydown.escape', ['$event'])
  onEscape(event: KeyboardEvent) {
    // Also rendered as a sibling of app-photo-viewer in some flows - stop
    // propagation so Escape doesn't also close the viewer underneath.
    event.stopPropagation();
    this.close.emit();
  }
}
