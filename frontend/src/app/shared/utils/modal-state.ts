/**
 * Minimal open/close toggle for a modal with no extra state of its own.
 * Pulled out of `AppComponent`, which had a `showX = false` / `openX()` /
 * `closeX()` triad repeated for every simple modal it owns.
 */
export class ModalState {
  isOpen = false;

  open(): void {
    this.isOpen = true;
  }

  close(): void {
    this.isOpen = false;
  }
}
