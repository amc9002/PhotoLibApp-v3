using Microsoft.EntityFrameworkCore;
using PhotoLibApi.Data;

namespace PhotoLibApi.Services
{
    /// <summary>
    /// Builds the Discovery feed: a live, weighted-random sample of the
    /// user's own photos, nudged toward tags they've actually opened in the
    /// viewer before (tracked via <see cref="Models.PhotoView"/>). Nothing
    /// is pre-computed or stored - every call re-samples from scratch, so
    /// the feed reacts to new views immediately.
    /// </summary>
    public class DiscoveryFeedService
    {
        /// <summary>
        /// How much each prior view of a tag adds to that tag's weight
        /// multiplier. Tunable - higher values personalize faster.
        /// </summary>
        private const double BoostPerView = 0.15;

        /// <summary>
        /// Upper bound on how much a photo's tags can boost its selection
        /// weight, on top of the base weight of 1.0. Keeps the feed from
        /// ever collapsing to "only your most-viewed theme" - every photo
        /// always has at least its base weight, so nothing is ever
        /// excluded outright.
        /// </summary>
        private const double MaxTagBoost = 3.0;

        private readonly PhotoDbContext _db;
        private readonly GalleryAccessService _galleryAccess;

        /// <param name="db">Database context used for candidate and view-history queries.</param>
        /// <param name="galleryAccess">Supplies the owned-photos candidate pool.</param>
        public DiscoveryFeedService(PhotoDbContext db, GalleryAccessService galleryAccess)
        {
            _db = db;
            _galleryAccess = galleryAccess;
        }

        /// <summary>
        /// Samples up to <paramref name="count"/> photos owned by
        /// <paramref name="userId"/>, weighted toward tags they've viewed
        /// most. <paramref name="excludeIds"/> (photos already shown in the
        /// current client-side session) are left out of the pool when
        /// there's enough left without them - otherwise ignored, so a small
        /// library can still fill a full page even if everything has
        /// already been shown once.
        /// </summary>
        public async Task<List<CandidatePhoto>> GetFeedAsync(Guid userId, int count, HashSet<Guid> excludeIds)
        {
            var candidates = await _galleryAccess.OwnedPhotos(userId)
                .Select(p => new CandidatePhoto
                {
                    Id = p.Id,
                    Title = p.Title,
                    Description = p.Description,
                    HasThumbnail = p.HasThumbnail,
                    UpdatedAtUtc = p.UpdatedAtUtc,
                    TagIds = p.Tags.Select(t => t.Id).ToList(),
                    TagNames = p.Tags.Select(t => t.Name).ToList(),
                })
                .ToListAsync();

            if (candidates.Count == 0)
                return candidates;

            // Every candidate here is owned by userId (OwnedPhotos' contract),
            // so one lookup covers the whole batch - once Discovery mixes in
            // other users' public photos, this will need to become a
            // per-gallery-owner join instead.
            var ownerName = await _db.Users
                .Where(u => u.Id == userId)
                .Select(u => u.Name)
                .FirstOrDefaultAsync() ?? "";

            foreach (var candidate in candidates)
                candidate.OwnerName = ownerName;

            var tagViewCounts = await _db.PhotoViews
                .Where(v => v.UserId == userId)
                .Join(_db.Photos, v => v.PhotoId, p => p.Id, (v, p) => p)
                .SelectMany(p => p.Tags.Select(t => t.Id))
                .GroupBy(tagId => tagId)
                .Select(g => new { TagId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.TagId, x => x.Count);

            var pool = candidates.Where(c => !excludeIds.Contains(c.Id)).ToList();
            if (pool.Count < count)
                pool = candidates;

            double Weight(CandidatePhoto photo)
            {
                var boost = photo.TagIds.Sum(id => tagViewCounts.TryGetValue(id, out var views) ? views * BoostPerView : 0);
                return 1.0 + Math.Min(boost, MaxTagBoost);
            }

            // Efraimidis-Spirakis weighted random sampling without
            // replacement: each item gets a key of -ln(U)/weight for a
            // fresh uniform random U, and the N smallest keys win. Higher
            // weight -> key distribution skews lower -> more likely picked,
            // without needing to build a cumulative-weight table.
            var rng = Random.Shared;
            return pool
                .Select(c => (Photo: c, Key: -Math.Log(rng.NextDouble()) / Weight(c)))
                .OrderBy(x => x.Key)
                .Take(count)
                .Select(x => x.Photo)
                .ToList();
        }

        /// <summary>
        /// Minimal photo shape the feed needs - the same fields
        /// <see cref="Controllers.PhotoController.GetByGallery"/> already
        /// projects, plus <see cref="OwnerName"/> for Discovery's own
        /// hover caption (the frontend's <c>DiscoveryPhotoDto</c> extends
        /// <c>PhotoListItemDto</c> with that one extra field).
        /// </summary>
        public class CandidatePhoto
        {
            public Guid Id { get; set; }
            public string Title { get; set; } = "";
            public string? Description { get; set; }
            public bool HasThumbnail { get; set; }
            public DateTime UpdatedAtUtc { get; set; }
            public List<Guid> TagIds { get; set; } = new();
            public List<string> TagNames { get; set; } = new();
            public string OwnerName { get; set; } = "";
        }
    }
}
