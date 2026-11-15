using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PhotoLibApi.Data;
using PhotoLibApi.Models;
using PhotoLibApi.Services;

namespace backend.Tests;

/// <summary>
/// <see cref="SortOrderHelper"/> directly (pure, no DB), plus the Reorder
/// endpoints' HTTP-level contract: new order takes effect, ids from another
/// gallery/owner don't leak across galleries, and a couple of discovered
/// gaps in the soft-delete story are documented (not fixed) at the end.
/// </summary>
public class SortOrderTests
{
    private sealed class FakeOrderable
    {
        public Guid Id { get; set; }
        public int SortOrder { get; set; }
    }

    [Fact]
    public void Apply_AssignsDenseZeroBasedOrder_MatchingTheGivenSequence()
    {
        var a = new FakeOrderable { Id = Guid.NewGuid(), SortOrder = 5 };
        var b = new FakeOrderable { Id = Guid.NewGuid(), SortOrder = 1 };
        var c = new FakeOrderable { Id = Guid.NewGuid(), SortOrder = 9 };

        SortOrderHelper.Apply(new[] { a, b, c }, new[] { c.Id, a.Id, b.Id },
            x => x.Id, (x, i) => x.SortOrder = i);

        Assert.Equal(1, a.SortOrder);
        Assert.Equal(2, b.SortOrder);
        Assert.Equal(0, c.SortOrder);
    }

    [Fact]
    public void Apply_SkipsOrderedIdsNotPresentAmongTheEntities()
    {
        var a = new FakeOrderable { Id = Guid.NewGuid(), SortOrder = 0 };
        var unrelatedId = Guid.NewGuid();

        SortOrderHelper.Apply(new[] { a }, new[] { unrelatedId, a.Id },
            x => x.Id, (x, i) => x.SortOrder = i);

        // a matched at index 1 - the unrelated id at index 0 is simply
        // skipped, not an error, and doesn't shift a's assigned index.
        Assert.Equal(1, a.SortOrder);
    }

    [Fact]
    public void Apply_LeavesEntitiesNotMentionedInTheOrderedListUntouched()
    {
        var mentioned = new FakeOrderable { Id = Guid.NewGuid(), SortOrder = 7 };
        var notMentioned = new FakeOrderable { Id = Guid.NewGuid(), SortOrder = 42 };

        SortOrderHelper.Apply(new[] { mentioned, notMentioned }, new[] { mentioned.Id },
            x => x.Id, (x, i) => x.SortOrder = i);

        Assert.Equal(0, mentioned.SortOrder);
        Assert.Equal(42, notMentioned.SortOrder); // untouched, not reset
    }

    [Fact]
    public void Apply_WhenAnIdAppearsTwiceInTheOrderedList_TheLastOccurrenceWins()
    {
        var a = new FakeOrderable { Id = Guid.NewGuid(), SortOrder = 0 };
        var duplicateId = a.Id;

        SortOrderHelper.Apply(new[] { a }, new[] { duplicateId, Guid.NewGuid(), duplicateId },
            x => x.Id, (x, i) => x.SortOrder = i);

        // The loop re-assigns a's SortOrder each time its id is revisited,
        // so the final value comes from its LAST position (index 2), not
        // its first (index 0).
        Assert.Equal(2, a.SortOrder);
    }

