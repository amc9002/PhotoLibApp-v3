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
        /// Loads a tracked photo by id, but only if it isn't itself
        /// soft-deleted and its gallery is owned by <paramref name="userId"/>
        /// and isn't soft-deleted either - otherwise <c>null</c>, same as
        /// "photo doesn't exist" from the caller's perspective. Visibility is
        /// always computed from both flags at read time, never cascaded by
        /// writing one object's <c>IsDeleted</c> onto the other - deleting a
        /// gallery never touches its photos' own flag, so a later restore of
        /// the gallery (not yet implemented) would correctly leave
        /// individually-deleted photos deleted.
        /// </summary>
        public async Task<Photo?> GetOwnedPhotoAsync(Guid photoId, Guid userId)
        {
            var photo = await _db.Photos.FindAsync(photoId);
            if (photo == null || photo.IsDeleted)
                return null;

            var ownedByCaller = await _db.Galleries
                .AsNoTracking()
                .AnyAsync(g => g.Id == photo.GalleryId && g.OwnerId == userId && !g.IsDeleted);

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

        /// <summary>
        /// Same ownership rules as <see cref="GetOwnedGalleryAsync"/>
        /// (exists, owned by <paramref name="userId"/>, not soft-deleted),
        /// but with the <see cref="Gallery.Tags"/> navigation eagerly
        /// loaded via <c>Include</c>. Callers that are about to *replace*
        /// the gallery's tag set (<see cref="Controllers.GalleryController.SetTags"/>)
        /// need this - without the collection already loaded, EF Core's
        /// change tracker has no record of the existing associations, so
        /// assigning a new collection only adds rows and never removes the
        /// stale ones. Callers that don't touch <c>Tags</c> at all
        /// (<c>Update</c>, <c>Delete</c>) should keep using
        /// <see cref="GetOwnedGalleryAsync"/> instead, to avoid the extra
        /// join for no benefit.
        /// </summary>
        public async Task<Gallery?> GetOwnedGalleryWithTagsAsync(Guid galleryId, Guid userId)
        {
            var gallery = await _db.Galleries
                .Include(g => g.Tags)
                .FirstOrDefaultAsync(g => g.Id == galleryId);
            if (gallery == null || gallery.IsDeleted || gallery.OwnerId != userId)
                return null;

            return gallery;
        }

        /// <summary>
        /// Composable query for every non-deleted photo <paramref name="userId"/>
        /// owns, across all their galleries - not executed here, callers add
        /// their own <c>.Select</c>/<c>.Where</c> before materializing, same
        /// as <see cref="Controllers.PhotoController.GetByGallery"/> does
        /// internally for a single gallery.
        /// </summary>
        public IQueryable<Photo> OwnedPhotos(Guid userId) =>
            _db.Photos.Where(p => !p.IsDeleted &&
                _db.Galleries.Any(g => g.Id == p.GalleryId && g.OwnerId == userId && !g.IsDeleted));
    }
}
