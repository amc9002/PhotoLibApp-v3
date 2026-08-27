import { Injectable } from '@angular/core';
import { PhotoListItemDto } from '../../models/photoLisrItem.dto';
import { SlideshowConfig, SlideshowOrder } from '../../models/slideshow-config';

/**
 * Owns the auto-advancing slideshow sequence/timer for `PhotoViewerComponent`
 * - pulled out because it's a self-contained state machine (sequence order,
 * pacing, timer) that doesn't otherwise touch the viewer's UI state, unlike
 * the rest of that component.
 *
 * Paced off the viewer's `(loaded)` event via {@link onPhotoLoaded} rather
 * than a fixed `setInterval` - a metronome that fires regardless of load
 * status would, for a slow-loading original, keep requesting the next photo
 * before the previous request ever resolves.
 *
 * Not `providedIn: 'root'` - `PhotoViewerComponent` declares it in its own
 * `providers` so each viewer instance gets independent playback state.
 */
@Injectable()
export class SlideshowPlayerService {
  /** True while auto-advancing through the sequence. */
  active = false;

  private sequence: PhotoListItemDto[] = [];
  private intervalMs = 0;
  private transitionMs = 0;
  private advanceTimeout?: ReturnType<typeof setTimeout>;
  private onSelect?: (photoId: string) => void;

  /**
   * Starts playing `photos` per `config`. If the sequence's first photo
   * differs from `currentPhotoId`, calls `onSelect` (deferred a tick, since
   * this typically runs from the viewer's `ngOnInit` and emitting
   * synchronously there would update the parent-bound `activePhotoId` input
   * mid-change-detection-cycle) to move there first - pacing then continues
   * once the caller reports the photo loaded via {@link onPhotoLoaded}.
   */
  start(
    photos: PhotoListItemDto[],
    config: SlideshowConfig,
    currentPhotoId: string,
    onSelect: (photoId: string) => void,
  ): void {
    if (!photos.length) return;

    this.sequence = buildSequence(photos, config.order);
    this.intervalMs = config.intervalMs;
    this.transitionMs = config.transitionMs ?? 0;
    this.onSelect = onSelect;
    this.active = true;

    const firstId = this.sequence[0].id;
    if (firstId !== currentPhotoId) {
      setTimeout(() => onSelect(firstId));
    }
    // If firstId === currentPhotoId, the viewer's initial load still fires
    // and reports back via onPhotoLoaded, so pacing starts either way
    // without needing a separate kick here.
  }

  /**
   * Called once the currently displayed photo has actually finished loading
   * (or failed) - schedules the advance to `currentPhotoId`'s successor in
   * the sequence.
   */
  onPhotoLoaded(currentPhotoId: string): void {
    if (!this.active) return;

    this.clearTimer();
    // The interval is "how long to look at the photo", separate from and in
    // addition to however long the crossfade itself takes - without adding
    // the transition duration here, a transition configured longer than (or
    // close to) the interval never gets to finish before the next advance
    // interrupts it and restarts it from scratch, which looks like it's
    // barely fading at all no matter how long it's set to.
    const delay = this.intervalMs + this.transitionMs;
    this.advanceTimeout = setTimeout(() => this.advance(currentPhotoId), delay);
  }

  private advance(currentPhotoId: string): void {
    if (!this.sequence.length || !this.onSelect) return;

    const currentIndex = this.sequence.findIndex((p) => p.id === currentPhotoId);
    const nextIndex = (currentIndex + 1) % this.sequence.length;
    this.onSelect(this.sequence[nextIndex].id);
  }

  stop(): void {
    if (!this.active) return;

    this.active = false;
    this.clearTimer();
  }

  clearTimer(): void {
    if (this.advanceTimeout !== undefined) {
      clearTimeout(this.advanceTimeout);
      this.advanceTimeout = undefined;
    }
  }
}

function buildSequence(photos: PhotoListItemDto[], order: SlideshowOrder): PhotoListItemDto[] {
  const sequence = [...photos];

  if (order === 'reverse') {
    sequence.reverse();
  } else if (order === 'random') {
    for (let i = sequence.length - 1; i > 0; i--) {
      const j = Math.floor(Math.random() * (i + 1));
      [sequence[i], sequence[j]] = [sequence[j], sequence[i]];
    }
  }

  return sequence;
}
