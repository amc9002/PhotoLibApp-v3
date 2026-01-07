using System;
using System.ComponentModel.DataAnnotations;

namespace PhotoLibApi.Models
{
    /// <summary>
    /// Represents metadata for a single photo stored in the system.
    /// The actual image file is stored separately (e.g., in Blob storage),
    /// while this model holds descriptive information.
    /// </summary>
    /// 
    /// <remarks>
    /// Storage note:
    /// Currently, image files are stored on the local file system
    /// and accessed via dedicated API endpoints.
    /// In a future cloud-based architecture, this model may be extended
    /// to reference external storage (e.g., Blob/S3) using storage keys
    /// instead of direct URLs.
    /// </remarks>
    public class Photo
    {
        /// <summary>
        /// Permanent unique identifier generated on the server.
        /// This is the main reference used by the API and the database.
        /// </summary>
        [Key]
        public Guid Id { get; set; }

        /// <summary>
        /// Temporary identifier created on the client before the photo
        /// is synchronized with the server. Used to map local entries
        /// to server-side records during sync.
        /// </summary>
        public string? ClientTempId { get; set; }

        /// <summary>
        /// Human-readable title of the photo.
        /// </summary>
        [Required]
        public string Title { get; set; } = "";

        /// <summary>
        /// Optional text description of the photo.
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Identifier of the gallery to which this photo belongs.
        /// </summary>
        [Required]
        public Guid GalleryId { get; set; }

        /// <summary>
        /// Optional metadata in JSON form (e.g., EXIF information).
        /// </summary>
        public string? ExifJson { get; set; }

        /// <summary>
        /// Marks the photo as deleted without removing it from the database.
        /// Useful for synchronization and recovery.
        /// </summary>
        public bool IsDeleted { get; set; } = false;

        /// <summary>
        /// Timestamp of when this record was created (in UTC).
        /// </summary>
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Timestamp of the latest update (in UTC).
        /// Used to detect changes during synchronization.
        /// </summary>
        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Concurrency token used by Entity Framework to detect conflicts
        /// when multiple updates happen at the same time.
        /// </summary>
        [Timestamp]
        public byte[]? RowVersion { get; set; }

        /// <summary>
        /// Indicates whether the original image file exists on the server.
        /// </summary>
        public bool HasOriginal { get; set; } = false;

        /// <summary>
        /// Indicates whether a thumbnail image exists for this photo.
        /// </summary>
        public bool HasThumbnail { get; set; } = false;

    }
}
