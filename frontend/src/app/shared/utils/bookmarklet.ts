/**
 * Builds a `javascript:` bookmarklet that, when run on any page, lets the
 * user click a photo to send it to the PhotoLib API. Targets a fixed
 * gallery baked in at generation time.
 *
 * The actual upload happens in a same-origin popup (see the backend's
 * `/api/Photo/capture` page) rather than via a direct fetch() from the
 * source page's script context: many sites (Facebook, Instagram, ...) set
 * a Content-Security-Policy that blocks a page from fetching arbitrary
 * third-party hosts, which would silently swallow a direct fetch.
 *
 * @param apiBaseUrl absolute API root, e.g. "http://localhost:5146/api"
 */
export function buildAddFromInternetBookmarklet(
  apiBaseUrl: string,
  galleryId: string,
): string {
  const script = `
(function(){
  var API=${JSON.stringify(apiBaseUrl)};
  var GID=${JSON.stringify(galleryId)};

  // Resolves the image behind a click: a plain <img>, a CSS background-image
  // on the clicked element or a nearby ancestor (common on Facebook/
  // Instagram-style grids), or a single <img> nested inside it.
  function findImageUrl(start){
    var node = start;
    for (var depth = 0; node && depth < 5; node = node.parentElement, depth++) {
      if (node.tagName === 'IMG' && (node.currentSrc || node.src)) {
        return node.currentSrc || node.src;
      }
      var bg = window.getComputedStyle(node).backgroundImage;
      var match = bg && /url\\((['"]?)(.*?)\\1\\)/.exec(bg);
      if (match && match[2]) return match[2];
      var nested = node.querySelector && node.querySelector('img');
      if (nested && (nested.currentSrc || nested.src)) return nested.currentSrc || nested.src;
    }
    return null;
  }

  var imgs = Array.prototype.slice.call(document.images);
  imgs.forEach(function(img){ img.style.outline = '2px solid #4da3ff'; img.style.cursor = 'crosshair'; });

  function cleanup(){
    imgs.forEach(function(img){ img.style.outline = ''; img.style.cursor = ''; });
    document.removeEventListener('click', onClick, true);
    document.removeEventListener('keydown', onKeyDown, true);
  }
  function onKeyDown(e){
    if (e.key === 'Escape') cleanup();
  }
  function onClick(e){
    var url = findImageUrl(e.target);
    if (!url) return;
    e.preventDefault();
    e.stopPropagation();
    cleanup();
    window.open(
      API + '/Photo/capture?imageUrl=' + encodeURIComponent(url) + '&galleryId=' + encodeURIComponent(GID),
      'photolib_capture',
      'width=360,height=160'
    );
  }
  document.addEventListener('click', onClick, true);
  document.addEventListener('keydown', onKeyDown, true);
})();
`.trim();

  return 'javascript:' + encodeURIComponent(script);
}
