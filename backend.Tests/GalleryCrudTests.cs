using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PhotoLibApi.Data;
using PhotoLibApi.Models;

namespace backend.Tests;

/// <summary>
/// Gallery create/read/update/delete contract, driven entirely through real
/// HTTP calls. Cross-user isolation is already covered by
/// <see cref="OwnershipIsolationTests"/> and is not repeated here; this file
/// focuses on same-user CRUD correctness and the soft-delete contract
/// specific to <c>GalleryController</c>.
/// </summary>
public class GalleryCrudTests
{
    private static async Task<JsonElement> CreateGalleryRawAsync(HttpClient client, string title, string? clientTempId = null)
    {
        var response = await client.PostAsJsonAsync("/api/Gallery", new CreateGalleryRequest
        {
            Title = title,
            ClientTempId = clientTempId,
        });
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>(TestJson.CaseInsensitive);
    }

    private static async Task<Guid> CreateGalleryAsync(HttpClient client, string title, string? clientTempId = null)
    {
        var body = await CreateGalleryRawAsync(client, title, clientTempId);
        return body.GetProperty("id").GetGuid();
    }

    [Fact]
    public async Task Create_AddsGalleryVisibleInGetAll()
    {
        using var factory = new TestingWebApplicationFactory();
        var (client, _, _) = await factory.RegisterAndSignInAsync();

        await CreateGalleryAsync(client, "Vacation 2026");

        var all = await client.GetFromJsonAsync<JsonElement>("/api/Gallery", TestJson.CaseInsensitive);
        var titles = all.EnumerateArray().Select(g => g.GetProperty("title").GetString()).ToList();

        Assert.Contains("Vacation 2026", titles);
    }

    [Fact]
    public async Task Create_ClientTempIdReplay_ReturnsSameGallery_NotADuplicate()
    {
        using var factory = new TestingWebApplicationFactory();
        var (client, _, _) = await factory.RegisterAndSignInAsync();

        var first = await CreateGalleryRawAsync(client, "Replayed gallery", "temp-123");
        var second = await CreateGalleryRawAsync(client, "Replayed gallery", "temp-123");

        Assert.Equal(first.GetProperty("id").GetGuid(), second.GetProperty("id").GetGuid());

        var all = await client.GetFromJsonAsync<JsonElement>("/api/Gallery", TestJson.CaseInsensitive);
        Assert.Equal(1, all.GetArrayLength());
    }

    [Fact]
    public async Task Create_ClientTempIdReplay_AfterTheOriginalWasDeleted_ReturnsTheSameGallery_NotANewOne()
    {
        // FIXED (was a confirmed bug): ClientTempId identifies the create
        // *operation*, not the object's current state - the replay lookup
        // in GalleryController.Create no longer excludes soft-deleted rows,
        // so retrying the same create after the original was deleted
        // returns that same (now-deleted) row instead of creating a second,
        // duplicate one sharing the ClientTempId.
        using var factory = new TestingWebApplicationFactory();
        var (client, _, _) = await factory.RegisterAndSignInAsync();

        var firstId = await CreateGalleryAsync(client, "Will be deleted", "temp-replay-after-delete");
        var delete = await client.DeleteAsync($"/api/Gallery/{firstId}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        var replay = await CreateGalleryRawAsync(client, "Will be deleted", "temp-replay-after-delete");

        Assert.Equal(firstId, replay.GetProperty("id").GetGuid());
        // The replay's own response correctly reflects the deleted state -
        // the caller can see this isn't a fresh, live gallery.
        Assert.True(replay.GetProperty("isDeleted").GetBoolean());

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PhotoDbContext>();
        var sharingClientTempId = await db.Galleries
            .Where(g => g.ClientTempId == "temp-replay-after-delete")
            .ToListAsync();
        Assert.Single(sharingClientTempId);
    }

    [Fact]
    public async Task Create_WithANewClientTempId_StillCreatesANewGallery()
    {
        // Contrast case: a genuinely new ClientTempId (as the frontend
        // always generates for an actually-new create attempt) must never
        // be treated as a replay of an unrelated earlier one.
        using var factory = new TestingWebApplicationFactory();
        var (client, _, _) = await factory.RegisterAndSignInAsync();

        var firstId = await CreateGalleryAsync(client, "First", "temp-a");
        var secondId = await CreateGalleryAsync(client, "Second", "temp-b");

        Assert.NotEqual(firstId, secondId);
        var all = await client.GetFromJsonAsync<JsonElement>("/api/Gallery", TestJson.CaseInsensitive);
        Assert.Equal(2, all.GetArrayLength());
    }

    [Fact]
    public async Task GetById_ReturnsTitleDescriptionAndTags()
    {
        using var factory = new TestingWebApplicationFactory();
        var (client, _, _) = await factory.RegisterAndSignInAsync();
        var galleryId = await CreateGalleryAsync(client, "Original title");

        await client.PutAsJsonAsync($"/api/Gallery/{galleryId}", new UpdateGalleryRequest
        {
            Title = "Original title",
            Description = "A short description",
        });
        await client.PutAsJsonAsync($"/api/Gallery/{galleryId}/tags", new SetTagsRequest
        {
            TagNames = new List<string> { "Travel", "2026" },
        });

        var response = await client.GetAsync($"/api/Gallery/{galleryId}");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestJson.CaseInsensitive);

        Assert.Equal("Original title", body.GetProperty("title").GetString());
        Assert.Equal("A short description", body.GetProperty("description").GetString());
        // Order-independent on purpose: GalleryController.GetById's Tags
        // projection has no OrderBy, and EF Core materializes the join
        // ordered by each Tag's own (randomly-generated) Guid - so the
        // actual order is not part of the real contract. Still asserts
        // both expected tags are present, not just a matching count.
        var tags = body.GetProperty("tags").EnumerateArray().Select(t => t.GetString()).ToList();
        Assert.Equal(new[] { "2026", "Travel" }.OrderBy(t => t), tags.OrderBy(t => t));
    }

