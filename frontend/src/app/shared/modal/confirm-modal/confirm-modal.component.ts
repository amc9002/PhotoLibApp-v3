import { Component, EventEmitter, HostListener, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ButtonGroupNavDirective } from '../../directives/button-group-nav.directive';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';

/** Generic Yes/No confirmation dialog, used for both destructive and non-destructive actions. */
@Component({
  selector: 'app-confirm-modal',
  standalone: true,
  imports: [CommonModule, ButtonGroupNavDirective, TranslatePipe],
  templateUrl: './confirm-modal.component.html',
  styleUrls: ['./confirm-modal.component.css'],
})
/**
 * `title`/`message`/`confirmText` are plain display strings, already
 * resolved by the caller (via the `t` pipe or a plural-aware helper) -
 * this component doesn't know about translation keys itself, only the
 * intrinsic "Working…" busy label does.
 */
export class ConfirmModalComponent {
  @Input() title = 'Вы ўпэўнены?';
  @Input() message = '';
  @Input() confirmText = 'Пацвердзіць';
  @Input() isBusy = false;
  @Input() variant: 'danger' | 'primary' = 'danger';

  @Output() confirm = new EventEmitter<void>();
  @Output() cancel = new EventEmitter<void>();

  @HostListener('keydown.escape', ['$event'])
  onEscape(event: KeyboardEvent) {
    // Also rendered as a sibling of app-photo-viewer in some flows - stop
    // propagation so Escape doesn't also close the viewer underneath.
    event.stopPropagation();
    this.cancel.emit();
  }
}
