using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PhotoLibApi.Data;
using PhotoLibApi.Models;

namespace backend.Tests;

/// <summary>
/// Photo create/read/update/delete/move/copy contract, driven entirely
/// through real HTTP calls. Cross-user isolation and the soft-delete
/// contract fixed in <c>GetOwnedPhotoAsync</c> are already covered by
/// <see cref="OwnershipIsolationTests"/> and <see cref="SoftDeleteBypassTests"/>
/// and are not repeated here.
/// </summary>
public class PhotoCrudTests
{
    private static async Task<Guid> CreateGalleryAsync(HttpClient client, string title)
    {
        var response = await client.PostAsJsonAsync("/api/Gallery", new CreateGalleryRequest { Title = title });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestJson.CaseInsensitive);
        return body.GetProperty("id").GetGuid();
    }

    private static async Task<JsonElement> CreatePhotoRawAsync(
        HttpClient client, Guid galleryId, string title, string? description = null, string? clientTempId = null)
    {
        var response = await client.PostAsJsonAsync("/api/Photo", new CreatePhotoRequest
        {
            GalleryId = galleryId,
            Title = title,
            Description = description,
            ClientTempId = clientTempId,
        });
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>(TestJson.CaseInsensitive);
    }

    private static async Task<Guid> CreatePhotoAsync(
        HttpClient client, Guid galleryId, string title, string? description = null, string? clientTempId = null)
    {
        var body = await CreatePhotoRawAsync(client, galleryId, title, description, clientTempId);
        return body.GetProperty("id").GetGuid();
    }

    private static async Task<List<Guid>> GetGalleryPhotoIdsInOrderAsync(HttpClient client, Guid galleryId)
    {
        var list = await client.GetFromJsonAsync<JsonElement>(
            $"/api/Photo/by-gallery/{galleryId}", TestJson.CaseInsensitive);
        return list.EnumerateArray().Select(p => p.GetProperty("id").GetGuid()).ToList();
    }

    [Fact]
    public async Task Create_AddsPhotoVisibleInGetByGallery()
    {
        using var factory = new TestingWebApplicationFactory();
        var (client, _, _) = await factory.RegisterAndSignInAsync();
        var galleryId = await CreateGalleryAsync(client, "Gallery");

        await CreatePhotoAsync(client, galleryId, "Sunset");

        var ids = await GetGalleryPhotoIdsInOrderAsync(client, galleryId);
        Assert.Single(ids);
    }

    [Fact]
    public async Task Create_ClientTempIdReplay_ReturnsSamePhoto_NotADuplicate()
    {
        using var factory = new TestingWebApplicationFactory();
        var (client, _, _) = await factory.RegisterAndSignInAsync();
        var galleryId = await CreateGalleryAsync(client, "Gallery");

        var first = await CreatePhotoRawAsync(client, galleryId, "Replayed photo", clientTempId: "temp-photo-1");
        var second = await CreatePhotoRawAsync(client, galleryId, "Replayed photo", clientTempId: "temp-photo-1");

        Assert.Equal(first.GetProperty("id").GetGuid(), second.GetProperty("id").GetGuid());
        Assert.Single(await GetGalleryPhotoIdsInOrderAsync(client, galleryId));
    }

    [Fact]
    public async Task Create_ClientTempIdReplay_AfterTheOriginalWasDeleted_ReturnsTheSamePhoto_NotANewOne()
    {
        // FIXED (was a confirmed bug): same reasoning as GalleryCrudTests'
        // analogous test - PhotoController.Create's replay lookup no longer
        // excludes soft-deleted rows, so a retry after deletion returns the
        // same (now-deleted) photo instead of creating a second, duplicate
        // one sharing the ClientTempId.
        using var factory = new TestingWebApplicationFactory();
        var (client, _, _) = await factory.RegisterAndSignInAsync();
        var galleryId = await CreateGalleryAsync(client, "Gallery");

        var firstId = await CreatePhotoAsync(client, galleryId, "Will be deleted", clientTempId: "temp-photo-replay");
        await client.DeleteAsync($"/api/Photo/{firstId}");

        var replay = await CreatePhotoRawAsync(client, galleryId, "Will be deleted", clientTempId: "temp-photo-replay");

        Assert.Equal(firstId, replay.GetProperty("id").GetGuid());
        Assert.True(replay.GetProperty("isDeleted").GetBoolean());

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PhotoDbContext>();
        var sharing = await db.Photos.Where(p => p.ClientTempId == "temp-photo-replay").ToListAsync();
        Assert.Single(sharing);
    }

    [Fact]
    public async Task Create_WithANewClientTempId_StillCreatesANewPhoto()
    {
        using var factory = new TestingWebApplicationFactory();
        var (client, _, _) = await factory.RegisterAndSignInAsync();
        var galleryId = await CreateGalleryAsync(client, "Gallery");

        var firstId = await CreatePhotoAsync(client, galleryId, "First", clientTempId: "temp-a");
        var secondId = await CreatePhotoAsync(client, galleryId, "Second", clientTempId: "temp-b");

        Assert.NotEqual(firstId, secondId);
        Assert.Equal(2, (await GetGalleryPhotoIdsInOrderAsync(client, galleryId)).Count);
    }

    [Fact]
    public async Task GetById_ReturnsMetadataIncludingGalleryIdAndTags()
    {
        using var factory = new TestingWebApplicationFactory();
        var (client, _, _) = await factory.RegisterAndSignInAsync();
        var galleryId = await CreateGalleryAsync(client, "Gallery");
        var photoId = await CreatePhotoAsync(client, galleryId, "A photo", "A description");
        await client.PutAsJsonAsync($"/api/Photo/{photoId}/tags", new SetTagsRequest
        {
            TagNames = new List<string> { "Nature" },
        });

        var response = await client.GetAsync($"/api/Photo/{photoId}");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestJson.CaseInsensitive);

        Assert.Equal(galleryId, body.GetProperty("galleryId").GetGuid());
        Assert.Equal("A photo", body.GetProperty("title").GetString());
        Assert.Equal("A description", body.GetProperty("description").GetString());
        Assert.Equal(new[] { "Nature" }, body.GetProperty("tags").EnumerateArray().Select(t => t.GetString()));
    }

    [Fact]
    public async Task Update_ChangesTitleAndDescription_AndAdvancesUpdatedAtUtc()
    {
        using var factory = new TestingWebApplicationFactory();
        var (client, _, _) = await factory.RegisterAndSignInAsync();
        var galleryId = await CreateGalleryAsync(client, "Gallery");
        var photoId = await CreatePhotoAsync(client, galleryId, "Before edit");

        var before = await client.GetFromJsonAsync<JsonElement>($"/api/Photo/{photoId}", TestJson.CaseInsensitive);
        var updatedAtBefore = before.GetProperty("updatedAtUtc").GetDateTime();

        var update = await client.PutAsJsonAsync($"/api/Photo/{photoId}", new UpdatePhotoRequest
        {
            Title = "After edit",
            Description = "New description",
        });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        var after = await client.GetFromJsonAsync<JsonElement>($"/api/Photo/{photoId}", TestJson.CaseInsensitive);
        Assert.Equal("After edit", after.GetProperty("title").GetString());
        Assert.Equal("New description", after.GetProperty("description").GetString());
        Assert.True(after.GetProperty("updatedAtUtc").GetDateTime() >= updatedAtBefore);
    }

    [Fact]
    public async Task Delete_RemovesFromGetByGallery_ButGetByIdStillShowsItAsDeleted()
    {
        using var factory = new TestingWebApplicationFactory();
        var (client, _, _) = await factory.RegisterAndSignInAsync();
        var galleryId = await CreateGalleryAsync(client, "Gallery");
        var photoId = await CreatePhotoAsync(client, galleryId, "Soon gone");

        var delete = await client.DeleteAsync($"/api/Photo/{photoId}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        Assert.DoesNotContain(photoId, await GetGalleryPhotoIdsInOrderAsync(client, galleryId));

        // Documented, intentional exception (same sync-support reasoning as
        // GalleryController.GetById) - the gallery itself is still live, so
        // GetById still returns the photo's own deleted state.
        var byId = await client.GetAsync($"/api/Photo/{photoId}");
        Assert.Equal(HttpStatusCode.OK, byId.StatusCode);
        var body = await byId.Content.ReadFromJsonAsync<JsonElement>(TestJson.CaseInsensitive);
        Assert.True(body.GetProperty("isDeleted").GetBoolean());
    }

    [Fact]
    public async Task Move_ChangesGalleryId_AndAppendsAtTheEndOfTheDestination()
    {
        using var factory = new TestingWebApplicationFactory();
        var (client, _, _) = await factory.RegisterAndSignInAsync();
        var sourceGalleryId = await CreateGalleryAsync(client, "Source");
        var destGalleryId = await CreateGalleryAsync(client, "Destination");
        await CreatePhotoAsync(client, destGalleryId, "Already there #1");
        await CreatePhotoAsync(client, destGalleryId, "Already there #2");
        var movedPhotoId = await CreatePhotoAsync(client, sourceGalleryId, "Moving");

        var move = await client.PostAsJsonAsync($"/api/Photo/{movedPhotoId}/move", new MovePhotoRequest
        {
            GalleryId = destGalleryId,
        });
        Assert.Equal(HttpStatusCode.NoContent, move.StatusCode);

        Assert.DoesNotContain(movedPhotoId, await GetGalleryPhotoIdsInOrderAsync(client, sourceGalleryId));
        var destOrder = await GetGalleryPhotoIdsInOrderAsync(client, destGalleryId);
        Assert.Equal(movedPhotoId, destOrder[^1]);

        var photo = await client.GetFromJsonAsync<JsonElement>($"/api/Photo/{movedPhotoId}", TestJson.CaseInsensitive);
        Assert.Equal(destGalleryId, photo.GetProperty("galleryId").GetGuid());
    }

    [Fact]
    public async Task Move_DoesNotDisturbTheRelativeOrderOfRemainingSourcePhotos()
    {
        using var factory = new TestingWebApplicationFactory();
        var (client, _, _) = await factory.RegisterAndSignInAsync();
        var sourceGalleryId = await CreateGalleryAsync(client, "Source");
        var destGalleryId = await CreateGalleryAsync(client, "Destination");
        var stayingPhoto1 = await CreatePhotoAsync(client, sourceGalleryId, "Stays #1");
        var movingPhoto = await CreatePhotoAsync(client, sourceGalleryId, "Moves");
        var stayingPhoto2 = await CreatePhotoAsync(client, sourceGalleryId, "Stays #2");

        await client.PostAsJsonAsync($"/api/Photo/{movingPhoto}/move", new MovePhotoRequest
        {
            GalleryId = destGalleryId,
        });

        var remaining = await GetGalleryPhotoIdsInOrderAsync(client, sourceGalleryId);
        Assert.Equal(new[] { stayingPhoto1, stayingPhoto2 }, remaining);
    }

    [Fact]
    public async Task Copy_CreatesAnIndependentPhoto_WithCopiedMetadata_AndTheSameTags()
    {
        // FIXED (was a confirmed gap): Copy now links the new photo to the
        // exact same Tag rows as the source, as an independent association
        // set - not new Tag rows with the same name.
        using var factory = new TestingWebApplicationFactory();
        var (client, _, _) = await factory.RegisterAndSignInAsync();
        var sourceGalleryId = await CreateGalleryAsync(client, "Source");
        var destGalleryId = await CreateGalleryAsync(client, "Destination");
        var sourcePhotoId = await CreatePhotoAsync(client, sourceGalleryId, "Original", "Original description");
        await client.PutAsJsonAsync($"/api/Photo/{sourcePhotoId}/tags", new SetTagsRequest
        {
            TagNames = new List<string> { "Nature", "2026" },
        });

        var copyResponse = await client.PostAsJsonAsync($"/api/Photo/{sourcePhotoId}/copy", new MovePhotoRequest
        {
            GalleryId = destGalleryId,
        });
        Assert.Equal(HttpStatusCode.Created, copyResponse.StatusCode);
        var copyBody = await copyResponse.Content.ReadFromJsonAsync<JsonElement>(TestJson.CaseInsensitive);
        var copyId = copyBody.GetProperty("id").GetGuid();

        Assert.NotEqual(sourcePhotoId, copyId);

        var copy = await client.GetFromJsonAsync<JsonElement>($"/api/Photo/{copyId}", TestJson.CaseInsensitive);
        Assert.Equal("Original", copy.GetProperty("title").GetString());
        Assert.Equal("Original description", copy.GetProperty("description").GetString());
        Assert.Equal(destGalleryId, copy.GetProperty("galleryId").GetGuid());
        var copyTagNames = copy.GetProperty("tags").EnumerateArray().Select(t => t.GetString()).OrderBy(t => t);
        Assert.Equal(new[] { "2026", "Nature" }.OrderBy(t => t), copyTagNames);

        // Not just the same names - the exact same Tag rows (no new Tag
        // created with a duplicate name).
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PhotoDbContext>();
        var sourceTagIds = (await db.Photos.Include(p => p.Tags)
            .FirstAsync(p => p.Id == sourcePhotoId)).Tags.Select(t => t.Id).OrderBy(x => x).ToList();
        var copyTagIds = (await db.Photos.Include(p => p.Tags)
            .FirstAsync(p => p.Id == copyId)).Tags.Select(t => t.Id).OrderBy(x => x).ToList();
        Assert.Equal(sourceTagIds, copyTagIds);
        Assert.Equal(2, await db.Tags.CountAsync());
    }

    [Fact]
    public async Task Copy_OfAPhotoWithNoTags_CopyRemainsWithoutTags()
    {
        using var factory = new TestingWebApplicationFactory();
        var (client, _, _) = await factory.RegisterAndSignInAsync();
        var sourceGalleryId = await CreateGalleryAsync(client, "Source");
        var destGalleryId = await CreateGalleryAsync(client, "Destination");
        var sourcePhotoId = await CreatePhotoAsync(client, sourceGalleryId, "Untagged");

        var copyResponse = await client.PostAsJsonAsync($"/api/Photo/{sourcePhotoId}/copy", new MovePhotoRequest
        {
            GalleryId = destGalleryId,
        });
        var copyBody = await copyResponse.Content.ReadFromJsonAsync<JsonElement>(TestJson.CaseInsensitive);
        var copyId = copyBody.GetProperty("id").GetGuid();

        var copy = await client.GetFromJsonAsync<JsonElement>($"/api/Photo/{copyId}", TestJson.CaseInsensitive);
        Assert.Empty(copy.GetProperty("tags").EnumerateArray());
    }

    [Fact]
    public async Task Copy_ChangingTheOriginalsTagsAfterwards_DoesNotAffectTheCopy()
    {
        using var factory = new TestingWebApplicationFactory();
        var (client, _, _) = await factory.RegisterAndSignInAsync();
        var sourceGalleryId = await CreateGalleryAsync(client, "Source");
        var destGalleryId = await CreateGalleryAsync(client, "Destination");
        var sourcePhotoId = await CreatePhotoAsync(client, sourceGalleryId, "Original");
        await client.PutAsJsonAsync($"/api/Photo/{sourcePhotoId}/tags", new SetTagsRequest
        {
            TagNames = new List<string> { "Before" },
        });

        var copyResponse = await client.PostAsJsonAsync($"/api/Photo/{sourcePhotoId}/copy", new MovePhotoRequest
        {
            GalleryId = destGalleryId,
        });
        var copyId = (await copyResponse.Content.ReadFromJsonAsync<JsonElement>(TestJson.CaseInsensitive))
            .GetProperty("id").GetGuid();

        // Change the original's tags AFTER the copy already exists.
        await client.PutAsJsonAsync($"/api/Photo/{sourcePhotoId}/tags", new SetTagsRequest
        {
            TagNames = new List<string> { "After" },
        });

        var copy = await client.GetFromJsonAsync<JsonElement>($"/api/Photo/{copyId}", TestJson.CaseInsensitive);
        Assert.Equal(new[] { "Before" }, copy.GetProperty("tags").EnumerateArray().Select(t => t.GetString()));
    }

    [Fact]
    public async Task Copy_ClearingTheOriginalsTagsAfterwards_DoesNotAffectTheCopy()
    {
        using var factory = new TestingWebApplicationFactory();
        var (client, _, _) = await factory.RegisterAndSignInAsync();
        var sourceGalleryId = await CreateGalleryAsync(client, "Source");
        var destGalleryId = await CreateGalleryAsync(client, "Destination");
        var sourcePhotoId = await CreatePhotoAsync(client, sourceGalleryId, "Original");
        await client.PutAsJsonAsync($"/api/Photo/{sourcePhotoId}/tags", new SetTagsRequest
        {
            TagNames = new List<string> { "Keepsake" },
        });

        var copyResponse = await client.PostAsJsonAsync($"/api/Photo/{sourcePhotoId}/copy", new MovePhotoRequest
        {
            GalleryId = destGalleryId,
        });
        var copyId = (await copyResponse.Content.ReadFromJsonAsync<JsonElement>(TestJson.CaseInsensitive))
            .GetProperty("id").GetGuid();

        // Clear the original's tags AFTER the copy already exists.
        await client.PutAsJsonAsync($"/api/Photo/{sourcePhotoId}/tags", new SetTagsRequest
        {
            TagNames = new List<string>(),
        });

        var copy = await client.GetFromJsonAsync<JsonElement>($"/api/Photo/{copyId}", TestJson.CaseInsensitive);
        Assert.Equal(new[] { "Keepsake" }, copy.GetProperty("tags").EnumerateArray().Select(t => t.GetString()));
    }

    [Fact]
    public async Task Copy_DoesNotAffectTheSourcePhoto()
    {
        using var factory = new TestingWebApplicationFactory();
        var (client, _, _) = await factory.RegisterAndSignInAsync();
        var sourceGalleryId = await CreateGalleryAsync(client, "Source");
        var destGalleryId = await CreateGalleryAsync(client, "Destination");
        var sourcePhotoId = await CreatePhotoAsync(client, sourceGalleryId, "Original");

        await client.PostAsJsonAsync($"/api/Photo/{sourcePhotoId}/copy", new MovePhotoRequest
        {
            GalleryId = destGalleryId,
        });

        var source = await client.GetFromJsonAsync<JsonElement>($"/api/Photo/{sourcePhotoId}", TestJson.CaseInsensitive);
        Assert.Equal(sourceGalleryId, source.GetProperty("galleryId").GetGuid());
        Assert.Contains(sourcePhotoId, await GetGalleryPhotoIdsInOrderAsync(client, sourceGalleryId));
    }
}
