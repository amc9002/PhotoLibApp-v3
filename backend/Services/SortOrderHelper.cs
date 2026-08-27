using System;
using System.Collections.Generic;
using System.Linq;

namespace PhotoLibApi.Services
{
    /// <summary>
    /// Shared helper for persisting a drag-and-drop reorder. Used by both
    /// <see cref="Controllers.GalleryController.Reorder"/> and
    /// <see cref="Controllers.PhotoController.Reorder"/>, which otherwise
    /// each re-implemented the same "id list -> dense SortOrder" mapping.
    /// </summary>
    public static class SortOrderHelper
    {
        /// <summary>
        /// Assigns a dense, zero-based <c>SortOrder</c> to each of
        /// <paramref name="entities"/> based on its position in
        /// <paramref name="orderedIds"/>. Ids in <paramref name="orderedIds"/>
        /// that don't match any of <paramref name="entities"/> (e.g. an id
        /// the caller no longer owns) are silently skipped. Does not save
        /// changes - the caller owns the unit of work.
        /// </summary>
        public static void Apply<TEntity>(
            IEnumerable<TEntity> entities,
            IReadOnlyList<Guid> orderedIds,
            Func<TEntity, Guid> idSelector,
            Action<TEntity, int> setSortOrder)
        {
            var byId = entities.ToDictionary(idSelector);

            for (var i = 0; i < orderedIds.Count; i++)
            {
                if (byId.TryGetValue(orderedIds[i], out var entity))
                {
                    setSortOrder(entity, i);
                }
            }
        }
    }
}
