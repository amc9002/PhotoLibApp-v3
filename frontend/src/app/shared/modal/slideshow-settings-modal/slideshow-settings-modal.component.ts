import { Component, EventEmitter, HostListener, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { SlideshowConfig, SlideshowOrder } from '../../../models/slideshow-config';

const MIN_INTERVAL_SECONDS = 1;
const MAX_INTERVAL_SECONDS = 60;
const DEFAULT_INTERVAL_SECONDS = 4;
const DEFAULT_ORDER: SlideshowOrder = 'forward';
const STORAGE_KEY = 'photolib.slideshow';

interface StoredSlideshowSettings {
  order: SlideshowOrder;
  intervalSeconds: number;
}

/**
 * Lets the user pick a playback order and per-slide interval, then emits
 * a {@link SlideshowConfig} for the photo viewer to run with. The chosen
 * order/interval are remembered (in localStorage) across visits, so
 * re-opening this modal later starts from what was last actually used.
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

    localStorage.setItem(
      STORAGE_KEY,
      JSON.stringify({ order: this.order, intervalSeconds: clamped } satisfies StoredSlideshowSettings),
    );

    this.start.emit({ order: this.order, intervalMs: clamped * 1000 });
  }
}

function readStoredSettings(): StoredSlideshowSettings {
  const validOrders: SlideshowOrder[] = ['forward', 'reverse', 'random'];

  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    if (!raw) return { order: DEFAULT_ORDER, intervalSeconds: DEFAULT_INTERVAL_SECONDS };

    const parsed = JSON.parse(raw) as Partial<StoredSlideshowSettings>;
    const order = validOrders.includes(parsed.order as SlideshowOrder) ? parsed.order! : DEFAULT_ORDER;
    const intervalSeconds =
      typeof parsed.intervalSeconds === 'number' &&
      parsed.intervalSeconds >= MIN_INTERVAL_SECONDS &&
      parsed.intervalSeconds <= MAX_INTERVAL_SECONDS
        ? parsed.intervalSeconds
        : DEFAULT_INTERVAL_SECONDS;

    return { order, intervalSeconds };
  } catch {
    return { order: DEFAULT_ORDER, intervalSeconds: DEFAULT_INTERVAL_SECONDS };
  }
}
