using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PhotoLibApi.Data;
using PhotoLibApi.Models;

namespace backend.Tests;

/// <summary>
/// HTTP-level tests of <c>DiscoveryController</c> and the <c>RecordView</c>
/// endpoint it depends on for personalization. The individually-deleted-
/// photo case for <c>RecordView</c> is already covered by
/// <see cref="SoftDeleteBypassTests"/> and is not repeated here; this file
/// adds the previously-untested "photo's gallery is deleted" case plus the
/// Discovery-specific ownership/isolation and no-dedup confirmations.
/// </summary>
public class DiscoveryTests
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
    public async Task GetFeed_CountZero_IsClampedToOne()
    {
        using var factory = new TestingWebApplicationFactory();
        var (client, _, _) = await factory.RegisterAndSignInAsync();
        var galleryId = await CreateGalleryAsync(client, "Gallery");
        await CreatePhotoAsync(client, galleryId, "A");
        await CreatePhotoAsync(client, galleryId, "B");

        var response = await client.GetAsync("/api/Discovery/feed?count=0");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestJson.CaseInsensitive);

        Assert.Equal(1, body.GetArrayLength());
    }

    [Fact]
    public async Task GetFeed_NegativeCount_IsClampedToOne()
    {
        using var factory = new TestingWebApplicationFactory();
        var (client, _, _) = await factory.RegisterAndSignInAsync();
        var galleryId = await CreateGalleryAsync(client, "Gallery");
        await CreatePhotoAsync(client, galleryId, "A");

        var response = await client.GetAsync("/api/Discovery/feed?count=-5");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestJson.CaseInsensitive);

        Assert.Equal(1, body.GetArrayLength());
    }

    [Fact]
    public async Task GetFeed_CountAboveMaximum_IsClampedToSixty()
    {
        using var factory = new TestingWebApplicationFactory();
        var (client, _, _) = await factory.RegisterAndSignInAsync();
        var galleryId = await CreateGalleryAsync(client, "Gallery");
        for (var i = 0; i < 61; i++)
        {
            await CreatePhotoAsync(client, galleryId, $"Photo {i}");
        }

        var response = await client.GetAsync("/api/Discovery/feed?count=1000");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestJson.CaseInsensitive);

        Assert.Equal(60, body.GetArrayLength());
    }

    [Fact]
    public async Task GetFeed_OnlyEverReturnsTheCallersOwnPhotos()
    {
        using var factory = new TestingWebApplicationFactory();
        var (ownerClient, _, _) = await factory.RegisterAndSignInAsync("owner");
        var ownerGalleryId = await CreateGalleryAsync(ownerClient, "Owner's gallery");
        var ownerPhoto1 = await CreatePhotoAsync(ownerClient, ownerGalleryId, "Owner 1");
        var ownerPhoto2 = await CreatePhotoAsync(ownerClient, ownerGalleryId, "Owner 2");

        var (otherClient, _, _) = await factory.RegisterAndSignInAsync("other");
        var otherGalleryId = await CreateGalleryAsync(otherClient, "Other's gallery");
        await CreatePhotoAsync(otherClient, otherGalleryId, "Other 1");

        var response = await ownerClient.GetAsync("/api/Discovery/feed?count=60");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestJson.CaseInsensitive);
        var ids = body.EnumerateArray().Select(p => p.GetProperty("id").GetGuid()).ToList();

        Assert.Equal(
            new[] { ownerPhoto1, ownerPhoto2 }.OrderBy(x => x),
            ids.OrderBy(x => x));
    }

    [Fact]
    public async Task RecordView_ForOwnLivePhoto_InsertsAPhotoViewRow()
    {
        using var factory = new TestingWebApplicationFactory();
        var (client, userId, _) = await factory.RegisterAndSignInAsync();
        var galleryId = await CreateGalleryAsync(client, "Gallery");
        var photoId = await CreatePhotoAsync(client, galleryId, "Photo");

        var response = await client.PostAsync($"/api/Photo/{photoId}/view", content: null);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PhotoDbContext>();
        var views = await db.PhotoViews.Where(v => v.PhotoId == photoId && v.UserId == userId).ToListAsync();
        Assert.Single(views);
    }

    [Fact]
    public async Task RecordView_ForPhotoWhoseGalleryIsSoftDeleted_ReturnsNotFound()
    {
        // Gap not previously covered: SoftDeleteBypassTests exercised
        // RecordView against an individually soft-deleted photo, but not
        // against a photo whose GALLERY was deleted instead.
        using var factory = new TestingWebApplicationFactory();
        var (client, _, _) = await factory.RegisterAndSignInAsync();
        var galleryId = await CreateGalleryAsync(client, "Gallery");
        var photoId = await CreatePhotoAsync(client, galleryId, "Photo");
        await client.DeleteAsync($"/api/Gallery/{galleryId}");

        var response = await client.PostAsync($"/api/Photo/{photoId}/view", content: null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task RecordView_ForAnotherUsersPhoto_ReturnsNotFound()
    {
        using var factory = new TestingWebApplicationFactory();
        var (owner, _, _) = await factory.RegisterAndSignInAsync("owner");
        var galleryId = await CreateGalleryAsync(owner, "Owner's gallery");
        var photoId = await CreatePhotoAsync(owner, galleryId, "Owner's photo");

        var (intruder, _, _) = await factory.RegisterAndSignInAsync("intruder");
        var response = await intruder.PostAsync($"/api/Photo/{photoId}/view", content: null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task RecordView_CalledTwiceForTheSamePhoto_CreatesTwoSeparatePhotoViewRows()
    {
        // Confirms the product decision to NOT throttle or deduplicate -
        // every call is recorded as its own, independent signal.
        using var factory = new TestingWebApplicationFactory();
        var (client, userId, _) = await factory.RegisterAndSignInAsync();
        var galleryId = await CreateGalleryAsync(client, "Gallery");
        var photoId = await CreatePhotoAsync(client, galleryId, "Photo");

        await client.PostAsync($"/api/Photo/{photoId}/view", content: null);
        await client.PostAsync($"/api/Photo/{photoId}/view", content: null);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PhotoDbContext>();
        var views = await db.PhotoViews.Where(v => v.PhotoId == photoId && v.UserId == userId).ToListAsync();
        Assert.Equal(2, views.Count);
    }
}
