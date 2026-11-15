using System.Linq;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PhotoLibApi.Data;

namespace backend.Tests;

/// <summary>
/// Boots the real backend (same <c>Program.cs</c> pipeline, controllers,
/// DI graph) against a private in-memory SQLite database and a temporary
/// photo/avatar storage folder, instead of the real
/// <c>photolib.db</c>/<c>uploads</c> used by actual runs. Each instance is
/// an isolated database - tests must not share one factory across test
/// classes that could race on the same rows.
/// </summary>
public class TestingWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly string _storageRoot = Path.Combine(
        Path.GetTempPath(), "photolib-tests", Guid.NewGuid().ToString("N"));

    public string PhotosPath => Path.Combine(_storageRoot, "photos");
    public string AvatarsPath => Path.Combine(_storageRoot, "avatars");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // A single open connection is required for SQLite's ":memory:" mode -
        // closing it (e.g. between requests) would discard the whole database.
        _connection.Open();

        builder.UseSetting("Storage:PhotosPath", PhotosPath);
        builder.UseSetting("Storage:AvatarsPath", AvatarsPath);
        builder.UseSetting("Auth:AdminKey", "test-admin-key");

        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<PhotoDbContext>));
            if (descriptor != null)
                services.Remove(descriptor);

            services.AddDbContext<PhotoDbContext>(options => options.UseSqlite(_connection));
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        _connection.Dispose();

        if (Directory.Exists(_storageRoot))
            Directory.Delete(_storageRoot, recursive: true);
    }
}
