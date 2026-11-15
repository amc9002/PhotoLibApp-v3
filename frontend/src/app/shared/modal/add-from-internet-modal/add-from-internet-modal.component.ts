import { Component, HostListener, Input, Output, EventEmitter } from '@angular/core';
import { CommonModule } from '@angular/common';
import { DomSanitizer, SafeUrl } from '@angular/platform-browser';
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
    // The bookmarklet needs an absolute API root - it runs on arbitrary
    // third-party pages, not this app's own origin, so a relative /api/
    // path (as used everywhere else - see ApiService) wouldn't resolve.
    // Derived from this page's own current origin at generation time,
    // rather than a build-time-baked value, so the same build is correct
    // whether served from the shared Tailscale instance or, in the
    // future, a local desktop backend on its own port - both already
    // serve the API from the same origin as the app itself.
    const apiBaseUrl = `${window.location.origin}/api`;
    const href = buildAddFromInternetBookmarklet(apiBaseUrl, this.galleryId);
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
