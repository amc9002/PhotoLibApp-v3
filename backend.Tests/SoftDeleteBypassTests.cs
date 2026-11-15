using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PhotoLibApi.Data;
using PhotoLibApi.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace backend.Tests;

/// <summary>
/// Originally a follow-up investigation after
/// <see cref="GalleryAccessServiceTests"/> found that
/// <c>GalleryAccessService.GetOwnedPhotoAsync</c>'s ownership query had no
/// "not deleted" clause (neither the photo's own <c>IsDeleted</c> flag nor
/// its gallery's). That gap is now fixed, and these HTTP-level tests
/// (through the real backend, no mocks) assert the corrected contract: a
/// soft-deleted photo, or a photo whose gallery is soft-deleted, is now
/// unreachable via every normal operation, with the one deliberate,
/// documented behavior change called out explicitly (see
/// <see cref="DeletePhoto_OnAPhotoWhoseGalleryIsSoftDeleted_NowReturnsNotFound"/>).
/// </summary>
public class SoftDeleteBypassTests
{
    private static byte[] CreateMinimalJpegBytes()
    {
        using var image = new Image<Rgba32>(4, 4);
        using var ms = new MemoryStream();
        image.SaveAsJpeg(ms);
        return ms.ToArray();
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

    private static async Task<HttpResponseMessage> UploadAsync(HttpClient client, Guid photoId)
    {
        using var content = new MultipartFormDataContent();
        using var fileContent = new ByteArrayContent(CreateMinimalJpegBytes());
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        content.Add(fileContent, "file", "test.jpg");

        return await client.PostAsync($"/api/Photo/{photoId}/upload", content);
    }

    [Fact]
    public async Task Upload_OnAnAlreadySoftDeletedPhoto_NowReturnsNotFound()
    {
        using var factory = new TestingWebApplicationFactory();
        var (client, _, _) = await factory.RegisterAndSignInAsync();
        var galleryId = await CreateGalleryAsync(client, "Gallery");
        var photoId = await CreatePhotoAsync(client, galleryId, "Photo");

        var delete = await client.DeleteAsync($"/api/Photo/{photoId}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        var upload = await UploadAsync(client, photoId);

        Assert.Equal(HttpStatusCode.NotFound, upload.StatusCode);
    }

    [Fact]
    public async Task Upload_OnAPhotoWhoseGalleryIsSoftDeleted_NowReturnsNotFound()
    {
        using var factory = new TestingWebApplicationFactory();
        var (client, _, _) = await factory.RegisterAndSignInAsync();
        var galleryId = await CreateGalleryAsync(client, "Gallery");
        var photoId = await CreatePhotoAsync(client, galleryId, "Photo");

        var deleteGallery = await client.DeleteAsync($"/api/Gallery/{galleryId}");
        Assert.Equal(HttpStatusCode.NoContent, deleteGallery.StatusCode);

        var upload = await UploadAsync(client, photoId);

        Assert.Equal(HttpStatusCode.NotFound, upload.StatusCode);
    }

    [Fact]
    public async Task UpdatePhoto_OnAnAlreadySoftDeletedPhoto_NowReturnsNotFound()
    {
        using var factory = new TestingWebApplicationFactory();
        var (client, _, _) = await factory.RegisterAndSignInAsync();
        var galleryId = await CreateGalleryAsync(client, "Gallery");
        var photoId = await CreatePhotoAsync(client, galleryId, "Photo");
        await client.DeleteAsync($"/api/Photo/{photoId}");

        var update = await client.PutAsJsonAsync($"/api/Photo/{photoId}", new UpdatePhotoRequest
        {
            Title = "Edited after deletion",
        });

        Assert.Equal(HttpStatusCode.NotFound, update.StatusCode);
    }

    [Fact]
    public async Task UpdatePhoto_OnAPhotoWhoseGalleryIsSoftDeleted_NowReturnsNotFound()
    {
        using var factory = new TestingWebApplicationFactory();
        var (client, _, _) = await factory.RegisterAndSignInAsync();
        var galleryId = await CreateGalleryAsync(client, "Gallery");
        var photoId = await CreatePhotoAsync(client, galleryId, "Photo");
        await client.DeleteAsync($"/api/Gallery/{galleryId}");

        var update = await client.PutAsJsonAsync($"/api/Photo/{photoId}", new UpdatePhotoRequest
        {
            Title = "Edited after gallery deletion",
        });

        Assert.Equal(HttpStatusCode.NotFound, update.StatusCode);
    }

    [Fact]
    public async Task GetPhotoFile_OnAnAlreadySoftDeletedPhoto_NowReturnsNotFound()
    {
        using var factory = new TestingWebApplicationFactory();
        var (client, _, _) = await factory.RegisterAndSignInAsync();
        var galleryId = await CreateGalleryAsync(client, "Gallery");
        var photoId = await CreatePhotoAsync(client, galleryId, "Photo");
        (await UploadAsync(client, photoId)).EnsureSuccessStatusCode();

        await client.DeleteAsync($"/api/Photo/{photoId}");

        var response = await client.GetAsync($"/api/Photo/{photoId}/file");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task RecordView_OnAnAlreadySoftDeletedPhoto_NowReturnsNotFound()
    {
        using var factory = new TestingWebApplicationFactory();
        var (client, _, _) = await factory.RegisterAndSignInAsync();
        var galleryId = await CreateGalleryAsync(client, "Gallery");
        var photoId = await CreatePhotoAsync(client, galleryId, "Photo");
        await client.DeleteAsync($"/api/Photo/{photoId}");

        var response = await client.PostAsync($"/api/Photo/{photoId}/view", content: null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task SetPhotoTags_IsProtected_WhenPhotosGalleryIsSoftDeleted()
    {
        // Contrast case: SetTags checks ownership via IsOwnedAsync (not
        // GetOwnedPhotoAsync), which DOES filter by gallery.IsDeleted - so,
        // unlike the Update tests above, this one correctly 404s.
        using var factory = new TestingWebApplicationFactory();
        var (client, _, _) = await factory.RegisterAndSignInAsync();
        var galleryId = await CreateGalleryAsync(client, "Gallery");
        var photoId = await CreatePhotoAsync(client, galleryId, "Photo");
        await client.DeleteAsync($"/api/Gallery/{galleryId}");

        var response = await client.PutAsJsonAsync($"/api/Photo/{photoId}/tags", new SetTagsRequest
        {
            TagNames = new List<string> { "still-tagged" },
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeletePhoto_OnAnAlreadySoftDeletedPhoto_ReturnsNotFound()
    {
        // Contrast case: Delete explicitly checks photo.IsDeleted itself
        // after calling GetOwnedPhotoAsync, so a second delete is correctly
        // rejected - unlike Upload/Update/RecordView above, which add no
        // such extra check of their own.
        using var factory = new TestingWebApplicationFactory();
        var (client, _, _) = await factory.RegisterAndSignInAsync();
        var galleryId = await CreateGalleryAsync(client, "Gallery");
        var photoId = await CreatePhotoAsync(client, galleryId, "Photo");
        await client.DeleteAsync($"/api/Photo/{photoId}");

        var secondDelete = await client.DeleteAsync($"/api/Photo/{photoId}");

        Assert.Equal(HttpStatusCode.NotFound, secondDelete.StatusCode);
    }

    [Fact]
    public async Task MovePhoto_OutOfASoftDeletedGallery_NowReturnsNotFound()
    {
        // Was: Move checked photo.IsDeleted (false here) but not the SOURCE
        // gallery's IsDeleted, so a photo "orphaned" by its gallery being
        // soft-deleted could still be moved out into a live gallery. Now
        // that GetOwnedPhotoAsync itself filters the source gallery's
        // IsDeleted too, this "rescue path" is closed - per requirement 1,
        // a soft-deleted gallery's photos are inaccessible to ALL normal
        // operations, including Move.
        using var factory = new TestingWebApplicationFactory();
        var (client, _, _) = await factory.RegisterAndSignInAsync();
        var deletedGalleryId = await CreateGalleryAsync(client, "Soon-deleted gallery");
        var liveGalleryId = await CreateGalleryAsync(client, "Live gallery");
        var photoId = await CreatePhotoAsync(client, deletedGalleryId, "Orphaned photo");

        await client.DeleteAsync($"/api/Gallery/{deletedGalleryId}");

        var move = await client.PostAsJsonAsync($"/api/Photo/{photoId}/move", new MovePhotoRequest
        {
            GalleryId = liveGalleryId,
        });

        Assert.Equal(HttpStatusCode.NotFound, move.StatusCode);
    }

    [Fact]
    public async Task DeletePhoto_OnAPhotoWhoseGalleryIsSoftDeleted_NowReturnsNotFound()
    {
        // The one deliberate behavior change from the fix: previously,
        // deleting a photo whose gallery was already soft-deleted (but the
        // photo's own IsDeleted was still false) silently "succeeded"
        // (204) as a mostly-no-op mutation. Per requirement 1 (a
        // soft-deleted gallery's photos are inaccessible to ALL normal
        // operations, and Delete counts as one), this now correctly 404s
        // instead.
        using var factory = new TestingWebApplicationFactory();
        var (client, _, _) = await factory.RegisterAndSignInAsync();
        var galleryId = await CreateGalleryAsync(client, "Gallery");
        var photoId = await CreatePhotoAsync(client, galleryId, "Photo");
        await client.DeleteAsync($"/api/Gallery/{galleryId}");

        var deletePhoto = await client.DeleteAsync($"/api/Photo/{photoId}");

        Assert.Equal(HttpStatusCode.NotFound, deletePhoto.StatusCode);
    }

    [Fact]
    public async Task GalleryDelete_DoesNotCascadeIsDeletedToItsPhotos()
    {
        // Root-cause check for the whole investigation: soft-deleting a
        // gallery only ever sets the gallery's own IsDeleted - it never
        // touches any of its photos' IsDeleted flags.
        using var factory = new TestingWebApplicationFactory();
        var (client, _, _) = await factory.RegisterAndSignInAsync();
        var galleryId = await CreateGalleryAsync(client, "Gallery");
        var photoId = await CreatePhotoAsync(client, galleryId, "Photo");

        await client.DeleteAsync($"/api/Gallery/{galleryId}");

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PhotoDbContext>();
        var photo = await db.Photos.FindAsync(photoId);

        Assert.False(photo!.IsDeleted);
    }
}
