using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhotoLibApi.Data;
using PhotoLibApi.Models;

namespace PhotoLibApi.Controllers
{
    /// <summary>
    /// Controller to manage user galleries.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class GalleryController : ControllerBase
    {
        private readonly PhotoDbContext _db;

        public GalleryController(PhotoDbContext db)
        {
            _db = db;
        }

        /// <summary>
        /// Returns the list of galleries for the current user.
        /// </summary>
        /// <remarks>
        /// Uses the current authenticated user if available.
        /// For local testing (no auth), it returns galleries where OwnerId is null.
        /// </remarks>
        /// <response code="200">A list of galleries belonging to the user.</response>
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<ActionResult<IEnumerable<Gallery>>> GetAll()
        {
            // simple owner resolution: use authenticated user name or null for local testing
            var ownerId = User?.Identity?.Name;

            var galleries = await _db.Galleries
                .AsNoTracking()
                .Where(g => g.OwnerId == ownerId && !g.IsDeleted)
                .OrderBy(g => g.CreatedAtUtc)
                .ToListAsync();

            return Ok(galleries);
        }

#if DEBUG
        /// <summary>
        /// DEV: Returns all galleries, including deleted ones.
        /// </summary>
        [HttpGet("dev")]
        public async Task<IActionResult> DevGetAll()
        {
            var ownerId = User?.Identity?.Name;

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
            var ownerId = User?.Identity?.Name;

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
        /// <param name="id">Gallery identifier.</param>
        /// <response code="200">Gallery metadata returned.</response>
        /// <response code="404">Gallery not found.</response>
        [HttpGet("{id:guid}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetById(Guid id)
        {
            var ownerId = User?.Identity?.Name;

            var gallery = await _db.Galleries
                .AsNoTracking()
                .Where(g => g.Id == id && g.OwnerId == ownerId && !g.IsDeleted)
                .Select(g => new
                {
                    g.Id,
                    g.Title,
                    g.CreatedAtUtc,
                    g.UpdatedAtUtc
                })
                .FirstOrDefaultAsync();

            if (gallery == null)
                return NotFound();

            return Ok(gallery);
        }

        /// <summary>
        /// Creates a new gallery.
        /// </summary>
        ///  <remarks>
        /// Currently, the gallery is created without authentication.
        /// OwnerId will be null until authentication is added.
        /// </remarks>
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

            var gallery = new Gallery
            {
                Id = Guid.NewGuid(),
                Title = request.Title,
                OwnerId = User?.Identity?.Name,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };

            _db.Galleries.Add(gallery);
            await _db.SaveChangesAsync();

            return CreatedAtAction(nameof(GetAll), gallery);
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

            if (gallery == null || gallery.IsDeleted)
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

            if (gallery == null)
                return NotFound();

            _db.Galleries.Remove(gallery);
            await _db.SaveChangesAsync();

            return NoContent();
        }
#endif

    }
}
