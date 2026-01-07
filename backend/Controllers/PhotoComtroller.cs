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
        private readonly PhotoDbContext _db;
        private readonly PhotoFilePathHelper _filePathHelper;


        public PhotoController(PhotoDbContext db, IConfiguration configuration)
        {
            _db = db;

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
                .OrderBy(p => p.CreatedAtUtc)
                // Minimal projection for gallery view:
                // only data required to render thumbnails list
                .Select(p => new
                {
                    p.Id,
                    p.Title,
                    p.HasThumbnail
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
                    p.IsDeleted
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
            var galleryExists = await _db.Galleries
                .AsNoTracking()
                .AnyAsync(g => g.Id == request.GalleryId);

            if (!galleryExists)
                return NotFound($"Gallery with id '{request.GalleryId}' not found.");

            var photo = new Photo
            {
                Id = Guid.NewGuid(),
                GalleryId = request.GalleryId,
                Title = request.Title,
                Description = request.Description,
                CreatedAtUtc = DateTime.UtcNow
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
        public async Task<IActionResult> Upload(Guid id, IFormFile file)
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
            await using var stream = System.IO.File.Create(filePath);
            await file.CopyToAsync(stream);

            // mark original as existing
            photo.HasOriginal = true;

            // Generate and save thumbnail
            Directory.CreateDirectory(_filePathHelper.GetThumbnailsDirectory());
            var thumbnailPath = _filePathHelper.GetThumbnailFilePath(id);
            // Generate thumbnail
            using (var image = Image.Load(filePath))
            {
                image.Mutate(x => x.Resize(new ResizeOptions
                {
                    Size = new Size(300, 300),
                    Mode = ResizeMode.Max
                }));

                image.Save(thumbnailPath);
            }

            // mark thumbnail as existing
            photo.HasThumbnail = true;
            await _db.SaveChangesAsync();

            return NoContent();
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

            await _db.SaveChangesAsync();

            return NoContent();
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

    }
}
