using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using PhotoLibApi.Data;
using PhotoLibApi.Models;
using PhotoLibApi.Services;

namespace PhotoLibApi.Controllers
{
    /// <summary>
    /// Controller for working with photos.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class PhotoController : ControllerBase
    {
        /// <summary>Upper bound on how many bytes a from-url download may use.</summary>
        private const long MaxDownloadBytes = 25 * 1024 * 1024;

        private readonly PhotoDbContext _db;
        private readonly PhotoFilePathHelper _filePathHelper;
        private readonly TagResolver _tagResolver;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly PhotoImageProcessingService _imageProcessing;
        private readonly PhotoDescriptionAiService _descriptionAi;
        private readonly CurrentUserService _currentUser;
        private readonly GalleryAccessService _galleryAccess;

        /// <summary>
        /// Creates the controller with its DB context and supporting services.
        /// </summary>
        public PhotoController(
            PhotoDbContext db,
            PhotoFilePathHelper filePathHelper,
            TagResolver tagResolver,
            IHttpClientFactory httpClientFactory,
            PhotoImageProcessingService imageProcessing,
            PhotoDescriptionAiService descriptionAi,
            CurrentUserService currentUser,
            GalleryAccessService galleryAccess)
        {
            _db = db;
            _filePathHelper = filePathHelper;
            _tagResolver = tagResolver;
            _httpClientFactory = httpClientFactory;
            _imageProcessing = imageProcessing;
            _descriptionAi = descriptionAi;
            _currentUser = currentUser;
            _galleryAccess = galleryAccess;
        }

        /// <summary>
        /// Returns photos belonging to a gallery.
        /// </summary>
        /// <param name="galleryId">Gallery identifier.</param>
        /// <response code="200">List of photos.</response>
        [HttpGet("by-gallery/{galleryId:guid}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<IEnumerable<Photo>>> GetByGallery(Guid galleryId)
        {
            if (!await _galleryAccess.IsOwnedAsync(galleryId, _currentUser.UserId))
                return NotFound();

            var photos = await _db.Photos
                .AsNoTracking()
                .Where(p => p.GalleryId == galleryId && !p.IsDeleted)
                .OrderBy(p => p.SortOrder)
                // Minimal projection for gallery view: only data required to
                // render the thumbnails list and the viewer's info panel.
                .Select(p => new
                {
                    p.Id,
                    p.Title,
                    p.Description,
                    p.HasThumbnail,
                    p.UpdatedAtUtc,
                    Tags = p.Tags.Select(t => t.Name)
                })
                .ToListAsync();

            return Ok(photos);
        }

#if DEBUG
        /// <summary>
        /// DEV: Returns all photos in a gallery, including deleted ones.
        /// </summary>
        [HttpGet("dev/by-gallery/{galleryId:guid}")]
        public async Task<IActionResult> DevGetAllByGallery(Guid galleryId)
        {
            if (!await _galleryAccess.IsOwnedAsync(galleryId, _currentUser.UserId))
                return NotFound();

            var photos = await _db.Photos
                .AsNoTracking()
                .Where(p => p.GalleryId == galleryId)
                .OrderBy(p => p.CreatedAtUtc)
                .Select(p => new
                {
                    p.Id,
                    p.Title,
                    p.IsDeleted,
                    p.HasOriginal,
                    p.HasThumbnail
                })
                .ToListAsync();

            return Ok(photos);
        }
#endif

#if DEBUG
        /// <summary>
        /// DEV: Returns deleted photos in a gallery.
        /// </summary>
        [HttpGet("dev/deleted/by-gallery/{galleryId:guid}")]
        public async Task<IActionResult> DevGetDeletedByGallery(Guid galleryId)
        {
            if (!await _galleryAccess.IsOwnedAsync(galleryId, _currentUser.UserId))
                return NotFound();

            var photos = await _db.Photos
                .AsNoTracking()
                .Where(p => p.GalleryId == galleryId && p.IsDeleted)
                .OrderBy(p => p.UpdatedAtUtc)
                .Select(p => new
                {
                    p.Id,
                    p.Title,
                    p.UpdatedAtUtc
                })
                .ToListAsync();

            return Ok(photos);
        }
#endif  

        /// <summary>
        /// Returns photo metadata by identifier.
        /// </summary>
        /// <param name="id">Photo identifier.</param>
        /// <response code="200">Photo metadata returned.</response>
        /// <response code="404">Photo not found.</response>
        [HttpGet("{id:guid}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetById(Guid id)
        {
            var photo = await _db.Photos
                .AsNoTracking()
                .Where(p => p.Id == id)
                .Select(p => new
                {
                    p.Id,
                    p.GalleryId,
                    p.Title,
                    p.Description,
                    p.CreatedAtUtc,
                    p.UpdatedAtUtc,
                    p.HasOriginal,
                    p.HasThumbnail,
                    p.IsDeleted,
                    p.ExifJson,
                    p.Latitude,
                    p.Longitude,
                    Tags = p.Tags.Select(t => t.Name)
                })
                .FirstOrDefaultAsync();

            if (photo == null)
                return NotFound();

            if (!await _galleryAccess.IsOwnedAsync(photo.GalleryId, _currentUser.UserId))
                return NotFound();

            return Ok(photo);
        }

        /// <summary>
        /// Returns the original image file for a photo.
        /// </summary>
        /// <param name="id">Identifier of the photo.</param>
        /// <response code="200">Image file returned.</response>
        /// <response code="404">Photo file not found.</response>
        [HttpGet("{id:guid}/file")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetFile(Guid id)
        {
            var photo = await _galleryAccess.GetOwnedPhotoAsync(id, _currentUser.UserId);
            if (photo == null || !photo.HasOriginal)
                return NotFound();

            if (!_filePathHelper.OriginalExists(id))
                return NotFound();

            return PhysicalFile(
                _filePathHelper.GetOriginalFilePath(id),
                ImageContentTypes.Jpeg);
        }

        /// <summary>
        /// Returns the thumbnail image file for a photo.
        /// </summary>
        /// <param name="id">Identifier of the photo.</param>
        /// <response code="200">Thumbnail image returned.</response>
        /// <response code="404">Thumbnail not found.</response>
        [HttpGet("{id:guid}/thumbnail")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetThumbnail(Guid id)
        {
            var photo = await _galleryAccess.GetOwnedPhotoAsync(id, _currentUser.UserId);
            if (photo == null || !photo.HasThumbnail)
                return NotFound();

            if (!_filePathHelper.ThumbnailExists(id))
                return NotFound();

            return PhysicalFile(
                _filePathHelper.GetThumbnailFilePath(id),
                ImageContentTypes.Jpeg);
        }

        /// <summary>
        /// Creates a photo metadata record.
        /// </summary>
        /// <remarks>
        /// This endpoint creates metadata only.
        /// The image file will be uploaded in a later step.
        /// </remarks>
        /// <response code="201">Photo metadata created.</response>
        /// <response code="400">Invalid request data.</response>
        [HttpPost]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<Photo>> Create(
            [FromBody] CreatePhotoRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            // 1️⃣ Reject empty Guid explicitly
            if (request.GalleryId == Guid.Empty)
                return BadRequest("GalleryId must not be empty.");

            // 2️⃣ Check that the gallery exists and is owned by the caller
            if (!await _galleryAccess.IsOwnedAsync(request.GalleryId, _currentUser.UserId))
                return NotFound(new { message = $"Gallery with id '{request.GalleryId}' not found." });

            // Idempotent replay: a sync retry with the same ClientTempId
            // returns the row that already exists instead of duplicating it.
            if (!string.IsNullOrEmpty(request.ClientTempId))
            {
                var existing = await _db.Photos.FirstOrDefaultAsync(p =>
                    p.GalleryId == request.GalleryId &&
                    p.ClientTempId == request.ClientTempId &&
                    !p.IsDeleted);

                if (existing != null)
                    return StatusCode(StatusCodes.Status201Created, existing);
            }

            var photo = new Photo
            {
                Id = Guid.NewGuid(),
                GalleryId = request.GalleryId,
                Title = request.Title,
                Description = request.Description,
                ClientTempId = request.ClientTempId,
                CreatedAtUtc = DateTime.UtcNow,
                SortOrder = await NextSortOrderAsync(request.GalleryId),
            };

            _db.Photos.Add(photo);
            await _db.SaveChangesAsync();

            return StatusCode(StatusCodes.Status201Created, photo);
        }

        /// <summary>
        /// Uploads an original image file for an existing photo.
        /// </summary>
        /// <remarks>
        /// This endpoint accepts a multipart/form-data request and stores
        /// the uploaded image file on the server file system.
        /// The file is saved using the photo identifier as its filename.
        /// </remarks>
        /// <param name="id">Identifier of the photo.</param>
        /// <param name="file">Image file to upload.</param>
        /// <response code="204">File uploaded successfully.</response>
        /// <response code="400">File is missing or empty.</response>
        /// <response code="404">Photo with the specified id was not found.</response>
        [HttpPost("{id:guid}/upload")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Upload(Guid id, [FromForm] IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { message = "File is required." });

            var photo = await _galleryAccess.GetOwnedPhotoAsync(id, _currentUser.UserId);
            if (photo == null)
                return NotFound();

            Directory.CreateDirectory(_filePathHelper.GetOriginalsDirectory());
            var filePath = _filePathHelper.GetOriginalFilePath(id);

            await using (var stream = System.IO.File.Create(filePath))
            {
                await file.CopyToAsync(stream);
            }

            try
            {
                _imageProcessing.GenerateThumbnailAndExif(photo, filePath);
            }
            catch (SixLabors.ImageSharp.UnknownImageFormatException)
            {
                return BadRequest(new { message = "File is not a recognizable image." });
            }

            photo.HasOriginal = true;
            await _db.SaveChangesAsync();

            return NoContent();
        }

        /// <summary>
        /// Same-origin relay page for the "Add from internet" bookmarklet.
        /// </summary>
        /// <remarks>
        /// The bookmarklet opens this as a popup instead of calling
        /// from-url directly from the source page's script context, because
        /// some sites (e.g. Facebook) set a Content-Security-Policy that
        /// blocks a page from fetching arbitrary third-party hosts. This
        /// page's own script runs on this app's origin, so it isn't subject
        /// to the source page's CSP; it posts to from-url and reports the
        /// result, then closes itself on success.
        /// The image URL is rendered into an HTML data attribute (HTML-encoded)
        /// rather than interpolated into inline script, so it can't be used
        /// to break out into a script context.
        /// </remarks>
        [HttpGet("capture")]
        [AllowAnonymous]
        public ContentResult Capture([FromQuery] string imageUrl, [FromQuery] Guid galleryId)
        {
            var encodedImageUrl = System.Net.WebUtility.HtmlEncode(imageUrl ?? "");

            const string template = """
                <!doctype html>
                <html>
                <head>
                <meta charset="utf-8">
                <title>PhotoLib</title>
                <style>
                  body {
                    font-family: system-ui, sans-serif;
                    background: #1a1a1a;
                    color: #eee;
                    display: flex;
                    align-items: center;
                    justify-content: center;
                    height: 100vh;
                    margin: 0;
                    font-size: 14px;
                    text-align: center;
                  }
                </style>
                </head>
                <body>
                <div id="status" data-image-url="__IMAGE_URL__" data-gallery-id="__GALLERY_ID__">
                  Adding photo…
                </div>
                <script>
                (function () {
                  var el = document.getElementById('status');
                  var imageUrl = el.getAttribute('data-image-url');
                  var galleryId = el.getAttribute('data-gallery-id');

                  fetch('/api/Photo/from-url', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({ galleryId: galleryId, imageUrl: imageUrl })
                  })
                    .then(function (r) {
                      el.textContent = r.ok ? 'Added ✓' : ('Failed: HTTP ' + r.status);
                      if (r.ok) setTimeout(function () { window.close(); }, 900);
                    })
                    .catch(function () {
                      el.textContent = 'Network error — is the app running?';
                    });
                })();
                </script>
                </body>
                </html>
                """;

            // Substitute the safe (Guid-shaped) token first so that, even in
            // the ultra-unlikely case where the encoded image URL itself
            // contains the literal placeholder text, it can't be re-matched
            // by a later replacement.
            var html = template
                .Replace("__GALLERY_ID__", galleryId.ToString())
                .Replace("__IMAGE_URL__", encodedImageUrl);

            return Content(html, "text/html");
        }

        /// <summary>
        /// Creates a photo by downloading an image from a URL.
        /// </summary>
        /// <remarks>
        /// Called by the capture relay page (see <see cref="Capture"/>).
        /// Cross-origin CORS is enabled here too, in case a page's CSP
        /// allows fetches but the popup was blocked and a caller posts to
        /// this endpoint directly instead.
        ///
        /// Accepted risk: <c>AllowAnyOrigin</c> CORS is incompatible with
        /// credentialed requests, so this endpoint cannot carry the session
        /// cookie and stays unauthenticated - <c>[AllowAnonymous]</c>,
        /// relying only on <see cref="CreatePhotoFromUrlRequest.GalleryId"/>
        /// being an unguessable GUID, same as before authentication existed.
        /// On a Tailscale-only shared instance the residual risk is limited
        /// to one invited user guessing another's gallery id.
        /// </remarks>
        /// <param name="request">Destination gallery and image URL.</param>
        /// <response code="201">Photo created and downloaded successfully.</response>
        /// <response code="400">Invalid URL, or the URL did not return an image.</response>
        /// <response code="404">Destination gallery not found.</response>
        [HttpPost("from-url")]
        [AllowAnonymous]
        [EnableCors("BookmarkletUpload")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> CreateFromUrl([FromBody] CreatePhotoFromUrlRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            if (!Uri.TryCreate(request.ImageUrl, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                return BadRequest("imageUrl must be an absolute http(s) URL.");
            }

            if (!await GalleryExistsAsync(request.GalleryId))
                return NotFound(new { message = $"Gallery with id '{request.GalleryId}' not found." });

            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(15);
            // Some hosts (e.g. Wikimedia) reject requests with no User-Agent.
            client.DefaultRequestHeaders.UserAgent.ParseAdd("PhotoLibApp/1.0 (+http://localhost)");

            HttpResponseMessage response;
            try
            {
                response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead);
            }
            catch (Exception)
            {
                return BadRequest("Could not download the image from the given URL.");
            }

            // `using` (rather than a bare local) ensures the response - and the connection/
            // buffers it holds - is disposed on every exit path below, including the early
            // BadRequest returns.
            using (response)
            {
                if (!response.IsSuccessStatusCode)
                    return BadRequest($"The source returned HTTP {(int)response.StatusCode}.");

                var contentType = response.Content.Headers.ContentType?.MediaType;
                if (contentType == null || !contentType.StartsWith("image/"))
                    return BadRequest("The URL does not point to an image.");

                if (response.Content.Headers.ContentLength is long declaredLength &&
                    declaredLength > MaxDownloadBytes)
                {
                    return BadRequest("Image is too large.");
                }

                var fileName = Path.GetFileName(uri.LocalPath);
                var photo = new Photo
                {
                    Id = Guid.NewGuid(),
                    GalleryId = request.GalleryId,
                    Title = !string.IsNullOrWhiteSpace(request.Title)
                        ? request.Title!
                        : (!string.IsNullOrWhiteSpace(fileName) ? fileName : "Untitled"),
                    CreatedAtUtc = DateTime.UtcNow,
                    SortOrder = await NextSortOrderAsync(request.GalleryId),
                };

                Directory.CreateDirectory(_filePathHelper.GetOriginalsDirectory());
                var filePath = _filePathHelper.GetOriginalFilePath(photo.Id);

                // Stream the download straight to disk instead of buffering the whole
                // image in memory first (the previous `ReadAsByteArrayAsync` approach) -
                // that let a response with no Content-Length header balloon memory before
                // the size limit could ever reject it.
                long totalBytes;
                await using (var downloadStream = await response.Content.ReadAsStreamAsync())
                {
                    try
                    {
                        totalBytes = await _imageProcessing.SaveStreamToFileAsync(
                            downloadStream, filePath, MaxDownloadBytes);
                    }
                    catch (StreamTooLargeException)
                    {
                        return BadRequest("Image is too large.");
                    }
                }

                if (totalBytes == 0)
                {
                    System.IO.File.Delete(filePath);
                    return BadRequest("Image is empty.");
                }

                photo.HasOriginal = true;
                _imageProcessing.GenerateThumbnailAndExif(photo, filePath);

                _db.Photos.Add(photo);
                await _db.SaveChangesAsync();

                return StatusCode(StatusCodes.Status201Created, photo);
            }
        }

        /// <summary>
        /// Updates photo metadata.
        /// </summary>
        /// <param name="id">Photo identifier.</param>
        /// <param name="request"></param>
        /// <response code="204">Photo updated successfully.</response>
        /// <response code="400">Invalid request data.</response>
        /// <response code="404">Photo not found.</response>
        [HttpPut("{id:guid}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Update(
            Guid id,
            [FromBody] UpdatePhotoRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var photo = await _galleryAccess.GetOwnedPhotoAsync(id, _currentUser.UserId);

            if (photo == null)
                return NotFound();

            photo.Title = request.Title;
            photo.Description = request.Description;
            photo.UpdatedAtUtc = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            // Callers (in particular the offline mirror) need the server's
            // authoritative UpdatedAtUtc to avoid caching a stale/guessed
            // value that would later trip a false sync conflict.
            return Ok(new { updatedAtUtc = photo.UpdatedAtUtc });
        }

        /// <summary>
        /// Asks the AI to draft a title, description and tags for a photo.
        /// </summary>
        /// <remarks>
        /// This does not persist anything - the draft is returned for the
        /// caller to review and save via the normal update endpoints.
        /// </remarks>
        /// <param name="id">Photo identifier.</param>
        /// <param name="request">Requested writing style and length.</param>
        /// <param name="cancellationToken">Cancellation token for the request.</param>
        /// <response code="200">Drafted title, description and tags.</response>
        /// <response code="404">Photo not found, or has no image file to analyze.</response>
        /// <response code="429">Too many AI requests from this client recently - try again later.</response>
        /// <response code="502">The AI request failed.</response>
        /// <response code="503">AI features are disabled on this server (<c>Ai:Enabled</c> config flag).</response>
        [HttpPost("{id:guid}/generate-description")]
        [EnableRateLimiting("AiGeneration")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
        [ProducesResponseType(StatusCodes.Status502BadGateway)]
        [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
        public async Task<IActionResult> GenerateDescription(
            Guid id,
            [FromBody] GenerateDescriptionRequest request,
            CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var photo = await _galleryAccess.GetOwnedPhotoAsync(id, _currentUser.UserId);
            if (photo == null || photo.IsDeleted || (!photo.HasOriginal && !photo.HasThumbnail))
                return NotFound();

            try
            {
                var draft = await _descriptionAi.GenerateAsync(photo, request, cancellationToken);
                return Ok(draft);
            }
            catch (AiFeatureDisabledException ex)
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = ex.Message });
            }
            catch (FileNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status502BadGateway, new { message = "AI request failed: " + ex.Message });
            }
        }

        /// <summary>
        /// Replaces the full tag set of a photo.
        /// </summary>
        /// <param name="id">Photo identifier.</param>
        /// <param name="request">The complete list of tag names to attach.</param>
        /// <response code="200">Updated tag names.</response>
        /// <response code="404">Photo not found.</response>
        [HttpPut("{id:guid}/tags")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> SetTags(Guid id, [FromBody] SetTagsRequest request)
        {
            var photo = await _db.Photos
                .Include(p => p.Tags)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (photo == null || photo.IsDeleted)
                return NotFound();

            if (!await _galleryAccess.IsOwnedAsync(photo.GalleryId, _currentUser.UserId))
                return NotFound();

            await _tagResolver.ApplyAsync(photo, request.TagNames);
            await _db.SaveChangesAsync();

            return Ok(new
            {
                tagNames = photo.Tags.Select(t => t.Name),
                updatedAtUtc = photo.UpdatedAtUtc,
            });
        }

        /// <summary>
        /// Moves a photo into another gallery.
        /// </summary>
        /// <param name="id">Photo identifier.</param>
        /// <param name="request">Destination gallery.</param>
        /// <response code="204">Photo moved successfully.</response>
        /// <response code="400">Invalid request data.</response>
        /// <response code="404">Photo or destination gallery not found.</response>
        [HttpPost("{id:guid}/move")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Move(Guid id, [FromBody] MovePhotoRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var photo = await _galleryAccess.GetOwnedPhotoAsync(id, _currentUser.UserId);
            if (photo == null || photo.IsDeleted)
                return NotFound();

            if (!await _galleryAccess.IsOwnedAsync(request.GalleryId, _currentUser.UserId))
                return NotFound(new { message = $"Gallery with id '{request.GalleryId}' not found." });

            photo.SortOrder = await NextSortOrderAsync(request.GalleryId);
            photo.GalleryId = request.GalleryId;
            photo.UpdatedAtUtc = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            return NoContent();
        }

        /// <summary>
        /// Copies a photo (metadata and files) into another gallery.
        /// </summary>
        /// <param name="id">Photo identifier.</param>
        /// <param name="request">Destination gallery.</param>
        /// <response code="201">Photo copied successfully.</response>
        /// <response code="400">Invalid request data.</response>
        /// <response code="404">Photo or destination gallery not found.</response>
        [HttpPost("{id:guid}/copy")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<Photo>> Copy(Guid id, [FromBody] MovePhotoRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var source = await _galleryAccess.GetOwnedPhotoAsync(id, _currentUser.UserId);
            if (source == null || source.IsDeleted)
                return NotFound();

            if (!await _galleryAccess.IsOwnedAsync(request.GalleryId, _currentUser.UserId))
                return NotFound(new { message = $"Gallery with id '{request.GalleryId}' not found." });

            var copy = new Photo
            {
                Id = Guid.NewGuid(),
                GalleryId = request.GalleryId,
                Title = source.Title,
                Description = source.Description,
                ExifJson = source.ExifJson,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow,
                SortOrder = await NextSortOrderAsync(request.GalleryId),
            };

            if (source.HasOriginal)
            {
                Directory.CreateDirectory(_filePathHelper.GetOriginalsDirectory());
                System.IO.File.Copy(
                    _filePathHelper.GetOriginalFilePath(source.Id),
                    _filePathHelper.GetOriginalFilePath(copy.Id));
                copy.HasOriginal = true;
            }

            if (source.HasThumbnail)
            {
                Directory.CreateDirectory(_filePathHelper.GetThumbnailsDirectory());
                System.IO.File.Copy(
                    _filePathHelper.GetThumbnailFilePath(source.Id),
                    _filePathHelper.GetThumbnailFilePath(copy.Id));
                copy.HasThumbnail = true;
            }

            _db.Photos.Add(copy);
            await _db.SaveChangesAsync();

            return StatusCode(StatusCodes.Status201Created, copy);
        }

        /// <summary>
        /// Soft-deletes a photo by marking it as deleted.
        /// </summary>
        /// <param name="id">Photo identifier.</param>
        /// <response code="204">Photo marked as deleted.</response>
        /// <response code="404">Photo not found.</response>
        [HttpDelete("{id:guid}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Delete(Guid id)
        {
            var photo = await _galleryAccess.GetOwnedPhotoAsync(id, _currentUser.UserId);

            if (photo == null || photo.IsDeleted)
                return NotFound();

            photo.IsDeleted = true;
            photo.UpdatedAtUtc = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            return NoContent();
        }

        /// <summary>
        /// Persists a new drag-and-drop display order for the photos within a gallery.
        /// </summary>
        /// <param name="request">Gallery and photo identifiers in the desired order.</param>
        /// <response code="204">Order saved.</response>
        /// <response code="400">Invalid request data.</response>
        [HttpPut("reorder")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Reorder([FromBody] ReorderPhotosRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            if (!await _galleryAccess.IsOwnedAsync(request.GalleryId, _currentUser.UserId))
                return NotFound();

            var photos = await _db.Photos
                .Where(p => p.GalleryId == request.GalleryId && request.PhotoIds.Contains(p.Id))
                .ToListAsync();

            SortOrderHelper.Apply(photos, request.PhotoIds, p => p.Id, (p, i) => p.SortOrder = i);

            await _db.SaveChangesAsync();

            return NoContent();
        }

#if DEBUG
        /// <summary>
        /// ADMIN: Permanently deletes a photo and its files.
        /// </summary>
        /// <param name="id">Photo identifier.</param>
        /// <response code="204">Photo permanently deleted.</response>
        /// <response code="404">Photo not found.</response>
        [HttpDelete("admin/{id:guid}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> AdminHardDelete(Guid id)
        {
            var photo = await _db.Photos.FindAsync(id);

            if (photo == null)
                return NotFound();

            // IO-operations are isolated in the helper
            _filePathHelper.DeleteOriginal(id);
            _filePathHelper.DeleteThumbnail(id);

            _db.Photos.Remove(photo);
            await _db.SaveChangesAsync();

            return NoContent();
        }
#endif

        /// <summary>Checks that a (non-deleted) gallery with this id exists.</summary>
        private Task<bool> GalleryExistsAsync(Guid galleryId)
        {
            return _db.Galleries
                .AsNoTracking()
                .AnyAsync(g => g.Id == galleryId && !g.IsDeleted);
        }

        /// <summary>The SortOrder a newly added/moved photo should get to land at the end of the gallery.</summary>
        private async Task<int> NextSortOrderAsync(Guid galleryId)
        {
            var max = await _db.Photos
                .Where(p => p.GalleryId == galleryId)
                .Select(p => (int?)p.SortOrder)
                .MaxAsync() ?? -1;

            return max + 1;
        }
    }
}
