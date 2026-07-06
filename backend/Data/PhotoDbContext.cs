using Microsoft.EntityFrameworkCore;
using PhotoLibApi.Models;

namespace PhotoLibApi.Data
{
    public class PhotoDbContext : DbContext
    {
        public PhotoDbContext(DbContextOptions<PhotoDbContext> options) : base(options) { }

        public DbSet<Photo> Photos => Set<Photo>();
        public DbSet<Gallery> Galleries => Set<Gallery>();
        public DbSet<Tag> Tags => Set<Tag>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Photo>().HasIndex(p => p.Id);
            modelBuilder.Entity<Photo>().HasIndex(p => p.GalleryId);
            modelBuilder.Entity<Photo>().HasIndex(p => new { p.Id, p.ClientTempId });
            modelBuilder.Entity<Gallery>().HasIndex(g => g.OwnerId);

            // Unidirectional many-to-many: Tag has no navigation back to Photo/Gallery,
            // which keeps tag reads free of serialization cycles.
            modelBuilder.Entity<Photo>()
                .HasMany(p => p.Tags)
                .WithMany()
                .UsingEntity(j => j.ToTable("PhotoTags"));

            modelBuilder.Entity<Gallery>()
                .HasMany(g => g.Tags)
                .WithMany()
                .UsingEntity(j => j.ToTable("GalleryTags"));
        }
    }
}
