import { Component, EventEmitter, HostListener, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ButtonGroupNavDirective } from '../../directives/button-group-nav.directive';
import { I18nService } from '../../../core/i18n/i18n.service';
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
 * intrinsic "Working…" busy label does. The defaults below are translated
 * once at construction time (rather than hardcoded in one language) so a
 * caller that forgets to override them still shows text in the user's
 * current language instead of always Belarusian.
 */
export class ConfirmModalComponent {
  @Input() title: string;
  @Input() message = '';
  @Input() confirmText: string;
  @Input() isBusy = false;
  @Input() variant: 'danger' | 'primary' = 'danger';

  @Output() confirm = new EventEmitter<void>();
  @Output() cancel = new EventEmitter<void>();

  constructor(i18n: I18nService) {
    this.title = i18n.translate('common.areYouSure');
    this.confirmText = i18n.translate('common.confirm');
  }

  @HostListener('keydown.escape', ['$event'])
  onEscape(event: KeyboardEvent) {
    // Also rendered as a sibling of app-photo-viewer in some flows - stop
    // propagation so Escape doesn't also close the viewer underneath.
    event.stopPropagation();
    this.cancel.emit();
  }
}
