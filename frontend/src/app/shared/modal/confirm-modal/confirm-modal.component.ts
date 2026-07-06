import { Component, EventEmitter, HostListener, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ButtonGroupNavDirective } from '../../directives/button-group-nav.directive';

/** Generic Yes/No confirmation dialog, used for both destructive and non-destructive actions. */
@Component({
  selector: 'app-confirm-modal',
  standalone: true,
  imports: [CommonModule, ButtonGroupNavDirective],
  templateUrl: './confirm-modal.component.html',
  styleUrls: ['./confirm-modal.component.css'],
})
export class ConfirmModalComponent {
  @Input() title = 'Are you sure?';
  @Input() message = '';
  @Input() confirmText = 'Confirm';
  @Input() isBusy = false;
  @Input() variant: 'danger' | 'primary' = 'danger';

  @Output() confirm = new EventEmitter<void>();
  @Output() cancel = new EventEmitter<void>();

  @HostListener('keydown.escape')
  onEscape() {
    this.cancel.emit();
  }
}
