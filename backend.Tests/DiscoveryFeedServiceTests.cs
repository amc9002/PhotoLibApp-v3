using PhotoLibApi.Models;
using PhotoLibApi.Services;

namespace backend.Tests;

/// <summary>
/// Direct, HTTP-free tests of <see cref="DiscoveryFeedService"/>. Most of
/// these are fully deterministic - pool composition, counts, and the
/// exclude-list fallback never depend on randomness at all, because that
/// filtering happens *before* the weighted draw. The one property that
/// genuinely cannot be checked deterministically (the exact effect of the
/// view-count-based weight formula) is called out explicitly below, with a
/// statistical check designed to have a negligible false-failure rate
/// rather than just a "probably fine" margin.
/// </summary>
public class DiscoveryFeedServiceTests
{
    private static (Guid GalleryId, Guid OwnerId) SeedGallery(TestDb db, Guid? ownerId = null)
    {
        var owner = ownerId ?? Guid.NewGuid();
        var galleryId = Guid.NewGuid();
        db.Context.Galleries.Add(new Gallery { Id = galleryId, Title = "Gallery", OwnerId = owner });
        return (galleryId, owner);
    }

    private static Photo SeedPhoto(TestDb db, Guid galleryId, string title, Tag? tag = null)
    {
        var photo = new Photo { Id = Guid.NewGuid(), GalleryId = galleryId, Title = title };
        if (tag != null) photo.Tags.Add(tag);
        db.Context.Photos.Add(photo);
        return photo;
    }

