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
