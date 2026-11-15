using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PhotoLibApi.Data;
using PhotoLibApi.Models;
using PhotoLibApi.Services;

namespace backend.Tests;

/// <summary>
/// Direct, HTTP-free tests of <see cref="TagResolver"/> - normalization,
/// case-insensitive reuse, the "replace, don't merge" contract of
/// <c>ApplyAsync</c>, and a deliberately simulated create-race that
/// documents a real architectural gap (no unique constraint on
/// <c>Tag.Name</c>) without fixing it.
/// </summary>
public class TagResolverTests
{
    [Fact]
    public async Task ResolveAsync_NewTagName_IsTrimmedBeforeBeingStored()
    {
        using var testDb = new TestDb();
        var resolver = new TagResolver(testDb.Context);

        var result = await resolver.ResolveAsync(new[] { "  Travel  " });

        Assert.Equal("Travel", result.Single().Name);
    }

    [Fact]
    public async Task ResolveAsync_EmptyAndWhitespaceOnlyNames_AreFilteredOutEntirely()
    {
        using var testDb = new TestDb();
        var resolver = new TagResolver(testDb.Context);

        var result = await resolver.ResolveAsync(new[] { "", "   ", "\t" });

        Assert.Empty(result);
        Assert.Empty(await testDb.Context.Tags.ToListAsync());
    }

    [Fact]
    public async Task ResolveAsync_DuplicateNamesWithinOneCall_CaseInsensitive_CollapseToOneTag()
    {
        using var testDb = new TestDb();
        var resolver = new TagResolver(testDb.Context);

        var result = await resolver.ResolveAsync(new[] { "Travel", "TRAVEL", "travel " });

        Assert.Single(result);
        Assert.Equal("Travel", result[0].Name); // first-seen casing wins
    }

    [Fact]
    public async Task ResolveAsync_ExistingTagWithDifferentCase_IsReused_NotDuplicated()
    {
        using var testDb = new TestDb();
        var resolver = new TagResolver(testDb.Context);
        var first = await resolver.ResolveAsync(new[] { "Travel" });
        await testDb.Context.SaveChangesAsync();

        var second = await resolver.ResolveAsync(new[] { "TRAVEL" });

        Assert.Equal(first.Single().Id, second.Single().Id);
        Assert.Single(await testDb.Context.Tags.ToListAsync());
    }

    [Fact]
    public async Task ResolveAsync_CyrillicCaseFolding_ReusesTheSameTag()
    {
        // The comment in TagResolver explicitly calls out that in-memory
        // OrdinalIgnoreCase folds non-ASCII (e.g. Cyrillic) case correctly,
        // unlike SQLite's own LOWER(). This confirms that claim end to end.
        using var testDb = new TestDb();
        var resolver = new TagResolver(testDb.Context);
        var first = await resolver.ResolveAsync(new[] { "Падарожжа" });
        await testDb.Context.SaveChangesAsync();

        var second = await resolver.ResolveAsync(new[] { "ПАДАРОЖЖА" });

        Assert.Equal(first.Single().Id, second.Single().Id);
        Assert.Single(await testDb.Context.Tags.ToListAsync());
    }

    [Fact]
    public async Task ResolveAsync_ReturnsTagsInTheNormalizedInputOrder()
    {
        // Asserted directly on ResolveAsync's own return value - not via a
        // later GetById read-back, whose order is independently determined
        // by EF Core's join materialization (see the Gallery/Photo CRUD
        // stage's findings) and is deliberately out of scope here.
        using var testDb = new TestDb();
        var resolver = new TagResolver(testDb.Context);

        var result = await resolver.ResolveAsync(new[] { "Zebra", "Apple", "Mango" });

        Assert.Equal(new[] { "Zebra", "Apple", "Mango" }, result.Select(t => t.Name));
    }

