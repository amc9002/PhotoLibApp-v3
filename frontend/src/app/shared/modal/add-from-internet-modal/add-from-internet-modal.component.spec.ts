import { DomSanitizer } from '@angular/platform-browser';
import { AddFromInternetModalComponent } from './add-from-internet-modal.component';

/**
 * Confirms the component wires `bookmarkletHref` to the page's *current*
 * origin (via `window.location.origin`), not a build-time value. Uses
 * Karma's own real origin (an actual "HTTP localhost with a port" case)
 * rather than faking `window.location`, which is fragile to override in a
 * real browser. The two origin *shapes* themselves (HTTPS web / HTTP
 * localhost+port) are covered deterministically in `bookmarklet.spec.ts`
 * against the underlying builder function directly.
 */
describe('AddFromInternetModalComponent', () => {
  function createComponent(): AddFromInternetModalComponent {
    const fakeSanitizer = {
      bypassSecurityTrustUrl: (url: string) => url,
    } as unknown as DomSanitizer;
    return new AddFromInternetModalComponent(fakeSanitizer);
  }

  it('derives the bookmarklet API root from the current page origin, not a baked-in value', () => {
    const component = createComponent();
    component.galleryId = 'gallery-xyz';

    const href = component.bookmarkletHref as unknown as string;
    const script = decodeURIComponent(href.substring('javascript:'.length));

    expect(script).toContain(`var API=${JSON.stringify(window.location.origin + '/api')};`);
    expect(script).toContain(`var GID=${JSON.stringify('gallery-xyz')};`);
  });

  it('never embeds the old hardcoded development URL', () => {
    const component = createComponent();
    component.galleryId = 'gallery-xyz';

    const href = component.bookmarkletHref as unknown as string;

    expect(href).not.toContain('localhost:5146');
  });
});
