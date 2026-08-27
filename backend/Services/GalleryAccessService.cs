using Microsoft.EntityFrameworkCore;
using PhotoLibApi.Data;
using PhotoLibApi.Models;

namespace PhotoLibApi.Services
{
    /// <summary>
    /// Central ownership check reused by <see cref="Controllers.GalleryController"/>
    /// and <see cref="Controllers.PhotoController"/>. <see cref="Photo"/> has no
    /// owner of its own - ownership is always resolved through its parent
    /// <see cref="Gallery.OwnerId"/>. Every failed check should be surfaced
    /// as <c>404</c> (never <c>403</c>), so a guessed id belonging to
    /// another user is indistinguishable from an id that doesn't exist.
    /// </summary>
    public class GalleryAccessService
    {
        private readonly PhotoDbContext _db;

        /// <param name="db">Database context used for ownership lookups.</param>
        public GalleryAccessService(PhotoDbContext db)
        {
            _db = db;
        }

        /// <summary>
        /// Whether a (non-deleted) gallery with this id exists and is owned
        /// by <paramref name="userId"/>.
        /// </summary>
        public Task<bool> IsOwnedAsync(Guid galleryId, Guid userId)
        {
            return _db.Galleries
                .AsNoTracking()
                .AnyAsync(g => g.Id == galleryId && g.OwnerId == userId && !g.IsDeleted);
        }

        /// <summary>
        /// Loads a tracked photo by id, but only if its gallery is owned by
        /// <paramref name="userId"/> - otherwise <c>null</c>, same as "photo
        /// doesn't exist" from the caller's perspective.
        /// </summary>
        public async Task<Photo?> GetOwnedPhotoAsync(Guid photoId, Guid userId)
        {
            var photo = await _db.Photos.FindAsync(photoId);
            if (photo == null)
                return null;

            var ownedByCaller = await _db.Galleries
                .AsNoTracking()
                .AnyAsync(g => g.Id == photo.GalleryId && g.OwnerId == userId);

            return ownedByCaller ? photo : null;
        }

        /// <summary>
        /// Loads a tracked, non-deleted gallery by id, but only if it's
        /// owned by <paramref name="userId"/> - otherwise <c>null</c>, same
        /// as "gallery doesn't exist" from the caller's perspective.
        /// </summary>
        public async Task<Gallery?> GetOwnedGalleryAsync(Guid galleryId, Guid userId)
        {
            var gallery = await _db.Galleries.FindAsync(galleryId);
            if (gallery == null || gallery.IsDeleted || gallery.OwnerId != userId)
                return null;

            return gallery;
        }
    }
}
