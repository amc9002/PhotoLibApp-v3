using Microsoft.EntityFrameworkCore;
using PhotoLibApi.Models;

namespace PhotoLibApi.Data
{
    /// <summary>
    /// Database context for the PhotoLib API.
    /// Holds the tables for galleries and photos.
    /// </summary>
    public class PhotoDbContext : DbContext
    {
        public PhotoDbContext(DbContextOptions<PhotoDbContext> options) : base(options) { }

        public DbSet<Photo> Photos => Set<Photo>();
        public DbSet<Gallery> Galleries => Set<Gallery>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Photo>().HasIndex(p => p.Id);
            modelBuilder.Entity<Photo>().HasIndex(p => p.GalleryId);
            modelBuilder.Entity<Photo>().HasIndex(p => new { p.Id, p.ClientTempId });
            modelBuilder.Entity<Gallery>().HasIndex(g => g.OwnerId);
        }
    }
}
