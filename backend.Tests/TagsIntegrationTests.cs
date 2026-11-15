using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PhotoLibApi.Models;

namespace backend.Tests;

/// <summary>
/// HTTP-level contract of <c>SetTags</c> for both <c>GalleryController</c>
/// and <c>PhotoController</c> - response shape, clearing, and sequential
/// (non-racing) reuse of an already-existing tag. Ownership/soft-delete
/// gating on these endpoints is already covered by
/// <see cref="OwnershipIsolationTests"/>, <see cref="GalleryCrudTests"/>
/// and <see cref="SoftDeleteBypassTests"/> and is not repeated here.
/// </summary>
public class TagsIntegrationTests
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

    private static IEnumerable<string?> TagNamesOf(JsonElement body) =>
        body.GetProperty("tagNames").EnumerateArray().Select(t => t.GetString());

    [Fact]
    public async Task SetGalleryTags_ReturnsTagNamesAndUpdatedAtUtc_AndIsVisibleViaGetById()
    {
        using var factory = new TestingWebApplicationFactory();
        var (client, _, _) = await factory.RegisterAndSignInAsync();
        var galleryId = await CreateGalleryAsync(client, "Gallery");

        var response = await client.PutAsJsonAsync($"/api/Gallery/{galleryId}/tags", new SetTagsRequest
        {
            TagNames = new List<string> { "Travel" },
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestJson.CaseInsensitive);
        Assert.Contains("Travel", TagNamesOf(body));
        Assert.True(body.TryGetProperty("updatedAtUtc", out _));

        var byId = await client.GetFromJsonAsync<JsonElement>($"/api/Gallery/{galleryId}", TestJson.CaseInsensitive);
        Assert.Contains("Travel", byId.GetProperty("tags").EnumerateArray().Select(t => t.GetString()));
    }

    [Fact]
    public async Task SetGalleryTags_CalledASecondTimeWithAnEmptyList_RemovesThePreviousAssociation_ButTheOldTagRowRemains()
    {
        // FIXED (was a confirmed bug - see the project's testing-stage
        // history): GalleryController.SetTags now loads the gallery via
        // GalleryAccessService.GetOwnedGalleryWithTagsAsync, which includes
        // the Tags navigation, so EF Core's change tracker can correctly
        // diff the old association away. The old Tag *row* itself is still
        // never deleted - Tag has no cascade-delete-when-unused logic, by
        // design (see TagResolverTests) - only the gallery's link to it.
        using var factory = new TestingWebApplicationFactory();
        var (client, _, _) = await factory.RegisterAndSignInAsync();
        var galleryId = await CreateGalleryAsync(client, "Gallery");
        await client.PutAsJsonAsync($"/api/Gallery/{galleryId}/tags", new SetTagsRequest
        {
            TagNames = new List<string> { "Travel" },
        });

        var clear = await client.PutAsJsonAsync($"/api/Gallery/{galleryId}/tags", new SetTagsRequest
        {
            TagNames = new List<string>(),
        });
        Assert.Equal(HttpStatusCode.OK, clear.StatusCode);
        var clearBody = await clear.Content.ReadFromJsonAsync<JsonElement>(TestJson.CaseInsensitive);
        Assert.Empty(TagNamesOf(clearBody));

        // A fresh read now agrees with the response: no tags left.
        var byId = await client.GetFromJsonAsync<JsonElement>($"/api/Gallery/{galleryId}", TestJson.CaseInsensitive);
        Assert.Empty(byId.GetProperty("tags").EnumerateArray());

        // The old "Travel" Tag row itself still exists in the Tags table -
        // only the gallery's association with it was removed.
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PhotoLibApi.Data.PhotoDbContext>();
        Assert.True(await db.Tags.AnyAsync(t => t.Name == "Travel"));
    }

    [Fact]
    public async Task SetGalleryTags_CalledTwiceWithDifferentTags_ReplacesInsteadOfAccumulating()
    {
        // FIXED: changing a gallery's tags from ["A"] to ["B"] now actually
        // leaves only "B" attached. "A"'s own Tag row still exists (not
        // deleted, same reasoning as the test above) - only the gallery's
        // link to it is gone.
        using var factory = new TestingWebApplicationFactory();
        var (client, _, _) = await factory.RegisterAndSignInAsync();
        var galleryId = await CreateGalleryAsync(client, "Gallery");
        await client.PutAsJsonAsync($"/api/Gallery/{galleryId}/tags", new SetTagsRequest
        {
            TagNames = new List<string> { "A" },
        });

        var second = await client.PutAsJsonAsync($"/api/Gallery/{galleryId}/tags", new SetTagsRequest
        {
            TagNames = new List<string> { "B" },
        });
        var secondBody = await second.Content.ReadFromJsonAsync<JsonElement>(TestJson.CaseInsensitive);
        Assert.Equal(new[] { "B" }, TagNamesOf(secondBody));

        var byId = await client.GetFromJsonAsync<JsonElement>($"/api/Gallery/{galleryId}", TestJson.CaseInsensitive);
        Assert.Equal(new[] { "B" }, byId.GetProperty("tags").EnumerateArray().Select(t => t.GetString()));

        // "A"'s Tag row is still there - it's just no longer linked to this
        // gallery (the join row, not the Tag itself, is what got removed).
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PhotoLibApi.Data.PhotoDbContext>();
        Assert.True(await db.Tags.AnyAsync(t => t.Name == "A"));
    }

    [Fact]
    public async Task SetGalleryTags_OnASecondGallery_ReusesTheTagCreatedByTheFirst()
    {
        // Sequential (non-racing) reuse - the counterpart to
        // TagResolverTests' deterministic race simulation, which covers the
        // concurrent case separately.
        using var factory = new TestingWebApplicationFactory();
        var (client, _, _) = await factory.RegisterAndSignInAsync();
        var galleryA = await CreateGalleryAsync(client, "A");
        var galleryB = await CreateGalleryAsync(client, "B");

        await client.PutAsJsonAsync($"/api/Gallery/{galleryA}/tags", new SetTagsRequest
        {
            TagNames = new List<string> { "Shared" },
        });
        await client.PutAsJsonAsync($"/api/Gallery/{galleryB}/tags", new SetTagsRequest
        {
            TagNames = new List<string> { "shared" }, // different case, same tag
        });

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PhotoLibApi.Data.PhotoDbContext>();
        var sharedTags = db.Tags.Where(t => t.Name.ToLower() == "shared").ToList();

        Assert.Single(sharedTags);
    }

    [Fact]
    public async Task SetPhotoTags_ReturnsTagNamesAndUpdatedAtUtc_AndIsVisibleViaGetById()
    {
        using var factory = new TestingWebApplicationFactory();
        var (client, _, _) = await factory.RegisterAndSignInAsync();
        var galleryId = await CreateGalleryAsync(client, "Gallery");
        var photoId = await CreatePhotoAsync(client, galleryId, "Photo");

        var response = await client.PutAsJsonAsync($"/api/Photo/{photoId}/tags", new SetTagsRequest
        {
            TagNames = new List<string> { "Nature" },
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestJson.CaseInsensitive);
        Assert.Contains("Nature", TagNamesOf(body));
        Assert.True(body.TryGetProperty("updatedAtUtc", out _));

        var byId = await client.GetFromJsonAsync<JsonElement>($"/api/Photo/{photoId}", TestJson.CaseInsensitive);
        Assert.Contains("Nature", byId.GetProperty("tags").EnumerateArray().Select(t => t.GetString()));
    }

    [Fact]
    public async Task SetPhotoTags_WithAnEmptyList_ClearsAllTags()
    {
        using var factory = new TestingWebApplicationFactory();
        var (client, _, _) = await factory.RegisterAndSignInAsync();
        var galleryId = await CreateGalleryAsync(client, "Gallery");
        var photoId = await CreatePhotoAsync(client, galleryId, "Photo");
        await client.PutAsJsonAsync($"/api/Photo/{photoId}/tags", new SetTagsRequest
        {
            TagNames = new List<string> { "Nature" },
        });

        var clear = await client.PutAsJsonAsync($"/api/Photo/{photoId}/tags", new SetTagsRequest
        {
            TagNames = new List<string>(),
        });
        Assert.Equal(HttpStatusCode.OK, clear.StatusCode);
        var body = await clear.Content.ReadFromJsonAsync<JsonElement>(TestJson.CaseInsensitive);
        Assert.Empty(TagNamesOf(body));

        var byId = await client.GetFromJsonAsync<JsonElement>($"/api/Photo/{photoId}", TestJson.CaseInsensitive);
        Assert.Empty(byId.GetProperty("tags").EnumerateArray());
    }

    [Fact]
    public async Task SetPhotoTags_OnASecondPhoto_ReusesTheTagCreatedByTheFirst()
    {
        using var factory = new TestingWebApplicationFactory();
        var (client, _, _) = await factory.RegisterAndSignInAsync();
        var galleryId = await CreateGalleryAsync(client, "Gallery");
        var photoA = await CreatePhotoAsync(client, galleryId, "A");
        var photoB = await CreatePhotoAsync(client, galleryId, "B");

        await client.PutAsJsonAsync($"/api/Photo/{photoA}/tags", new SetTagsRequest
        {
            TagNames = new List<string> { "Shared" },
        });
        await client.PutAsJsonAsync($"/api/Photo/{photoB}/tags", new SetTagsRequest
        {
            TagNames = new List<string> { "SHARED" }, // different case, same tag
        });

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PhotoLibApi.Data.PhotoDbContext>();
        var sharedTags = db.Tags.Where(t => t.Name.ToLower() == "shared").ToList();

        Assert.Single(sharedTags);
    }
}
