using System;
using System.ComponentModel.DataAnnotations;

namespace PhotoLibApi.Models
{
    /// <summary>
    /// Represents a user-created gallery that groups photos together.
    /// </summary>
    public class Gallery : ITaggable
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
        /// Optional text description of the gallery.
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Identifier of the <see cref="User"/> who owns this gallery.
        /// Used to ensure each user sees only their own content. Nullable
        /// only to represent legacy pre-authentication data; every gallery
        /// created after authentication was added always has an owner.
        /// </summary>
        public Guid? OwnerId { get; set; }

        /// <summary>
        /// Timestamp of when the gallery was created (UTC).
        /// </summary>
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Client-generated identifier used to make offline-queued creates
        /// idempotent: replaying the same create twice (e.g. after a sync
        /// retry) returns the existing row instead of inserting a duplicate.
        /// </summary>
        public string? ClientTempId { get; set; }

        /// <summary>
        /// Manual display order among the user's galleries (ascending).
        /// New galleries are appended after the current highest value;
        /// drag-and-drop reordering rewrites this for the affected galleries.
        /// </summary>
        public int SortOrder { get; set; }

        /// <summary>
        /// Marks the gallery as deleted without removing it from the database.
        /// </summary>
        public bool IsDeleted { get; set; } = false;

        /// <summary>
        /// Timestamp of the latest update (UTC).
        /// Used to detect changes during synchronization.
        /// </summary>
        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Tags attached to this gallery.
        /// </summary>
        public ICollection<Tag> Tags { get; set; } = new List<Tag>();
    }
}

