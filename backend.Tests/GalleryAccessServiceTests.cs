using Microsoft.EntityFrameworkCore;
using PhotoLibApi.Models;
using PhotoLibApi.Services;

namespace backend.Tests;

/// <summary>
/// Direct, HTTP-free tests of <see cref="GalleryAccessService"/> - the
/// single ownership gate every controller delegates to. An earlier pass of
/// these tests found that <c>GetOwnedPhotoAsync</c> didn't filter by either
/// its own or its gallery's <c>IsDeleted</c> flag; that gap is now closed
/// (see the method's remarks), and the tests below assert the corrected
/// contract, including a restore simulation proving it doesn't regress
/// into cascading one object's deletion onto the other.
/// </summary>
public class GalleryAccessServiceTests
{
    [Fact]
    public async Task IsOwnedAsync_ReturnsTrue_WhenGalleryOwnedByUser()
    {
        using var testDb = new TestDb();
        var ownerId = Guid.NewGuid();
        var galleryId = Guid.NewGuid();
        testDb.Context.Galleries.Add(new Gallery { Id = galleryId, Title = "Mine", OwnerId = ownerId });
        await testDb.Context.SaveChangesAsync();

        var service = new GalleryAccessService(testDb.Context);

        Assert.True(await service.IsOwnedAsync(galleryId, ownerId));
    }

    [Fact]
    public async Task IsOwnedAsync_ReturnsFalse_WhenGalleryOwnedByAnotherUser()
    {
        using var testDb = new TestDb();
        var ownerId = Guid.NewGuid();
        var galleryId = Guid.NewGuid();
        testDb.Context.Galleries.Add(new Gallery { Id = galleryId, Title = "Not yours", OwnerId = ownerId });
        await testDb.Context.SaveChangesAsync();

        var service = new GalleryAccessService(testDb.Context);

        Assert.False(await service.IsOwnedAsync(galleryId, Guid.NewGuid()));
    }

    [Fact]
    public async Task IsOwnedAsync_ReturnsFalse_WhenGalleryIsSoftDeleted()
    {
        using var testDb = new TestDb();
        var ownerId = Guid.NewGuid();
        var galleryId = Guid.NewGuid();
        testDb.Context.Galleries.Add(new Gallery { Id = galleryId, Title = "Deleted", OwnerId = ownerId, IsDeleted = true });
        await testDb.Context.SaveChangesAsync();

        var service = new GalleryAccessService(testDb.Context);

        Assert.False(await service.IsOwnedAsync(galleryId, ownerId));
    }

    [Fact]
    public async Task IsOwnedAsync_ReturnsFalse_WhenGalleryDoesNotExist()
    {
        using var testDb = new TestDb();
        var service = new GalleryAccessService(testDb.Context);

        Assert.False(await service.IsOwnedAsync(Guid.NewGuid(), Guid.NewGuid()));
    }

    [Fact]
    public async Task IsOwnedAsync_ReturnsFalse_ForLegacyGalleryWithNullOwnerId()
    {
        // Pre-authentication data (OwnerId == null) belongs to no one -
        // not even an arbitrary real user - until AuthController.AdminRegister
        // explicitly claims it (see AuthControllerTests' claim tests).
        using var testDb = new TestDb();
        var galleryId = Guid.NewGuid();
        testDb.Context.Galleries.Add(new Gallery { Id = galleryId, Title = "Legacy", OwnerId = null });
        await testDb.Context.SaveChangesAsync();

        var service = new GalleryAccessService(testDb.Context);

        Assert.False(await service.IsOwnedAsync(galleryId, Guid.NewGuid()));
    }

    [Fact]
    public async Task GetOwnedGalleryAsync_ReturnsGallery_WhenOwned()
    {
        using var testDb = new TestDb();
        var ownerId = Guid.NewGuid();
        var galleryId = Guid.NewGuid();
        testDb.Context.Galleries.Add(new Gallery { Id = galleryId, Title = "Mine", OwnerId = ownerId });
        await testDb.Context.SaveChangesAsync();

        var service = new GalleryAccessService(testDb.Context);
        var result = await service.GetOwnedGalleryAsync(galleryId, ownerId);

        Assert.NotNull(result);
        Assert.Equal(galleryId, result!.Id);
    }

    [Fact]
    public async Task GetOwnedGalleryAsync_ReturnsNull_WhenOwnedByAnotherUser()
    {
        using var testDb = new TestDb();
        var ownerId = Guid.NewGuid();
        var galleryId = Guid.NewGuid();
        testDb.Context.Galleries.Add(new Gallery { Id = galleryId, Title = "Not yours", OwnerId = ownerId });
        await testDb.Context.SaveChangesAsync();

        var service = new GalleryAccessService(testDb.Context);

        Assert.Null(await service.GetOwnedGalleryAsync(galleryId, Guid.NewGuid()));
    }