    [Fact]
    public async Task ApplyAsync_ReplacingTheTagSet_DetachesTheOldTagFromTheEntity_ButTheTagRowStillExists()
    {
        using var testDb = new TestDb();
        var gallery = new Gallery { Id = Guid.NewGuid(), Title = "Gallery", OwnerId = Guid.NewGuid() };
        testDb.Context.Galleries.Add(gallery);
        await testDb.Context.SaveChangesAsync();

        var resolver = new TagResolver(testDb.Context);
        await resolver.ApplyAsync(gallery, new[] { "OldTag" });
        await testDb.Context.SaveChangesAsync();
        var oldTagId = gallery.Tags.Single().Id;

        await resolver.ApplyAsync(gallery, new[] { "NewTag" });
        await testDb.Context.SaveChangesAsync();

        // The association is gone from the entity...
        Assert.DoesNotContain(gallery.Tags, t => t.Id == oldTagId);
        Assert.Equal("NewTag", gallery.Tags.Single().Name);

        // ...but the old Tag row itself is never deleted - Tag has no
        // cascade-delete-when-unreferenced logic at all.
        Assert.True(await testDb.Context.Tags.AnyAsync(t => t.Id == oldTagId));
    }

    [Fact]
    public async Task ResolveAsync_SameTagNameAcrossGalleryAndPhoto_ReusesTheExactSameTagId()
    {
        using var testDb = new TestDb();
        var gallery = new Gallery { Id = Guid.NewGuid(), Title = "Gallery", OwnerId = Guid.NewGuid() };
        var photo = new Photo { Id = Guid.NewGuid(), GalleryId = gallery.Id, Title = "Photo" };
        testDb.Context.Galleries.Add(gallery);
        testDb.Context.Photos.Add(photo);
        await testDb.Context.SaveChangesAsync();

        var resolver = new TagResolver(testDb.Context);
        await resolver.ApplyAsync(gallery, new[] { "Shared" });
        await testDb.Context.SaveChangesAsync();
        await resolver.ApplyAsync(photo, new[] { "Shared" });
        await testDb.Context.SaveChangesAsync();

        Assert.Equal(gallery.Tags.Single().Id, photo.Tags.Single().Id);
        Assert.Single(await testDb.Context.Tags.Where(t => t.Name == "Shared").ToListAsync());
    }

    [Fact]
    public async Task ResolveAsync_SimulatedConcurrentCreate_CanProduceTwoTagRowsWithTheSameName()
    {
        // ARCHITECTURAL FINDING, documented and NOT fixed here per explicit
        // instruction: PhotoDbContext has no unique index/constraint on
        // Tag.Name (unlike User.Email, which has one and is handled via
        // catch(DbUpdateException) in AuthController). Two independent
        // requests that both check "does this name exist?" before either
        // commits can both decide "no" and both insert a row.
        //
        // IMPORTANT SCOPE NOTE: this test MODELS that gap deterministically
        // by using two separate PhotoDbContext instances (standing in for
        // two concurrent HTTP requests' own DI-scoped contexts) against one
        // shared SQLite connection, with the two ResolveAsync calls run
        // sequentially in a controlled order rather than on real background
        // threads. It proves the application-level "check then act" is not
        // atomic. It is NOT a general proof of how real concurrent SQLite
        // access behaves under true multi-threaded load (connection
        // locking, busy-retry, WAL mode, etc.) - that is explicitly out of
        // scope for this test.
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<PhotoDbContext>().UseSqlite(connection).Options;

        using var dbA = new PhotoDbContext(options);
        dbA.Database.EnsureCreated();
        using var dbB = new PhotoDbContext(options);

        var resolverA = new TagResolver(dbA);
        var resolverB = new TagResolver(dbB);

        // Both "requests" resolve the same brand-new name before either has
        // saved - this is the race window the production code doesn't close.
        var fromA = await resolverA.ResolveAsync(new[] { "RaceTag" });
        var fromB = await resolverB.ResolveAsync(new[] { "RaceTag" });

        await dbA.SaveChangesAsync();
        await dbB.SaveChangesAsync(); // succeeds - nothing stops a duplicate name

        var allRaceTags = await dbA.Tags.Where(t => t.Name == "RaceTag").ToListAsync();
        Assert.Equal(2, allRaceTags.Count);
        Assert.NotEqual(fromA.Single().Id, fromB.Single().Id);
    }
}