    [Fact]
    public async Task Update_ChangesTitleAndDescription_AndAdvancesUpdatedAtUtc()
    {
        using var factory = new TestingWebApplicationFactory();
        var (client, _, _) = await factory.RegisterAndSignInAsync();
        var galleryId = await CreateGalleryAsync(client, "Before edit");

        var before = await client.GetFromJsonAsync<JsonElement>($"/api/Gallery/{galleryId}", TestJson.CaseInsensitive);
        var updatedAtBefore = before.GetProperty("updatedAtUtc").GetDateTime();

        var update = await client.PutAsJsonAsync($"/api/Gallery/{galleryId}", new UpdateGalleryRequest
        {
            Title = "After edit",
            Description = "New description",
        });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        var after = await client.GetFromJsonAsync<JsonElement>($"/api/Gallery/{galleryId}", TestJson.CaseInsensitive);
        Assert.Equal("After edit", after.GetProperty("title").GetString());
        Assert.Equal("New description", after.GetProperty("description").GetString());
        Assert.True(after.GetProperty("updatedAtUtc").GetDateTime() >= updatedAtBefore);
    }

    [Fact]
    public async Task Delete_RemovesFromGetAll_ButGetByIdStillShowsItAsDeleted()
    {
        using var factory = new TestingWebApplicationFactory();
        var (client, _, _) = await factory.RegisterAndSignInAsync();
        var galleryId = await CreateGalleryAsync(client, "Soon gone");

        var delete = await client.DeleteAsync($"/api/Gallery/{galleryId}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        var all = await client.GetFromJsonAsync<JsonElement>("/api/Gallery", TestJson.CaseInsensitive);
        var ids = all.EnumerateArray().Select(g => g.GetProperty("id").GetGuid());
        Assert.DoesNotContain(galleryId, ids);

        // Documented, intentional exception (sync needs to tell "deleted"
        // apart from "never existed/not yours") - GetById still returns it.
        var byId = await client.GetAsync($"/api/Gallery/{galleryId}");
        Assert.Equal(HttpStatusCode.OK, byId.StatusCode);
        var body = await byId.Content.ReadFromJsonAsync<JsonElement>(TestJson.CaseInsensitive);
        Assert.True(body.GetProperty("isDeleted").GetBoolean());
    }

    [Fact]
    public async Task Delete_OnAnAlreadyDeletedGallery_ReturnsNotFound()
    {
        using var factory = new TestingWebApplicationFactory();
        var (client, _, _) = await factory.RegisterAndSignInAsync();
        var galleryId = await CreateGalleryAsync(client, "Deleted twice");
        await client.DeleteAsync($"/api/Gallery/{galleryId}");

        var secondDelete = await client.DeleteAsync($"/api/Gallery/{galleryId}");

        Assert.Equal(HttpStatusCode.NotFound, secondDelete.StatusCode);
    }

    [Fact]
    public async Task Update_OnAnAlreadyDeletedGallery_ReturnsNotFound()
    {
        using var factory = new TestingWebApplicationFactory();
        var (client, _, _) = await factory.RegisterAndSignInAsync();
        var galleryId = await CreateGalleryAsync(client, "Deleted");
        await client.DeleteAsync($"/api/Gallery/{galleryId}");

        var update = await client.PutAsJsonAsync($"/api/Gallery/{galleryId}", new UpdateGalleryRequest
        {
            Title = "Trying to edit a deleted gallery",
        });

        Assert.Equal(HttpStatusCode.NotFound, update.StatusCode);
    }

    [Fact]
    public async Task SetTags_OnAnAlreadyDeletedGallery_ReturnsNotFound()
    {
        using var factory = new TestingWebApplicationFactory();
        var (client, _, _) = await factory.RegisterAndSignInAsync();
        var galleryId = await CreateGalleryAsync(client, "Deleted");
        await client.DeleteAsync($"/api/Gallery/{galleryId}");

        var response = await client.PutAsJsonAsync($"/api/Gallery/{galleryId}/tags", new SetTagsRequest
        {
            TagNames = new List<string> { "still-tagged" },
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
