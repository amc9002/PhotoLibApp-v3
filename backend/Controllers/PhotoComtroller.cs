using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhotoLibApi.Data;
using PhotoLibApi.Models;
using PhotoLibApi.Services;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace PhotoLibApi.Controllers
{
    /// <summary>
    /// Controller for working with photos.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class PhotoController : ControllerBase
    {
        /// <summary>Upper bound on how many bytes a from-url download may use.</summary>
        private const long MaxDownloadBytes = 25 * 1024 * 1024;

        private readonly PhotoDbContext _db;
        private readonly PhotoFilePathHelper _filePathHelper;
        private readonly TagResolver _tagResolver;
        private readonly IHttpClientFactory _httpClientFactory;


        public PhotoController(
            PhotoDbContext db,
            IConfiguration configuration,
            TagResolver tagResolver,
            IHttpClientFactory httpClientFactory)
        {
            _db = db;
            _tagResolver = tagResolver;
            _httpClientFactory = httpClientFactory;

            var photosRoot = Path.Combine(
                Directory.GetCurrentDirectory(),
                configuration["Storage:PhotosPath"]!);

            _filePathHelper = new PhotoFilePathHelper(photosRoot);
        }

        /// <summary>
        /// Returns photos belonging to a gallery.
        /// </summary>
        /// <param name="galleryId">Gallery identifier.</param>
        /// <response code="200">List of photos.</response>
        [HttpGet("by-gallery/{galleryId:guid}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<ActionResult<IEnumerable<Photo>>> GetByGallery(Guid galleryId)
        {
            var photos = await _db.Photos
                .AsNoTracking()
                .Where(p => p.GalleryId == galleryId && !p.IsDeleted)
                .OrderBy(p => p.SortOrder)
                // Minimal projection for gallery view:
                // only data required to render thumbnails list
                .Select(p => new
                {
                    p.Id,
                    p.Title,
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
        public IActionResult GetFile(Guid id)
        {

            // Check that photo exists and has original    
            var photo = _db.Photos.Find(id);
            if (photo == null || !photo.HasOriginal)
                return NotFound();

            var filePath = _filePathHelper.GetOriginalFilePath(id);

            // Check if file exists
            if (!System.IO.File.Exists(filePath))
                return NotFound();

            // Return file as-is
            return PhysicalFile(
                filePath,
                "image/jpeg");
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
        public IActionResult GetThumbnail(Guid id)
        {
            // Check that photo exists and has thumbnail
            var photo = _db.Photos.Find(id);
            if (photo == null || !photo.HasThumbnail)
                return NotFound();

            var filePath = _filePathHelper.GetThumbnailFilePath(id);
            // Check if file exists
            if (!System.IO.File.Exists(filePath))
                return NotFound();

            return PhysicalFile(
                filePath,
                "image/jpeg");
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

            // 2️⃣ Check that the gallery exists
            if (!await GalleryExistsAsync(request.GalleryId))
                return NotFound($"Gallery with id '{request.GalleryId}' not found.");

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
            try
            {
                // Validate file
                if (file == null || file.Length == 0)
                    return BadRequest("File is required.");

                // Check: photo exists in DB
                var photo = await _db.Photos.FindAsync(id);
                if (photo == null)
                    return NotFound();

                // Ensure originals directory exists
                Directory.CreateDirectory(_filePathHelper.GetOriginalsDirectory());
                var filePath = _filePathHelper.GetOriginalFilePath(id);

                // Save file to disk
                await using (var stream = System.IO.File.Create(filePath))
                {
                    await file.CopyToAsync(stream);
                }

                // mark original as existing
                photo.HasOriginal = true;

                GenerateThumbnailAndExif(photo, filePath);

                await _db.SaveChangesAsync();

                return NoContent();
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }

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
        /// </remarks>
        /// <param name="request">Destination gallery and image URL.</param>
        /// <response code="201">Photo created and downloaded successfully.</response>
        /// <response code="400">Invalid URL, or the URL did not return an image.</response>
        /// <response code="404">Destination gallery not found.</response>
        [HttpPost("from-url")]
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
                return NotFound($"Gallery with id '{request.GalleryId}' not found.");

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

            var bytes = await response.Content.ReadAsByteArrayAsync();
            if (bytes.Length == 0 || bytes.Length > MaxDownloadBytes)
                return BadRequest("Image is empty or too large.");

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
            await System.IO.File.WriteAllBytesAsync(filePath, bytes);
            photo.HasOriginal = true;

            GenerateThumbnailAndExif(photo, filePath);

            _db.Photos.Add(photo);
            await _db.SaveChangesAsync();

            return StatusCode(StatusCodes.Status201Created, photo);
        }

        /// <summary>
        /// Generates a thumbnail and reads EXIF metadata for an original file
        /// already saved on disk, updating the given photo's flags in place.
        /// </summary>
        private void GenerateThumbnailAndExif(Photo photo, string originalFilePath)
        {
            Directory.CreateDirectory(_filePathHelper.GetThumbnailsDirectory());
            var thumbnailPath = _filePathHelper.GetThumbnailFilePath(photo.Id);

            using (var image = Image.Load(originalFilePath))
            {
                image.Mutate(x => x.Resize(new ResizeOptions
                {
                    Size = new Size(300, 300),
                    Mode = ResizeMode.Max
                }));

                image.Save(thumbnailPath);
            }

            photo.HasThumbnail = true;

            var exif = ExifReader.Read(originalFilePath);
            photo.ExifJson = exif.Json;
            photo.Latitude = exif.Latitude;
            photo.Longitude = exif.Longitude;
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
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Update(
            Guid id,
            [FromBody] UpdatePhotoRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var photo = await _db.Photos.FindAsync(id);

            if (photo == null)
                return NotFound();

            photo.Title = request.Title;
            photo.Description = request.Description;
            photo.UpdatedAtUtc = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            return NoContent();
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

            await _tagResolver.ApplyAsync(photo, request.TagNames);
            await _db.SaveChangesAsync();

            return Ok(photo.Tags.Select(t => t.Name));
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

            var photo = await _db.Photos.FindAsync(id);
            if (photo == null || photo.IsDeleted)
                return NotFound();

            if (!await GalleryExistsAsync(request.GalleryId))
                return NotFound($"Gallery with id '{request.GalleryId}' not found.");

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

            var source = await _db.Photos.FindAsync(id);
            if (source == null || source.IsDeleted)
                return NotFound();

            if (!await GalleryExistsAsync(request.GalleryId))
                return NotFound($"Gallery with id '{request.GalleryId}' not found.");

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
            var photo = await _db.Photos.FindAsync(id);

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
        public async Task<IActionResult> Reorder([FromBody] ReorderPhotosRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var photos = await _db.Photos
                .Where(p => p.GalleryId == request.GalleryId && request.PhotoIds.Contains(p.Id))
                .ToListAsync();

            var photoById = photos.ToDictionary(p => p.Id);

            for (var i = 0; i < request.PhotoIds.Count; i++)
            {
                if (photoById.TryGetValue(request.PhotoIds[i], out var photo))
                {
                    photo.SortOrder = i;
                }
            }

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
