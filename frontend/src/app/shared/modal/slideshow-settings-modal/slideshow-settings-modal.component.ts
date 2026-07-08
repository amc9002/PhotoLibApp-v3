import { Component, EventEmitter, HostListener, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { SlideshowConfig, SlideshowOrder } from '../../../models/slideshow-config';

const MIN_INTERVAL_SECONDS = 1;
const MAX_INTERVAL_SECONDS = 60;
const DEFAULT_INTERVAL_SECONDS = 4;

const MIN_TRANSITION_SECONDS = 0.2;
const MAX_TRANSITION_SECONDS = 5;
const DEFAULT_TRANSITION_SECONDS = 1.2;

const DEFAULT_ORDER: SlideshowOrder = 'forward';
const STORAGE_KEY = 'photolib.slideshow';

interface StoredSlideshowSettings {
  order: SlideshowOrder;
  intervalSeconds: number;
  transitionSeconds: number;
}

/**
 * Lets the user pick a playback order, per-slide interval, and crossfade
 * duration, then emits a {@link SlideshowConfig} for the photo viewer to
 * run with. The chosen values are remembered (in localStorage) across
 * visits, so re-opening this modal later starts from what was last
 * actually used.
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

  private readonly stored = readStoredSettings();

  order: SlideshowOrder = this.stored.order;
  intervalSeconds = this.stored.intervalSeconds;
  transitionSeconds = this.stored.transitionSeconds;

  @HostListener('keydown.escape', ['$event'])
  onEscape(event: KeyboardEvent) {
    event.stopPropagation();
    this.cancel.emit();
  }

  setOrder(order: SlideshowOrder) {
    this.order = order;
  }

  onStart() {
    const intervalSeconds = Math.min(
      MAX_INTERVAL_SECONDS,
      Math.max(MIN_INTERVAL_SECONDS, Math.round(this.intervalSeconds) || DEFAULT_INTERVAL_SECONDS),
    );
    const transitionSeconds = Math.min(
      MAX_TRANSITION_SECONDS,
      Math.max(MIN_TRANSITION_SECONDS, this.transitionSeconds || DEFAULT_TRANSITION_SECONDS),
    );

    localStorage.setItem(
      STORAGE_KEY,
      JSON.stringify({
        order: this.order,
        intervalSeconds,
        transitionSeconds,
      } satisfies StoredSlideshowSettings),
    );

    this.start.emit({
      order: this.order,
      intervalMs: intervalSeconds * 1000,
      transitionMs: Math.round(transitionSeconds * 1000),
    });
  }
}

function readStoredSettings(): StoredSlideshowSettings {
  const validOrders: SlideshowOrder[] = ['forward', 'reverse', 'random'];
  const fallback: StoredSlideshowSettings = {
    order: DEFAULT_ORDER,
    intervalSeconds: DEFAULT_INTERVAL_SECONDS,
    transitionSeconds: DEFAULT_TRANSITION_SECONDS,
  };

  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    if (!raw) return fallback;

    const parsed = JSON.parse(raw) as Partial<StoredSlideshowSettings>;

    const order = validOrders.includes(parsed.order as SlideshowOrder) ? parsed.order! : fallback.order;
    const intervalSeconds =
      typeof parsed.intervalSeconds === 'number' &&
      parsed.intervalSeconds >= MIN_INTERVAL_SECONDS &&
      parsed.intervalSeconds <= MAX_INTERVAL_SECONDS
        ? parsed.intervalSeconds
        : fallback.intervalSeconds;
    const transitionSeconds =
      typeof parsed.transitionSeconds === 'number' &&
      parsed.transitionSeconds >= MIN_TRANSITION_SECONDS &&
      parsed.transitionSeconds <= MAX_TRANSITION_SECONDS
        ? parsed.transitionSeconds
        : fallback.transitionSeconds;

    return { order, intervalSeconds, transitionSeconds };
  } catch {
    return fallback;
  }
}
