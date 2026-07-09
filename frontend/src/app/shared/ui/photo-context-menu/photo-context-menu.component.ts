import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';

/**
 * Floating action menu positioned at a click point (right-click on a
 * gallery-grid thumbnail). Purely presentational - open/close state and
 * outside-click/Escape dismissal live in the parent (gallery-grid),
 * matching this app's existing dropdown pattern (see toolbar.component.ts)
 * rather than each menu managing its own document listeners.
 */
@Component({
  selector: 'app-photo-context-menu',
  standalone: true,
  imports: [CommonModule, TranslatePipe],
  templateUrl: './photo-context-menu.component.html',
  styleUrls: ['./photo-context-menu.component.css'],
})
export class PhotoContextMenuComponent {
  @Input() x = 0;
  @Input() y = 0;

  @Output() editInfo = new EventEmitter<void>();
  @Output() showInfo = new EventEmitter<void>();
  @Output() copyTo = new EventEmitter<void>();
  @Output() moveTo = new EventEmitter<void>();
  @Output() deletePhoto = new EventEmitter<void>();
}
