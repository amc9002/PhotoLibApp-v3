import { Component, EventEmitter, HostListener, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { SlideshowConfig, SlideshowOrder } from '../../../models/slideshow-config';

const MIN_INTERVAL_SECONDS = 1;
const MAX_INTERVAL_SECONDS = 60;
const DEFAULT_INTERVAL_SECONDS = 4;

/**
 * Lets the user pick a playback order and per-slide interval, then emits
 * a {@link SlideshowConfig} for the photo viewer to run with.
 */
@Component({
  selector: 'app-slideshow-settings-modal',
  standalone: true,
  imports: [CommonModule, FormsModule, TranslatePipe],
  templateUrl: './slideshow-settings-modal.component.html',
  styleUrls: ['./slideshow-settings-modal.component.css'],
})
export class SlideshowSettingsModalComponent {
  @Output() start = new EventEmitter<SlideshowConfig>();
  @Output() cancel = new EventEmitter<void>();

  order: SlideshowOrder = 'forward';
  intervalSeconds = DEFAULT_INTERVAL_SECONDS;

  @HostListener('keydown.escape', ['$event'])
  onEscape(event: KeyboardEvent) {
    event.stopPropagation();
    this.cancel.emit();
  }

  setOrder(order: SlideshowOrder) {
    this.order = order;
  }

  onStart() {
    const clamped = Math.min(
      MAX_INTERVAL_SECONDS,
      Math.max(MIN_INTERVAL_SECONDS, Math.round(this.intervalSeconds) || DEFAULT_INTERVAL_SECONDS),
    );

    this.start.emit({ order: this.order, intervalMs: clamped * 1000 });
  }
}
