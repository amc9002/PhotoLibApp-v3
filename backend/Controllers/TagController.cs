using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhotoLibApi.Data;

namespace PhotoLibApi.Controllers
{
    /// <summary>
    /// Read-only access to the full set of known tags, used for autocomplete.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class TagController : ControllerBase
    {
        private readonly PhotoDbContext _db;

        public TagController(PhotoDbContext db)
        {
            _db = db;
        }

        /// <summary>
        /// Returns all known tags, ordered by name.
        /// </summary>
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAll()
        {
            var tags = await _db.Tags
                .AsNoTracking()
                .OrderBy(t => t.Name)
                .Select(t => new { t.Id, t.Name })
                .ToListAsync();

            return Ok(tags);
        }
    }
}
