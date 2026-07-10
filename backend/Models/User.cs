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
        /// Timestamp of when the account was created (UTC).
        /// </summary>
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    }
}
