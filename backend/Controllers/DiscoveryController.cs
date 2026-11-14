using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PhotoLibApi.Services;

namespace PhotoLibApi.Controllers
{
    /// <summary>
    /// Serves the Discovery feed - a live, weighted-random sample of the
    /// caller's own photos, nudged toward tags they've viewed most. See
    /// <see cref="DiscoveryFeedService"/> for the sampling logic.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class DiscoveryController : ControllerBase
    {
        private const int MinCount = 1;
        private const int MaxCount = 60;

        private readonly DiscoveryFeedService _feed;
        private readonly CurrentUserService _currentUser;

        /// <param name="feed">Builds the weighted-random photo sample.</param>
        /// <param name="currentUser">Resolves the signed-in user's id.</param>
        public DiscoveryController(DiscoveryFeedService feed, CurrentUserService currentUser)
        {
            _feed = feed;
            _currentUser = currentUser;
        }

        /// <summary>
        /// Returns a fresh batch of Discovery photos. Each call re-samples
        /// live - there's no stored/stable feed to page through.
        /// </summary>
        /// <param name="count">How many photos to return (clamped to 1-60).</param>
        /// <param name="exclude">
        /// Comma-separated photo ids already shown in the current client
        /// session, to reduce (not guarantee-free) repeats. Unparsable
        /// fragments are silently skipped - this is an optimization hint,
        /// not a validated input.
        /// </param>
        /// <response code="200">A batch of photos.</response>
        [HttpGet("feed")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetFeed([FromQuery] int count = 30, [FromQuery] string? exclude = null)
        {
            var clampedCount = Math.Clamp(count, MinCount, MaxCount);
            var excludeIds = ParseGuids(exclude);

            var photos = await _feed.GetFeedAsync(_currentUser.UserId, clampedCount, excludeIds);

            return Ok(photos.Select(p => new
            {
                p.Id,
                p.Title,
                p.Description,
                p.HasThumbnail,
                p.UpdatedAtUtc,
                Tags = p.TagNames,
                p.OwnerName,
            }));
        }

        private static HashSet<Guid> ParseGuids(string? csv)
        {
            var ids = new HashSet<Guid>();
            if (string.IsNullOrWhiteSpace(csv))
                return ids;

            foreach (var part in csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (Guid.TryParse(part, out var id))
                    ids.Add(id);
            }

            return ids;
        }
    }
}
