import { animate, style, transition, trigger } from '@angular/animations';

/**
 * Masks a layout reflow that a CSS-only transition can't smooth out on its
 * own - e.g. Discovery's grid switching its column count (an instant,
 * non-interpolatable change) while its container width is still easing
 * between collapsed/full. Bind to a value that changes between discrete
 * states (not enter/leave): the content snaps to opacity 0 immediately,
 * stays hidden long enough for the width transition + grid reflow to
 * finish, then fades back in - so the reflow itself is never seen.
 *
 * The 280ms delay isn't arbitrary: when Discovery expands because a
 * gallery was deselected, app-gallery-page's own :leave animation
 * (revealTopDown, 220ms) keeps it occupying flex space for that whole
 * time, so Discovery briefly competes for width with it mid-transition
 * before settling full-width once gallery-page is actually removed -
 * the delay needs to outlast that, not just Discovery's own CSS
 * width transition.
 */
export const fadeThroughReflow = trigger('fadeThroughReflow', [
  transition('* => *', [
    style({ opacity: 0 }),
    animate('220ms 280ms ease-out', style({ opacity: 1 })),
  ]),
]);
