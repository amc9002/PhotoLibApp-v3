using System;
using System.ComponentModel.DataAnnotations;

namespace PhotoLibApi.Models
{
    /// <summary>
    /// A registered account. Owns galleries (and transitively their photos)
    /// via <see cref="Gallery.OwnerId"/>.
    /// </summary>
    public class User
    {
        /// <summary>
        /// Unique identifier of the user.
        /// </summary>
        [Key]
        public Guid Id { get; set; }

        /// <summary>
        /// Login email address, stored lowercased so lookups and the unique
        /// index are case-insensitive without relying on database collation.
        /// </summary>
        [Required]
        public string Email { get; set; } = "";

        /// <summary>
        /// Salted hash of the account password, produced by
        /// <see cref="Microsoft.AspNetCore.Identity.PasswordHasher{TUser}"/>.
        /// Never the plaintext password.
        /// </summary>
        [Required]
        public string PasswordHash { get; set; } = "";

        /// <summary>
        /// Display name, shown in the toolbar and (later) on this user's
        /// public profile. Required, unlike <see cref="Bio"/>.
        /// </summary>
        [Required]
        public string Name { get; set; } = "";

        /// <summary>
        /// Optional free-text personal info the user can add about
        /// themselves (e.g. shown on a future public profile page).
        /// </summary>
        public string? Bio { get; set; }

        /// <summary>
        /// Whether an avatar image exists for this user on disk. Mirrors
        /// <see cref="Photo.HasThumbnail"/>'s pattern - the file itself is
        /// stored separately and served via a dedicated endpoint.
        /// </summary>
        public bool HasAvatar { get; set; } = false;

        /// <summary>
        /// Timestamp of when the account was created (UTC).
        /// </summary>
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    }
}
