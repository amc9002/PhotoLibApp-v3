import { buildAddFromInternetBookmarklet } from './bookmarklet';

/**
 * `buildAddFromInternetBookmarklet` is the actual function that embeds the
 * API root into the generated bookmarklet - testing it directly (rather
 * than only through the component) deterministically covers both origin
 * shapes without needing to fake `window.location` in any way.
 */
describe('buildAddFromInternetBookmarklet', () => {
  function decodeScript(href: string): string {
    expect(href.startsWith('javascript:')).toBeTrue();
    return decodeURIComponent(href.substring('javascript:'.length));
  }

  it('embeds an HTTPS web origin API root exactly as given', () => {
    const apiBaseUrl = 'https://photolib.example.ts.net/api';
    const script = decodeScript(buildAddFromInternetBookmarklet(apiBaseUrl, 'gallery-1'));

    expect(script).toContain(`var API=${JSON.stringify(apiBaseUrl)};`);
    expect(script).toContain(`var GID=${JSON.stringify('gallery-1')};`);
  });

  it('embeds an HTTP localhost API root with an arbitrary port exactly as given', () => {
    const apiBaseUrl = 'http://localhost:54321/api';
    const script = decodeScript(buildAddFromInternetBookmarklet(apiBaseUrl, 'gallery-2'));

    expect(script).toContain(`var API=${JSON.stringify(apiBaseUrl)};`);
    expect(script).toContain(`var GID=${JSON.stringify('gallery-2')};`);
  });

  it('reflects whatever API root is passed in, not a cached or baked-in default', () => {
    const httpsScript = decodeScript(
      buildAddFromInternetBookmarklet('https://a.example.com/api', 'g'),
    );
    const localScript = decodeScript(
      buildAddFromInternetBookmarklet('http://localhost:12345/api', 'g'),
    );

    expect(httpsScript).toContain('https://a.example.com/api');
    expect(localScript).toContain('http://localhost:12345/api');
    expect(httpsScript).not.toEqual(localScript);
  });
});