    [Fact]
    public async Task GetOwnedGalleryAsync_ReturnsNull_WhenSoftDeleted()
    {
        using var testDb = new TestDb();
        var ownerId = Guid.NewGuid();
        var galleryId = Guid.NewGuid();
        testDb.Context.Galleries.Add(new Gallery { Id = galleryId, Title = "Deleted", OwnerId = ownerId, IsDeleted = true });
        await testDb.Context.SaveChangesAsync();

        var service = new GalleryAccessService(testDb.Context);

        Assert.Null(await service.GetOwnedGalleryAsync(galleryId, ownerId));
    }

    [Fact]
    public async Task GetOwnedGalleryAsync_ReturnsNull_WhenOwnerIdIsNull()
    {
        using var testDb = new TestDb();
        var galleryId = Guid.NewGuid();
        testDb.Context.Galleries.Add(new Gallery { Id = galleryId, Title = "Legacy", OwnerId = null });
        await testDb.Context.SaveChangesAsync();

        var service = new GalleryAccessService(testDb.Context);

        Assert.Null(await service.GetOwnedGalleryAsync(galleryId, Guid.NewGuid()));
    }

    [Fact]
    public async Task GetOwnedGalleryAsync_ReturnsNull_WhenGalleryDoesNotExist()
    {
        using var testDb = new TestDb();
        var service = new GalleryAccessService(testDb.Context);

        Assert.Null(await service.GetOwnedGalleryAsync(Guid.NewGuid(), Guid.NewGuid()));
    }

    [Fact]
    public async Task GetOwnedPhotoAsync_ReturnsPhoto_WhenGalleryOwnedByUser()
    {
        using var testDb = new TestDb();
        var ownerId = Guid.NewGuid();
        var galleryId = Guid.NewGuid();
        var photoId = Guid.NewGuid();
        testDb.Context.Galleries.Add(new Gallery { Id = galleryId, Title = "Mine", OwnerId = ownerId });
        testDb.Context.Photos.Add(new Photo { Id = photoId, GalleryId = galleryId, Title = "Photo" });
        await testDb.Context.SaveChangesAsync();

        var service = new GalleryAccessService(testDb.Context);

        Assert.NotNull(await service.GetOwnedPhotoAsync(photoId, ownerId));
    }

    [Fact]
    public async Task GetOwnedPhotoAsync_ReturnsNull_WhenGalleryOwnedByAnotherUser()
    {
        using var testDb = new TestDb();
        var ownerId = Guid.NewGuid();
        var galleryId = Guid.NewGuid();
        var photoId = Guid.NewGuid();
        testDb.Context.Galleries.Add(new Gallery { Id = galleryId, Title = "Not yours", OwnerId = ownerId });
        testDb.Context.Photos.Add(new Photo { Id = photoId, GalleryId = galleryId, Title = "Photo" });
        await testDb.Context.SaveChangesAsync();

        var service = new GalleryAccessService(testDb.Context);

        Assert.Null(await service.GetOwnedPhotoAsync(photoId, Guid.NewGuid()));
    }

    [Fact]
    public async Task GetOwnedPhotoAsync_ReturnsNull_WhenPhotoDoesNotExist()
    {
        using var testDb = new TestDb();
        var service = new GalleryAccessService(testDb.Context);

        Assert.Null(await service.GetOwnedPhotoAsync(Guid.NewGuid(), Guid.NewGuid()));
    }

    [Fact]
    public async Task GetOwnedPhotoAsync_ReturnsNull_WhenGalleryOwnerIdIsNull()
    {
        using var testDb = new TestDb();
        var galleryId = Guid.NewGuid();
        var photoId = Guid.NewGuid();
        testDb.Context.Galleries.Add(new Gallery { Id = galleryId, Title = "Legacy", OwnerId = null });
        testDb.Context.Photos.Add(new Photo { Id = photoId, GalleryId = galleryId, Title = "Photo" });
        await testDb.Context.SaveChangesAsync();

        var service = new GalleryAccessService(testDb.Context);

        Assert.Null(await service.GetOwnedPhotoAsync(photoId, Guid.NewGuid()));
    }

    [Fact]
    public async Task GetOwnedPhotoAsync_ReturnsNull_WhenGalleryIsSoftDeleted()
    {
        // Fixed contract (was a confirmed finding): visibility is computed
        // from both the photo's own IsDeleted and its gallery's, never by
        // cascading a write from one onto the other - see
        // GalleryAccessService.GetOwnedPhotoAsync's remarks.
        using var testDb = new TestDb();
        var ownerId = Guid.NewGuid();
        var galleryId = Guid.NewGuid();
        var photoId = Guid.NewGuid();
        testDb.Context.Galleries.Add(new Gallery { Id = galleryId, Title = "Soft-deleted", OwnerId = ownerId, IsDeleted = true });
        testDb.Context.Photos.Add(new Photo { Id = photoId, GalleryId = galleryId, Title = "Photo" });
        await testDb.Context.SaveChangesAsync();

        var service = new GalleryAccessService(testDb.Context);

        Assert.Null(await service.GetOwnedPhotoAsync(photoId, ownerId));
    }

