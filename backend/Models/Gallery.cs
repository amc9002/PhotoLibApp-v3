using System;
using System.ComponentModel.DataAnnotations;

namespace PhotoLibApi.Models
{
    /// <summary>
    /// Represents a user-created gallery that groups photos together.
    /// </summary>
    public class Gallery
    {
        /// <summary>
        /// Unique identifier of the gallery.
        /// </summary>
        [Key]
        public Guid Id { get; set; }

        /// <summary>
        /// Human-readable name of the gallery.
        /// /// For example: "Vacation 2025" or "Family".
        /// </summary>
        [Required]
        public string Title { get; set; } = "";

        /// <summary>
        /// Identifier of the user who owns this gallery.
        /// /// Used to ensure each user sees only their own content.
        /// </summary>
        public string? OwnerId { get; set; }

        /// <summary>
        /// Timestamp of when the gallery was created (UTC).
        /// </summary>
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Marks the gallery as deleted without removing it from the database.
        /// </summary>
        public bool IsDeleted { get; set; } = false;

        /// <summary>
        /// Timestamp of the latest update (UTC).
        /// Used to detect changes during synchronization.
        /// </summary>
        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    }
}

