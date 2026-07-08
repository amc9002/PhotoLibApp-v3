export type SlideshowOrder = 'forward' | 'reverse' | 'random';

export interface SlideshowConfig {
  order: SlideshowOrder;
  intervalMs: number;
}