    [Fact]
    public async Task GetOwnedPhotoAsync_ReturnsNull_WhenThePhotoItselfIsSoftDeleted()
    {
        using var testDb = new TestDb();
        var ownerId = Guid.NewGuid();
        var galleryId = Guid.NewGuid();
        var photoId = Guid.NewGuid();
        testDb.Context.Galleries.Add(new Gallery { Id = galleryId, Title = "Mine", OwnerId = ownerId });
        testDb.Context.Photos.Add(new Photo { Id = photoId, GalleryId = galleryId, Title = "Photo", IsDeleted = true });
        await testDb.Context.SaveChangesAsync();

        var service = new GalleryAccessService(testDb.Context);

        Assert.Null(await service.GetOwnedPhotoAsync(photoId, ownerId));
    }

    [Fact]
    public async Task GetOwnedPhotoAsync_RestoreSimulation_IndividuallyDeletedPhotoStaysHidden_AfterGalleryIsRestored()
    {
        // Simulates a future "restore gallery" feature (not yet built) by
        // flipping gallery.IsDeleted back to false directly. Confirms
        // requirement 4: a photo that was deleted independently of its
        // gallery must stay hidden after the gallery comes back, while a
        // photo that was only ever hidden because of the gallery becomes
        // visible again automatically - because neither photo's own flag
        // was ever touched by the gallery's deletion.
        using var testDb = new TestDb();
        var ownerId = Guid.NewGuid();
        var galleryId = Guid.NewGuid();
        var untouchedPhotoId = Guid.NewGuid();
        var individuallyDeletedPhotoId = Guid.NewGuid();

        var gallery = new Gallery { Id = galleryId, Title = "Mine", OwnerId = ownerId, IsDeleted = true };
        testDb.Context.Galleries.Add(gallery);
        testDb.Context.Photos.AddRange(
            new Photo { Id = untouchedPhotoId, GalleryId = galleryId, Title = "Never deleted itself" },
            new Photo { Id = individuallyDeletedPhotoId, GalleryId = galleryId, Title = "Deleted on its own", IsDeleted = true });
        await testDb.Context.SaveChangesAsync();

        var service = new GalleryAccessService(testDb.Context);

        // While the gallery is deleted, neither photo is reachable.
        Assert.Null(await service.GetOwnedPhotoAsync(untouchedPhotoId, ownerId));
        Assert.Null(await service.GetOwnedPhotoAsync(individuallyDeletedPhotoId, ownerId));

        // Simulate a future restore: only the gallery's own flag flips back.
        gallery.IsDeleted = false;
        await testDb.Context.SaveChangesAsync();

        Assert.NotNull(await service.GetOwnedPhotoAsync(untouchedPhotoId, ownerId));
        Assert.Null(await service.GetOwnedPhotoAsync(individuallyDeletedPhotoId, ownerId));
    }

    [Fact]
    public async Task OwnedPhotos_ReturnsOnlyNonDeletedPhotosInNonDeletedGalleriesOwnedByUser()
    {
        using var testDb = new TestDb();
        var ownerId = Guid.NewGuid();
        var otherOwnerId = Guid.NewGuid();

        var ownedGalleryId = Guid.NewGuid();
        var deletedGalleryId = Guid.NewGuid();
        var othersGalleryId = Guid.NewGuid();

        testDb.Context.Galleries.AddRange(
            new Gallery { Id = ownedGalleryId, Title = "Mine", OwnerId = ownerId },
            new Gallery { Id = deletedGalleryId, Title = "Mine, deleted", OwnerId = ownerId, IsDeleted = true },
            new Gallery { Id = othersGalleryId, Title = "Someone else's", OwnerId = otherOwnerId });

        var visiblePhotoId = Guid.NewGuid();
        testDb.Context.Photos.AddRange(
            new Photo { Id = visiblePhotoId, GalleryId = ownedGalleryId, Title = "Visible" },
            new Photo { Id = Guid.NewGuid(), GalleryId = ownedGalleryId, Title = "Soft-deleted photo", IsDeleted = true },
            new Photo { Id = Guid.NewGuid(), GalleryId = deletedGalleryId, Title = "In a deleted gallery" },
            new Photo { Id = Guid.NewGuid(), GalleryId = othersGalleryId, Title = "Someone else's photo" });

        await testDb.Context.SaveChangesAsync();

        var service = new GalleryAccessService(testDb.Context);
        var resultIds = await service.OwnedPhotos(ownerId).Select(p => p.Id).ToListAsync();

        Assert.Equal(new[] { visiblePhotoId }, resultIds);
    }
}
