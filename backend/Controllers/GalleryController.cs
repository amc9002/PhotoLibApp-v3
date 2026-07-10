using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhotoLibApi.Data;
using PhotoLibApi.Models;
using PhotoLibApi.Services;

namespace PhotoLibApi.Controllers
{
    /// <summary>
    /// Controller to manage user galleries.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class GalleryController : ControllerBase
    {
        private readonly PhotoDbContext _db;
        private readonly TagResolver _tagResolver;
        private readonly CurrentUserService _currentUser;

        public GalleryController(PhotoDbContext db, TagResolver tagResolver, CurrentUserService currentUser)
        {
            _db = db;
            _tagResolver = tagResolver;
            _currentUser = currentUser;
        }

        /// <summary>
        /// Returns the list of galleries for the current user.
        /// </summary>
        /// <response code="200">A list of galleries belonging to the user.</response>
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAll()
        {
            var ownerId = _currentUser.UserId;

            var galleries = await _db.Galleries
                .AsNoTracking()
                .Where(g => g.OwnerId == ownerId && !g.IsDeleted)
                .OrderBy(g => g.SortOrder)
                .Select(g => new
                {
                    g.Id,
                    g.Title,
                    g.Description,
                    g.IsDeleted,
                    g.CreatedAtUtc,
                    g.UpdatedAtUtc,
                    Tags = g.Tags.Select(t => t.Name)
                })
                .ToListAsync();

            return Ok(galleries);
        }

        /// <summary>
        /// Persists a new drag-and-drop display order for the user's galleries.
        /// </summary>
        /// <param name="request">Gallery identifiers in the desired order.</param>
        /// <response code="204">Order saved.</response>
        /// <response code="400">Invalid request data.</response>
        [HttpPut("reorder")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Reorder([FromBody] ReorderGalleriesRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var ownerId = _currentUser.UserId;

            var galleries = await _db.Galleries
                .Where(g => g.OwnerId == ownerId && request.GalleryIds.Contains(g.Id))
                .ToListAsync();

            var galleryById = galleries.ToDictionary(g => g.Id);

            for (var i = 0; i < request.GalleryIds.Count; i++)
            {
                if (galleryById.TryGetValue(request.GalleryIds[i], out var gallery))
                {
                    gallery.SortOrder = i;
                }
            }

            await _db.SaveChangesAsync();

            return NoContent();
        }

#if DEBUG
        /// <summary>
        /// DEV: Returns all galleries, including deleted ones.
        /// </summary>
        [HttpGet("dev")]
        public async Task<IActionResult> DevGetAll()
        {
            var ownerId = _currentUser.UserId;

            var galleries = await _db.Galleries
                .AsNoTracking()
                .Where(g => g.OwnerId == ownerId)
                .OrderBy(g => g.CreatedAtUtc)
                .Select(g => new
                {
                    g.Id,
                    g.Title,
                    g.IsDeleted,
                    g.CreatedAtUtc,
                    g.UpdatedAtUtc
                })
                .ToListAsync();

            return Ok(galleries);
        }
#endif

#if DEBUG
        /// <summary>
        /// DEV: Returns deleted galleries only.
        /// </summary>
        [HttpGet("dev/deleted")]
        public async Task<IActionResult> DevGetDeleted()
        {
            var ownerId = _currentUser.UserId;

            var galleries = await _db.Galleries
                .AsNoTracking()
                .Where(g => g.OwnerId == ownerId && g.IsDeleted)
                .OrderBy(g => g.UpdatedAtUtc)
                .Select(g => new
                {
                    g.Id,
                    g.Title,
                    g.UpdatedAtUtc
                })
                .ToListAsync();

            return Ok(galleries);
        }
#endif

        /// <summary>
        /// Returns gallery metadata by identifier.
        /// </summary>
        /// <remarks>
        /// Unlike the list endpoints, this does not filter out soft-deleted
        /// galleries - it returns the row with <c>isDeleted: true</c>
        /// instead, matching <see cref="PhotoController.GetById"/>. Offline
        /// sync's conflict check relies on being able to tell "deleted"
        /// apart from "never existed" for both entity types the same way.
        /// </remarks>
        /// <param name="id">Gallery identifier.</param>
        /// <response code="200">Gallery metadata returned.</response>
        /// <response code="404">Gallery not found.</response>
        [HttpGet("{id:guid}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetById(Guid id)
        {
            var ownerId = _currentUser.UserId;

            var gallery = await _db.Galleries
                .AsNoTracking()
                .Where(g => g.Id == id && g.OwnerId == ownerId)
                .Select(g => new
                {
                    g.Id,
                    g.Title,
                    g.Description,
                    g.CreatedAtUtc,
                    g.UpdatedAtUtc,
                    g.IsDeleted,
                    Tags = g.Tags.Select(t => t.Name)
                })
                .FirstOrDefaultAsync();

            if (gallery == null)
                return NotFound();

            return Ok(gallery);
        }

        /// <summary>
        /// Creates a new gallery, owned by the current user.
        /// </summary>
        /// <response code="201">Gallery created successfully.</response>
        /// <response code="400">Invalid request data.</response>
        [HttpPost]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<Gallery>> Create(
            [FromBody] CreateGalleryRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var ownerId = _currentUser.UserId;

            // Idempotent replay: a sync retry with the same ClientTempId
            // returns the row that already exists instead of duplicating it.
            if (!string.IsNullOrEmpty(request.ClientTempId))
            {
                var existing = await _db.Galleries
                    .FirstOrDefaultAsync(g =>
                        g.OwnerId == ownerId &&
                        g.ClientTempId == request.ClientTempId &&
                        !g.IsDeleted);

                if (existing != null)
                    return StatusCode(StatusCodes.Status201Created, existing);
            }

            var nextSortOrder = await _db.Galleries
                .Where(g => g.OwnerId == ownerId)
                .Select(g => (int?)g.SortOrder)
                .MaxAsync() ?? -1;

            var gallery = new Gallery
            {
                Id = Guid.NewGuid(),
                Title = request.Title,
                OwnerId = ownerId,
                ClientTempId = request.ClientTempId,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow,
                SortOrder = nextSortOrder + 1,
            };

            _db.Galleries.Add(gallery);
            await _db.SaveChangesAsync();

            return CreatedAtAction(nameof(GetAll), gallery);
        }

        /// <summary>
        /// Updates gallery metadata.
        /// </summary>
        /// <param name="id">Gallery identifier.</param>
        /// <param name="request"></param>
        /// <response code="204">Gallery updated successfully.</response>
        /// <response code="400">Invalid request data.</response>
        /// <response code="404">Gallery not found.</response>
        [HttpPut("{id:guid}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Update(
            Guid id,
            [FromBody] UpdateGalleryRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var gallery = await _db.Galleries.FindAsync(id);

            if (gallery == null || gallery.IsDeleted || gallery.OwnerId != _currentUser.UserId)
                return NotFound();

            gallery.Title = request.Title;
            gallery.Description = request.Description;
            gallery.UpdatedAtUtc = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            // Callers (in particular the offline mirror) need the server's
            // authoritative UpdatedAtUtc to avoid caching a stale/guessed
            // value that would later trip a false sync conflict.
            return Ok(new { updatedAtUtc = gallery.UpdatedAtUtc });
        }

        /// <summary>
        /// Replaces the full tag set of a gallery.
        /// </summary>
        /// <param name="id">Gallery identifier.</param>
        /// <param name="request">The complete list of tag names to attach.</param>
        /// <response code="200">Updated tag names.</response>
        /// <response code="404">Gallery not found.</response>
        [HttpPut("{id:guid}/tags")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> SetTags(Guid id, [FromBody] SetTagsRequest request)
        {
            var gallery = await _db.Galleries
                .Include(g => g.Tags)
                .FirstOrDefaultAsync(g => g.Id == id);

            if (gallery == null || gallery.IsDeleted || gallery.OwnerId != _currentUser.UserId)
                return NotFound();

            await _tagResolver.ApplyAsync(gallery, request.TagNames);
            await _db.SaveChangesAsync();

            return Ok(new
            {
                tagNames = gallery.Tags.Select(t => t.Name),
                updatedAtUtc = gallery.UpdatedAtUtc,
            });
        }

        /// <summary>
        /// Soft-deletes a gallery by marking it as deleted.
        /// </summary>
        /// <param name="id">Gallery identifier.</param>
        /// <response code="204">Gallery deleted successfully.</response>
        /// <response code="404">Gallery not found.</response>
        [HttpDelete("{id:guid}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Delete(Guid id)
        {
            var gallery = await _db.Galleries.FindAsync(id);

            if (gallery == null || gallery.IsDeleted || gallery.OwnerId != _currentUser.UserId)
                return NotFound();

            gallery.IsDeleted = true;
            gallery.UpdatedAtUtc = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            return NoContent();
        }

#if DEBUG
        /// <summary>
        /// ADMIN: Permanently deletes a gallery.
        /// </summary>
        /// <param name="id">Gallery identifier.</param>
        /// <response code="204">Gallery permanently deleted.</response>
        /// <response code="404">Gallery not found.</response>
        [HttpDelete("admin/{id:guid}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> AdminHardDelete(Guid id)
        {
            var gallery = await _db.Galleries.FindAsync(id);

            if (gallery == null || gallery.OwnerId != _currentUser.UserId)
                return NotFound();

            _db.Galleries.Remove(gallery);
            await _db.SaveChangesAsync();

            return NoContent();
        }
#endif

    }
}
