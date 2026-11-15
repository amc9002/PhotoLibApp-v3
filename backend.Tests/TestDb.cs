using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PhotoLibApi.Data;

namespace backend.Tests;

/// <summary>
/// A fresh, isolated <see cref="PhotoDbContext"/> backed by an in-memory
/// SQLite database, for tests that exercise a single service directly
/// (no HTTP, no <see cref="TestingWebApplicationFactory"/>). The schema is
/// created straight from the current EF Core model via
/// <c>EnsureCreated</c>, not through the Migrations history - this is
/// faster and, since these tests only care about query/ownership
/// behavior rather than the migration history itself, an acceptable
/// trade-off for this narrower use case.
/// </summary>
public sealed class TestDb : IDisposable
{
    private readonly SqliteConnection _connection;

    public PhotoDbContext Context { get; }

    public TestDb()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<PhotoDbContext>()
            .UseSqlite(_connection)
            .Options;

        Context = new PhotoDbContext(options);
        Context.Database.EnsureCreated();
    }

    public void Dispose()
    {
        Context.Dispose();
        _connection.Dispose();
    }
}
