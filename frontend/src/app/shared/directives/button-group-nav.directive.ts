import {
  AfterViewInit,
  Directive,
  ElementRef,
  HostListener,
} from '@angular/core';

/**
 * Attach to a container of action buttons (e.g. a modal footer) to get:
 * - focus moved onto the first enabled button once the container renders
 * - Left/Up and Right/Down arrow keys cycling focus between the buttons
 */
@Directive({
  selector: '[appButtonGroupNav]',
  standalone: true,
})
export class ButtonGroupNavDirective implements AfterViewInit {
  constructor(private el: ElementRef<HTMLElement>) {}

  ngAfterViewInit() {
    this.buttons[0]?.focus();
  }

  private get buttons(): HTMLButtonElement[] {
    return Array.from(
      this.el.nativeElement.querySelectorAll('button:not(:disabled)'),
    );
  }

  @HostListener('keydown.arrowright', ['$event'])
  @HostListener('keydown.arrowdown', ['$event'])
  onNext(event: KeyboardEvent) {
    this.move(event, 1);
  }

  @HostListener('keydown.arrowleft', ['$event'])
  @HostListener('keydown.arrowup', ['$event'])
  onPrev(event: KeyboardEvent) {
    this.move(event, -1);
  }

  private move(event: KeyboardEvent, delta: number) {
    const buttons = this.buttons;
    if (buttons.length < 2) return;

    event.preventDefault();
    const current = buttons.indexOf(
      document.activeElement as HTMLButtonElement,
    );
    const next = (current + delta + buttons.length) % buttons.length;
    buttons[next].focus();
  }
}
