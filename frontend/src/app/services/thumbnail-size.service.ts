import { Injectable } from '@angular/core';

const MIN_SIZE = 100;
const MAX_SIZE = 260;
const STEP = 20;
const DEFAULT_SIZE = 160;

@Injectable({ providedIn: 'root' })
export class ThumbnailSizeService {
  size = DEFAULT_SIZE;

  get canIncrease() {
    return this.size < MAX_SIZE;
  }

  get canDecrease() {
    return this.size > MIN_SIZE;
  }

  increase() {
    this.size = Math.min(MAX_SIZE, this.size + STEP);
  }

  decrease() {
    this.size = Math.max(MIN_SIZE, this.size - STEP);
  }
}