    [Fact]
    public async Task GetFeedAsync_EmptyCandidatePool_ReturnsEmptyList()
    {
        using var db = new TestDb();
        var service = new DiscoveryFeedService(db.Context, new GalleryAccessService(db.Context));

        var result = await service.GetFeedAsync(Guid.NewGuid(), 10, new HashSet<Guid>());

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetFeedAsync_NeverReturnsAnotherUsersPhotos()
    {
        using var db = new TestDb();
        var (galleryA, userA) = SeedGallery(db);
        var (galleryB, _) = SeedGallery(db);
        var a1 = SeedPhoto(db, galleryA, "A1");
        var a2 = SeedPhoto(db, galleryA, "A2");
        SeedPhoto(db, galleryB, "B1");
        await db.Context.SaveChangesAsync();
        var service = new DiscoveryFeedService(db.Context, new GalleryAccessService(db.Context));

        var result = await service.GetFeedAsync(userA, 10, new HashSet<Guid>());

        Assert.Equal(
            new[] { a1.Id, a2.Id }.OrderBy(x => x),
            result.Select(p => p.Id).OrderBy(x => x));
    }

    [Fact]
    public async Task GetFeedAsync_ResultCount_NeverExceedsRequestedCountOrPoolSize()
    {
        using var db = new TestDb();
        var (galleryId, ownerId) = SeedGallery(db);
        SeedPhoto(db, galleryId, "1");
        SeedPhoto(db, galleryId, "2");
        SeedPhoto(db, galleryId, "3");
        await db.Context.SaveChangesAsync();
        var service = new DiscoveryFeedService(db.Context, new GalleryAccessService(db.Context));

        Assert.Equal(2, (await service.GetFeedAsync(ownerId, 2, new HashSet<Guid>())).Count);
        Assert.Equal(3, (await service.GetFeedAsync(ownerId, 10, new HashSet<Guid>())).Count);
    }

    [Fact]
    public async Task GetFeedAsync_ExcludeIsRespected_WhenTheRemainingPoolIsLargeEnough()
    {
        // Deterministic: the exclude-filtering happens before any random
        // draw, and the pool left after exclusion here has exactly as many
        // items as requested - Take(3) over a pool of exactly 3 always
        // returns all of them, regardless of the random key ordering.
        using var db = new TestDb();
        var (galleryId, ownerId) = SeedGallery(db);
        var excluded1 = SeedPhoto(db, galleryId, "Excluded1");
        var excluded2 = SeedPhoto(db, galleryId, "Excluded2");
        var kept1 = SeedPhoto(db, galleryId, "Kept1");
        var kept2 = SeedPhoto(db, galleryId, "Kept2");
        var kept3 = SeedPhoto(db, galleryId, "Kept3");
        await db.Context.SaveChangesAsync();
        var service = new DiscoveryFeedService(db.Context, new GalleryAccessService(db.Context));

        var result = await service.GetFeedAsync(ownerId, 3,
            new HashSet<Guid> { excluded1.Id, excluded2.Id });

        Assert.Equal(
            new[] { kept1.Id, kept2.Id, kept3.Id }.OrderBy(x => x),
            result.Select(p => p.Id).OrderBy(x => x));
    }

    [Fact]
    public async Task GetFeedAsync_ExcludeIsIgnored_WhenTheRemainingPoolWouldBeSmallerThanRequested()
    {
        // Deterministic: excluding all 3 candidates leaves an empty pool,
        // below the requested count of 3, so GetFeedAsync falls back to the
        // full candidate list - all 3 must come back regardless of the
        // random draw.
        using var db = new TestDb();
        var (galleryId, ownerId) = SeedGallery(db);
        var a = SeedPhoto(db, galleryId, "A");
        var b = SeedPhoto(db, galleryId, "B");
        var c = SeedPhoto(db, galleryId, "C");
        await db.Context.SaveChangesAsync();
        var service = new DiscoveryFeedService(db.Context, new GalleryAccessService(db.Context));

        var result = await service.GetFeedAsync(ownerId, 3,
            new HashSet<Guid> { a.Id, b.Id, c.Id });

        Assert.Equal(3, result.Count);
    }

    [Fact]
    public async Task GetFeedAsync_PhotoWithNoRecordedViews_IsStillSelectable()
    {
        // Deterministic: the only candidate, with a base weight that's
        // never zero (1.0 + 0, per the implemented formula) and count=1 -
        // it must be returned regardless of the random draw.
        using var db = new TestDb();
        var (galleryId, ownerId) = SeedGallery(db);
        var onlyPhoto = SeedPhoto(db, galleryId, "Never viewed");
        await db.Context.SaveChangesAsync();
        var service = new DiscoveryFeedService(db.Context, new GalleryAccessService(db.Context));

        var result = await service.GetFeedAsync(ownerId, 1, new HashSet<Guid>());

        Assert.Equal(onlyPhoto.Id, result.Single().Id);
    }

    [Fact]
    public async Task GetFeedAsync_TagViewHistory_StatisticallyBiasesSelectionTowardThatTag()
    {
        // NOT deterministically checkable without a production-code change:
        // the exact weight formula lives in a private local function inside
        // GetFeedAsync, and the sampling step uses Random.Shared directly -
        // neither is exposed or injectable, and changing that is out of
        // scope for this stage. This is therefore the one necessarily
        // statistical test in this file, deliberately designed to have a
        // negligible false-failure rate rather than a merely "probably
        // fine" margin:
        //
        // "Heavy" has one tag, viewed 25 times by the owner -> boost =
        // min(25 * 0.15, 3.0) = 3.0 (the cap), so weight = 4.0. "Plain" has
        // no tags/views, weight = 1.0. For a 2-candidate, count=1 draw,
        // P(Heavy wins) = 4.0 / (4.0 + 1.0) = 0.8. Over 300 independent
        // trials, failing a >=60% threshold by chance alone has probability
        // on the order of 1e-15 (roughly 9 standard deviations below the
        // expected mean) - this is not a "sometimes flaky" test.
        using var db = new TestDb();
        var (galleryId, ownerId) = SeedGallery(db);
        var tag = new Tag { Id = Guid.NewGuid(), Name = "Popular" };
        var heavy = SeedPhoto(db, galleryId, "Heavy", tag);
        SeedPhoto(db, galleryId, "Plain");
        await db.Context.SaveChangesAsync();

        for (var i = 0; i < 25; i++)
        {
            db.Context.PhotoViews.Add(new PhotoView
            {
                Id = Guid.NewGuid(),
                UserId = ownerId,
                PhotoId = heavy.Id,
                ViewedAtUtc = DateTime.UtcNow,
            });
        }
        await db.Context.SaveChangesAsync();

        var service = new DiscoveryFeedService(db.Context, new GalleryAccessService(db.Context));

        const int trials = 300;
        var heavyWins = 0;
        for (var i = 0; i < trials; i++)
        {
            var result = await service.GetFeedAsync(ownerId, 1, new HashSet<Guid>());
            if (result.Single().Id == heavy.Id) heavyWins++;
        }

        Assert.True(heavyWins >= trials * 0.6,
            $"Expected the heavily-viewed photo to win clearly more than half of {trials} trials " +
            $"(expected ~80% given the weight formula); it actually won {heavyWins}.");
    }
}
