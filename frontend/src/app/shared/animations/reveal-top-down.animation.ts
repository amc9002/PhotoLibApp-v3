import { animate, style, transition, trigger } from '@angular/animations';

/**
 * Soft top-to-bottom reveal used across the app for view/modal/menu content
 * appearing or disappearing (a selected gallery mounting, the full-screen
 * viewer, dialogs, dropdown/context menus) - the element eases in from
 * slightly above while fading in, rather than popping in instantly.
 * Deliberately not applied to anything scroll-driven - scrolling itself
 * must stay instant.
 */
export const revealTopDown = trigger('revealTopDown', [
  transition(':enter', [
    style({ opacity: 0, transform: 'translateY(-24px)' }),
    animate('320ms cubic-bezier(0.4, 0, 0.2, 1)', style({ opacity: 1, transform: 'translateY(0)' })),
  ]),
  transition(':leave', [
    animate('220ms cubic-bezier(0.4, 0, 0.2, 1)', style({ opacity: 0, transform: 'translateY(-14px)' })),
  ]),
]);