    private static async Task<Guid> CreateGalleryAsync(HttpClient client, string title)
    {
        var response = await client.PostAsJsonAsync("/api/Gallery", new CreateGalleryRequest { Title = title });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestJson.CaseInsensitive);
        return body.GetProperty("id").GetGuid();
    }

    private static async Task<Guid> CreatePhotoAsync(HttpClient client, Guid galleryId, string title)
    {
        var response = await client.PostAsJsonAsync("/api/Photo", new CreatePhotoRequest
        {
            GalleryId = galleryId,
            Title = title,
        });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestJson.CaseInsensitive);
        return body.GetProperty("id").GetGuid();
    }

    private static async Task<List<Guid>> GetGalleryIdsInOrderAsync(HttpClient client)
    {
        var list = await client.GetFromJsonAsync<JsonElement>("/api/Gallery", TestJson.CaseInsensitive);
        return list.EnumerateArray().Select(g => g.GetProperty("id").GetGuid()).ToList();
    }

    private static async Task<List<Guid>> GetGalleryPhotoIdsInOrderAsync(HttpClient client, Guid galleryId)
    {
        var list = await client.GetFromJsonAsync<JsonElement>(
            $"/api/Photo/by-gallery/{galleryId}", TestJson.CaseInsensitive);
        return list.EnumerateArray().Select(p => p.GetProperty("id").GetGuid()).ToList();
    }

    [Fact]
    public async Task ReorderGalleries_NewOrderIsReflectedInGetAll()
    {
        using var factory = new TestingWebApplicationFactory();
        var (client, _, _) = await factory.RegisterAndSignInAsync();
        var a = await CreateGalleryAsync(client, "A");
        var b = await CreateGalleryAsync(client, "B");
        var c = await CreateGalleryAsync(client, "C");
        Assert.Equal(new[] { a, b, c }, await GetGalleryIdsInOrderAsync(client));

        var reorder = await client.PutAsJsonAsync("/api/Gallery/reorder", new ReorderGalleriesRequest
        {
            GalleryIds = new List<Guid> { c, a, b },
        });
        Assert.Equal(HttpStatusCode.NoContent, reorder.StatusCode);

        Assert.Equal(new[] { c, a, b }, await GetGalleryIdsInOrderAsync(client));
    }

    [Fact]
    public async Task ReorderPhotos_NewOrderIsReflectedInGetByGallery()
    {
        using var factory = new TestingWebApplicationFactory();
        var (client, _, _) = await factory.RegisterAndSignInAsync();
        var galleryId = await CreateGalleryAsync(client, "Gallery");
        var a = await CreatePhotoAsync(client, galleryId, "A");
        var b = await CreatePhotoAsync(client, galleryId, "B");
        var c = await CreatePhotoAsync(client, galleryId, "C");

        var reorder = await client.PutAsJsonAsync("/api/Photo/reorder", new ReorderPhotosRequest
        {
            GalleryId = galleryId,
            PhotoIds = new List<Guid> { c, b, a },
        });
        Assert.Equal(HttpStatusCode.NoContent, reorder.StatusCode);

        Assert.Equal(new[] { c, b, a }, await GetGalleryPhotoIdsInOrderAsync(client, galleryId));
    }

    [Fact]
    public async Task ReorderPhotos_IdFromAnotherGallery_IsIgnored_AndThatGalleryIsUntouched()
    {
        using var factory = new TestingWebApplicationFactory();
        var (client, _, _) = await factory.RegisterAndSignInAsync();
        var galleryA = await CreateGalleryAsync(client, "A");
        var galleryB = await CreateGalleryAsync(client, "B");
        var aPhoto1 = await CreatePhotoAsync(client, galleryA, "A1");
        var aPhoto2 = await CreatePhotoAsync(client, galleryA, "A2");
        var bPhoto = await CreatePhotoAsync(client, galleryB, "B1");

        // bPhoto doesn't belong to galleryA - the query behind Reorder is
        // scoped by GalleryId, so this id simply can't match anything there.
        var reorder = await client.PutAsJsonAsync("/api/Photo/reorder", new ReorderPhotosRequest
        {
            GalleryId = galleryA,
            PhotoIds = new List<Guid> { bPhoto, aPhoto2, aPhoto1 },
        });
        Assert.Equal(HttpStatusCode.NoContent, reorder.StatusCode);

        Assert.Equal(new[] { aPhoto2, aPhoto1 }, await GetGalleryPhotoIdsInOrderAsync(client, galleryA));
        Assert.Equal(new[] { bPhoto }, await GetGalleryPhotoIdsInOrderAsync(client, galleryB));
    }

    [Fact]
    public async Task ReorderPhotos_NonexistentIdsAreIgnored_RealOnesAreStillReordered()
    {
        using var factory = new TestingWebApplicationFactory();
        var (client, _, _) = await factory.RegisterAndSignInAsync();
        var galleryId = await CreateGalleryAsync(client, "Gallery");
        var a = await CreatePhotoAsync(client, galleryId, "A");
        var b = await CreatePhotoAsync(client, galleryId, "B");

        var reorder = await client.PutAsJsonAsync("/api/Photo/reorder", new ReorderPhotosRequest
        {
            GalleryId = galleryId,
            PhotoIds = new List<Guid> { Guid.NewGuid(), b, Guid.NewGuid(), a },
        });
        Assert.Equal(HttpStatusCode.NoContent, reorder.StatusCode);

        Assert.Equal(new[] { b, a }, await GetGalleryPhotoIdsInOrderAsync(client, galleryId));
    }

    [Fact]
    public async Task Create_NextSortOrder_CountsSoftDeletedGalleriesTowardTheMax()
    {
        // DISCOVERED BEHAVIOR: GalleryController.Create computes the new
        // gallery's SortOrder from MAX(SortOrder) across ALL of the
        // owner's galleries, including soft-deleted ones (no "!IsDeleted"
        // in that specific query). Deleting a gallery and creating a new
        // one therefore does not "reclaim" the deleted gallery's slot - the
        // new SortOrder value counts it anyway. Harmless in practice
        // (GetAll already orders by whatever values exist and tolerates
        // gaps), but documented here as the real contract.
        using var factory = new TestingWebApplicationFactory();
        var (client, _, _) = await factory.RegisterAndSignInAsync();
        var first = await CreateGalleryAsync(client, "First");
        var second = await CreateGalleryAsync(client, "Second");
        await client.DeleteAsync($"/api/Gallery/{second}");

        var third = await CreateGalleryAsync(client, "Third");

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PhotoDbContext>();
        var firstSortOrder = (await db.Galleries.FindAsync(first))!.SortOrder;
        var secondSortOrder = (await db.Galleries.FindAsync(second))!.SortOrder;
        var thirdSortOrder = (await db.Galleries.FindAsync(third))!.SortOrder;

        Assert.True(thirdSortOrder > secondSortOrder);
        Assert.True(secondSortOrder > firstSortOrder);
    }

    [Fact]
    public async Task Create_NextSortOrder_CountsSoftDeletedPhotosTowardTheMax()
    {
        // Same discovered pattern as the gallery test above, for
        // PhotoController's NextSortOrderAsync.
        using var factory = new TestingWebApplicationFactory();
        var (client, _, _) = await factory.RegisterAndSignInAsync();
        var galleryId = await CreateGalleryAsync(client, "Gallery");
        var first = await CreatePhotoAsync(client, galleryId, "First");
        var second = await CreatePhotoAsync(client, galleryId, "Second");
        await client.DeleteAsync($"/api/Photo/{second}");

        var third = await CreatePhotoAsync(client, galleryId, "Third");

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PhotoDbContext>();
        var firstSortOrder = (await db.Photos.FindAsync(first))!.SortOrder;
        var secondSortOrder = (await db.Photos.FindAsync(second))!.SortOrder;
        var thirdSortOrder = (await db.Photos.FindAsync(third))!.SortOrder;

        Assert.True(thirdSortOrder > secondSortOrder);
        Assert.True(secondSortOrder > firstSortOrder);
    }

    [Fact]
    public async Task ReorderGalleries_DoesNotChangeTheSortOrderOfAnAlreadySoftDeletedGallery()
    {
        // FIXED (was a confirmed gap): GalleryController.Reorder now
        // excludes soft-deleted galleries from its query, consistent with
        // every other gallery operation - a deleted id is simply absent
        // from what gets a new SortOrder, so its stored value never changes.
        using var factory = new TestingWebApplicationFactory();
        var (client, _, _) = await factory.RegisterAndSignInAsync();
        var live = await CreateGalleryAsync(client, "Live");
        var deleted = await CreateGalleryAsync(client, "Soon deleted");
        await client.DeleteAsync($"/api/Gallery/{deleted}");

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PhotoDbContext>();
        var sortOrderBefore = (await db.Galleries.FindAsync(deleted))!.SortOrder;

        var reorder = await client.PutAsJsonAsync("/api/Gallery/reorder", new ReorderGalleriesRequest
        {
            GalleryIds = new List<Guid> { deleted, live },
        });
        Assert.Equal(HttpStatusCode.NoContent, reorder.StatusCode);

        var deletedGallery = await db.Galleries.FindAsync(deleted);
        Assert.Equal(sortOrderBefore, deletedGallery!.SortOrder);
    }

    [Fact]
    public async Task ReorderPhotos_DoesNotChangeTheSortOrderOfAnIndividuallySoftDeletedPhoto()
    {
        // FIXED (was a confirmed gap): PhotoController.Reorder now excludes
        // soft-deleted photos from its query too (the gallery-level check
        // via IsOwnedAsync already excluded a soft-deleted GALLERY) - so an
        // individually-deleted photo's SortOrder is left untouched even
        // when its still-live gallery is being reordered.
        using var factory = new TestingWebApplicationFactory();
        var (client, _, _) = await factory.RegisterAndSignInAsync();
        var galleryId = await CreateGalleryAsync(client, "Gallery");
        var live = await CreatePhotoAsync(client, galleryId, "Live");
        var deleted = await CreatePhotoAsync(client, galleryId, "Soon deleted");
        await client.DeleteAsync($"/api/Photo/{deleted}");

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PhotoDbContext>();
        var sortOrderBefore = (await db.Photos.FindAsync(deleted))!.SortOrder;

        var reorder = await client.PutAsJsonAsync("/api/Photo/reorder", new ReorderPhotosRequest
        {
            GalleryId = galleryId,
            PhotoIds = new List<Guid> { deleted, live },
        });
        Assert.Equal(HttpStatusCode.NoContent, reorder.StatusCode);

        var deletedPhoto = await db.Photos.FindAsync(deleted);
        Assert.Equal(sortOrderBefore, deletedPhoto!.SortOrder);
    }

    [Fact]
    public async Task ReorderGalleries_WithAMixOfLiveAndDeletedIds_ReordersOnlyTheLiveOnes()
    {
        using var factory = new TestingWebApplicationFactory();
        var (client, _, _) = await factory.RegisterAndSignInAsync();
        var a = await CreateGalleryAsync(client, "A");
        var deleted = await CreateGalleryAsync(client, "Deleted");
        var c = await CreateGalleryAsync(client, "C");
        await client.DeleteAsync($"/api/Gallery/{deleted}");

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PhotoDbContext>();
        var deletedSortOrderBefore = (await db.Galleries.FindAsync(deleted))!.SortOrder;

        // Request a reversed order for the two LIVE galleries, with the
        // deleted one's id mixed in between.
        var reorder = await client.PutAsJsonAsync("/api/Gallery/reorder", new ReorderGalleriesRequest
        {
            GalleryIds = new List<Guid> { c, deleted, a },
        });
        Assert.Equal(HttpStatusCode.NoContent, reorder.StatusCode);

        Assert.Equal(new[] { c, a }, await GetGalleryIdsInOrderAsync(client));
        Assert.Equal(deletedSortOrderBefore, (await db.Galleries.FindAsync(deleted))!.SortOrder);
    }

    [Fact]
    public async Task ReorderPhotos_WithAMixOfLiveAndDeletedIds_ReordersOnlyTheLiveOnes()
    {
        using var factory = new TestingWebApplicationFactory();
        var (client, _, _) = await factory.RegisterAndSignInAsync();
        var galleryId = await CreateGalleryAsync(client, "Gallery");
        var a = await CreatePhotoAsync(client, galleryId, "A");
        var deleted = await CreatePhotoAsync(client, galleryId, "Deleted");
        var c = await CreatePhotoAsync(client, galleryId, "C");
        await client.DeleteAsync($"/api/Photo/{deleted}");

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PhotoDbContext>();
        var deletedSortOrderBefore = (await db.Photos.FindAsync(deleted))!.SortOrder;

        var reorder = await client.PutAsJsonAsync("/api/Photo/reorder", new ReorderPhotosRequest
        {
            GalleryId = galleryId,
            PhotoIds = new List<Guid> { c, deleted, a },
        });
        Assert.Equal(HttpStatusCode.NoContent, reorder.StatusCode);

        Assert.Equal(new[] { c, a }, await GetGalleryPhotoIdsInOrderAsync(client, galleryId));
        Assert.Equal(deletedSortOrderBefore, (await db.Photos.FindAsync(deleted))!.SortOrder);
    }

    [Fact]
    public async Task ReorderGalleries_WithARepeatedId_TheLastOccurrenceWins()
    {
        // HTTP-level confirmation of the same rule already proven directly
        // against SortOrderHelper above - the current "last occurrence
        // wins" semantics are preserved, not changed by this stage's fix.
        using var factory = new TestingWebApplicationFactory();
        var (client, _, _) = await factory.RegisterAndSignInAsync();
        var a = await CreateGalleryAsync(client, "A");
        var b = await CreateGalleryAsync(client, "B");

        var reorder = await client.PutAsJsonAsync("/api/Gallery/reorder", new ReorderGalleriesRequest
        {
            GalleryIds = new List<Guid> { a, b, a }, // a's last position is index 2
        });
        Assert.Equal(HttpStatusCode.NoContent, reorder.StatusCode);

        // a ends up after b (SortOrder 2 vs 1), not before it.
        Assert.Equal(new[] { b, a }, await GetGalleryIdsInOrderAsync(client));
    }
}
