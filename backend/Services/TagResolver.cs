using Microsoft.EntityFrameworkCore;
using PhotoLibApi.Data;
using PhotoLibApi.Models;

namespace PhotoLibApi.Services
{
    /// <summary>
    /// Resolves tag names to <see cref="Tag"/> entities, creating any that don't exist yet.
    /// Matching is case-insensitive so "Ships" and "ships" reuse the same tag.
    /// </summary>
    public class TagResolver
    {
        private readonly PhotoDbContext _db;

        public TagResolver(PhotoDbContext db)
        {
            _db = db;
        }

        /// <summary>
        /// Resolves tag names to tracked <see cref="Tag"/> entities, creating any that don't
        /// exist yet. Bounded by the total number of distinct tags in the whole app (not by
        /// this call's input), which is expected to stay small, so loading the full table is
        /// the simplest correct option here.
        /// </summary>
        /// <remarks>
        /// This intentionally does <em>not</em> filter server-side (e.g. via a translated
        /// <c>WHERE</c> on a lowered column) or use <c>AsNoTracking()</c>:
        /// <list type="bullet">
        /// <item>SQLite's <c>LOWER()</c> only folds ASCII, so a server-side case-insensitive
        /// filter would silently stop matching existing non-ASCII (e.g. Cyrillic) tag names
        /// that only differ by case - unlike the <see cref="StringComparer.OrdinalIgnoreCase"/>
        /// comparison used below, which folds correctly.</item>
        /// <item>The returned entities are assigned directly to a tracked photo/gallery's
        /// <c>Tags</c> navigation before <c>SaveChanges</c>; if they came back untracked,
        /// EF Core would treat pre-existing tags as new rows and attempt to re-insert them.</item>
        /// </list>
        /// </remarks>
        public async Task<List<Tag>> ResolveAsync(IEnumerable<string> tagNames)
        {
            var normalized = tagNames
                .Select(t => t.Trim())
                .Where(t => t.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (normalized.Count == 0)
                return new List<Tag>();

            var allTags = await _db.Tags.ToListAsync();
            var result = new List<Tag>();

            foreach (var name in normalized)
            {
                var tag = allTags.FirstOrDefault(t =>
                    string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase));

                if (tag == null)
                {
                    tag = new Tag { Id = Guid.NewGuid(), Name = name };
                    _db.Tags.Add(tag);
                    allTags.Add(tag);
                }

                result.Add(tag);
            }

            return result;
        }

        /// <summary>
        /// Resolves <paramref name="tagNames"/> and assigns them to <paramref name="entity"/>,
        /// bumping its <see cref="ITaggable.UpdatedAtUtc"/>. Does not save changes.
        /// </summary>
        public async Task ApplyAsync(ITaggable entity, IEnumerable<string> tagNames)
        {
            entity.Tags = await ResolveAsync(tagNames);
            entity.UpdatedAtUtc = DateTime.UtcNow;
        }
    }
}
