export type SlideshowOrder = 'forward' | 'reverse' | 'random';

export interface SlideshowConfig {
  order: SlideshowOrder;
  intervalMs: number;
  /** How long the crossfade dissolve between slides takes. */
  transitionMs: number;
}
