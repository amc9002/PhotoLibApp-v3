using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PhotoLibApi.Data;
using PhotoLibApi.Models;

namespace backend.Tests;

/// <summary>
/// End-to-end (through real HTTP requests) confirmation that no controller
/// ever leaks another user's gallery/photo as a 403 or a 200 - the
/// documented contract is always 404, so a guessed id belonging to another
/// user is indistinguishable from an id that doesn't exist at all (see
/// <see cref="PhotoLibApi.Services.GalleryAccessService"/>'s class remarks).
/// </summary>
public class OwnershipIsolationTests
{
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

    [Fact]
    public async Task GetGalleryById_OwnedByAnotherUser_ReturnsNotFound()
    {
        using var factory = new TestingWebApplicationFactory();
        var (owner, _, _) = await factory.RegisterAndSignInAsync("owner");
        var galleryId = await CreateGalleryAsync(owner, "Owner's gallery");

        var (intruder, _, _) = await factory.RegisterAndSignInAsync("intruder");
        var response = await intruder.GetAsync($"/api/Gallery/{galleryId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpdateGallery_OwnedByAnotherUser_ReturnsNotFound()
    {
        using var factory = new TestingWebApplicationFactory();
        var (owner, _, _) = await factory.RegisterAndSignInAsync("owner");
        var galleryId = await CreateGalleryAsync(owner, "Owner's gallery");

        var (intruder, _, _) = await factory.RegisterAndSignInAsync("intruder");
        var response = await intruder.PutAsJsonAsync($"/api/Gallery/{galleryId}", new UpdateGalleryRequest
        {
            Title = "Hijacked title",
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeleteGallery_OwnedByAnotherUser_ReturnsNotFound()
    {
        using var factory = new TestingWebApplicationFactory();
        var (owner, _, _) = await factory.RegisterAndSignInAsync("owner");
        var galleryId = await CreateGalleryAsync(owner, "Owner's gallery");

        var (intruder, _, _) = await factory.RegisterAndSignInAsync("intruder");
        var response = await intruder.DeleteAsync($"/api/Gallery/{galleryId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task SetGalleryTags_OwnedByAnotherUser_ReturnsNotFound()
    {
        using var factory = new TestingWebApplicationFactory();
        var (owner, _, _) = await factory.RegisterAndSignInAsync("owner");
        var galleryId = await CreateGalleryAsync(owner, "Owner's gallery");

        var (intruder, _, _) = await factory.RegisterAndSignInAsync("intruder");
        var response = await intruder.PutAsJsonAsync($"/api/Gallery/{galleryId}/tags", new SetTagsRequest
        {
            TagNames = new List<string> { "hijacked" },
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetPhotosByGallery_OwnedByAnotherUser_ReturnsNotFound()
    {
        using var factory = new TestingWebApplicationFactory();
        var (owner, _, _) = await factory.RegisterAndSignInAsync("owner");
        var galleryId = await CreateGalleryAsync(owner, "Owner's gallery");

        var (intruder, _, _) = await factory.RegisterAndSignInAsync("intruder");
        var response = await intruder.GetAsync($"/api/Photo/by-gallery/{galleryId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreatePhoto_InAnotherUsersGallery_ReturnsNotFound()
    {
        using var factory = new TestingWebApplicationFactory();
        var (owner, _, _) = await factory.RegisterAndSignInAsync("owner");
        var galleryId = await CreateGalleryAsync(owner, "Owner's gallery");

        var (intruder, _, _) = await factory.RegisterAndSignInAsync("intruder");
        var response = await intruder.PostAsJsonAsync("/api/Photo", new CreatePhotoRequest
        {
            GalleryId = galleryId,
            Title = "Smuggled-in photo",
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetPhotoById_InAnotherUsersGallery_ReturnsNotFound()
    {
        using var factory = new TestingWebApplicationFactory();
        var (owner, _, _) = await factory.RegisterAndSignInAsync("owner");
        var galleryId = await CreateGalleryAsync(owner, "Owner's gallery");
        var photoId = await CreatePhotoAsync(owner, galleryId, "Owner's photo");

        var (intruder, _, _) = await factory.RegisterAndSignInAsync("intruder");
        var response = await intruder.GetAsync($"/api/Photo/{photoId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetPhotoFile_InAnotherUsersGallery_ReturnsNotFound()
    {
        using var factory = new TestingWebApplicationFactory();
        var (owner, _, _) = await factory.RegisterAndSignInAsync("owner");
        var galleryId = await CreateGalleryAsync(owner, "Owner's gallery");
        var photoId = await CreatePhotoAsync(owner, galleryId, "Owner's photo");

        var (intruder, _, _) = await factory.RegisterAndSignInAsync("intruder");
        var response = await intruder.GetAsync($"/api/Photo/{photoId}/file");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetPhotoThumbnail_InAnotherUsersGallery_ReturnsNotFound()
    {
        using var factory = new TestingWebApplicationFactory();
        var (owner, _, _) = await factory.RegisterAndSignInAsync("owner");
        var galleryId = await CreateGalleryAsync(owner, "Owner's gallery");
        var photoId = await CreatePhotoAsync(owner, galleryId, "Owner's photo");

        var (intruder, _, _) = await factory.RegisterAndSignInAsync("intruder");
        var response = await intruder.GetAsync($"/api/Photo/{photoId}/thumbnail");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpdatePhoto_InAnotherUsersGallery_ReturnsNotFound()
    {
        using var factory = new TestingWebApplicationFactory();
        var (owner, _, _) = await factory.RegisterAndSignInAsync("owner");
        var galleryId = await CreateGalleryAsync(owner, "Owner's gallery");
        var photoId = await CreatePhotoAsync(owner, galleryId, "Owner's photo");

        var (intruder, _, _) = await factory.RegisterAndSignInAsync("intruder");
        var response = await intruder.PutAsJsonAsync($"/api/Photo/{photoId}", new UpdatePhotoRequest
        {
            Title = "Hijacked title",
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeletePhoto_InAnotherUsersGallery_ReturnsNotFound()
    {
        using var factory = new TestingWebApplicationFactory();
        var (owner, _, _) = await factory.RegisterAndSignInAsync("owner");
        var galleryId = await CreateGalleryAsync(owner, "Owner's gallery");
        var photoId = await CreatePhotoAsync(owner, galleryId, "Owner's photo");

        var (intruder, _, _) = await factory.RegisterAndSignInAsync("intruder");
        var response = await intruder.DeleteAsync($"/api/Photo/{photoId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task SetPhotoTags_InAnotherUsersGallery_ReturnsNotFound()
    {
        using var factory = new TestingWebApplicationFactory();
        var (owner, _, _) = await factory.RegisterAndSignInAsync("owner");
        var galleryId = await CreateGalleryAsync(owner, "Owner's gallery");
        var photoId = await CreatePhotoAsync(owner, galleryId, "Owner's photo");

        var (intruder, _, _) = await factory.RegisterAndSignInAsync("intruder");
        var response = await intruder.PutAsJsonAsync($"/api/Photo/{photoId}/tags", new SetTagsRequest
        {
            TagNames = new List<string> { "hijacked" },
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task MovePhoto_SourcePhotoOwnedByAnotherUser_ReturnsNotFound()
    {
        using var factory = new TestingWebApplicationFactory();
        var (owner, _, _) = await factory.RegisterAndSignInAsync("owner");
        var ownerGalleryId = await CreateGalleryAsync(owner, "Owner's gallery");
        var ownerPhotoId = await CreatePhotoAsync(owner, ownerGalleryId, "Owner's photo");

        var (intruder, _, _) = await factory.RegisterAndSignInAsync("intruder");
        var intruderGalleryId = await CreateGalleryAsync(intruder, "Intruder's gallery");

        var response = await intruder.PostAsJsonAsync($"/api/Photo/{ownerPhotoId}/move", new MovePhotoRequest
        {
            GalleryId = intruderGalleryId,
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task MovePhoto_DestinationGalleryOwnedByAnotherUser_ReturnsNotFound()
    {
        using var factory = new TestingWebApplicationFactory();
        var (owner, _, _) = await factory.RegisterAndSignInAsync("owner");
        var ownerGalleryId = await CreateGalleryAsync(owner, "Owner's gallery");

        var (intruder, _, _) = await factory.RegisterAndSignInAsync("intruder");
        var intruderGalleryId = await CreateGalleryAsync(intruder, "Intruder's gallery");
        var intruderPhotoId = await CreatePhotoAsync(intruder, intruderGalleryId, "Intruder's own photo");

        // Intruder owns the photo, but tries to move it into the owner's
        // gallery - the destination check must independently reject this.
        var response = await intruder.PostAsJsonAsync($"/api/Photo/{intruderPhotoId}/move", new MovePhotoRequest
        {
            GalleryId = ownerGalleryId,
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CopyPhoto_SourcePhotoOwnedByAnotherUser_ReturnsNotFound()
    {
        using var factory = new TestingWebApplicationFactory();
        var (owner, _, _) = await factory.RegisterAndSignInAsync("owner");
        var ownerGalleryId = await CreateGalleryAsync(owner, "Owner's gallery");
        var ownerPhotoId = await CreatePhotoAsync(owner, ownerGalleryId, "Owner's photo");

        var (intruder, _, _) = await factory.RegisterAndSignInAsync("intruder");
        var intruderGalleryId = await CreateGalleryAsync(intruder, "Intruder's gallery");

        var response = await intruder.PostAsJsonAsync($"/api/Photo/{ownerPhotoId}/copy", new MovePhotoRequest
        {
            GalleryId = intruderGalleryId,
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CopyPhoto_DestinationGalleryOwnedByAnotherUser_ReturnsNotFound()
    {
        using var factory = new TestingWebApplicationFactory();
        var (owner, _, _) = await factory.RegisterAndSignInAsync("owner");
        var ownerGalleryId = await CreateGalleryAsync(owner, "Owner's gallery");

        var (intruder, _, _) = await factory.RegisterAndSignInAsync("intruder");
        var intruderGalleryId = await CreateGalleryAsync(intruder, "Intruder's gallery");
        var intruderPhotoId = await CreatePhotoAsync(intruder, intruderGalleryId, "Intruder's own photo");

        var response = await intruder.PostAsJsonAsync($"/api/Photo/{intruderPhotoId}/copy", new MovePhotoRequest
        {
            GalleryId = ownerGalleryId,
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ReorderPhotos_WithGalleryOwnedByAnotherUser_ReturnsNotFound()
    {
        using var factory = new TestingWebApplicationFactory();
        var (owner, _, _) = await factory.RegisterAndSignInAsync("owner");
        var ownerGalleryId = await CreateGalleryAsync(owner, "Owner's gallery");
        var ownerPhotoId = await CreatePhotoAsync(owner, ownerGalleryId, "Owner's photo");

        var (intruder, _, _) = await factory.RegisterAndSignInAsync("intruder");
        var response = await intruder.PutAsJsonAsync("/api/Photo/reorder", new ReorderPhotosRequest
        {
            GalleryId = ownerGalleryId,
            PhotoIds = new List<Guid> { ownerPhotoId },
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ReorderGalleries_SilentlyIgnoresGalleryIdsNotOwnedByCaller()
    {
        // GalleryController.Reorder has no explicit ownership check per id -
        // it filters its own query to `OwnerId == ownerId`, so ids the
        // caller doesn't own are just absent from what gets updated. This
        // test confirms that silent filtering actually protects the other
        // user's SortOrder (no exception, no leak, no mutation), rather
        // than asserting a 404 contract this endpoint doesn't implement.
        using var factory = new TestingWebApplicationFactory();
        var (owner, _, _) = await factory.RegisterAndSignInAsync("owner");
        var ownerGalleryId = await CreateGalleryAsync(owner, "Owner's gallery");

        // GalleryController never exposes SortOrder in a response body, so
        // "before" is read straight from the database, same as "after".
        int sortOrderBefore;
        using (var beforeScope = factory.Services.CreateScope())
        {
            var beforeDb = beforeScope.ServiceProvider.GetRequiredService<PhotoDbContext>();
            sortOrderBefore = (await beforeDb.Galleries.FindAsync(ownerGalleryId))!.SortOrder;
        }

        var (intruder, _, _) = await factory.RegisterAndSignInAsync("intruder");
        var intruderGalleryId = await CreateGalleryAsync(intruder, "Intruder's gallery");

        var response = await intruder.PutAsJsonAsync("/api/Gallery/reorder", new ReorderGalleriesRequest
        {
            GalleryIds = new List<Guid> { ownerGalleryId, intruderGalleryId },
        });
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PhotoDbContext>();
        var ownerGalleryAfter = await db.Galleries.FindAsync(ownerGalleryId);
        Assert.Equal(sortOrderBefore, ownerGalleryAfter!.SortOrder);
    }

    [Fact]
    public async Task LegacyGalleryWithNullOwnerId_IsNotVisibleToAnySignedInUser()
    {
        using var factory = new TestingWebApplicationFactory();

        var orphanedGalleryId = Guid.NewGuid();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PhotoDbContext>();
            db.Galleries.Add(new Gallery { Id = orphanedGalleryId, Title = "Legacy, pre-auth", OwnerId = null });
            await db.SaveChangesAsync();
        }

        var (someRealUser, _, _) = await factory.RegisterAndSignInAsync("real-user");
        var response = await someRealUser.GetAsync($"/api/Gallery/{orphanedGalleryId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
