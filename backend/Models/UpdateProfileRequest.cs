using System.ComponentModel.DataAnnotations;

namespace PhotoLibApi.Models
{
    /// <summary>
    /// Request model for updating the signed-in user's own profile.
    /// </summary>
    public class UpdateProfileRequest
    {
        /// <summary>
        /// Display name. Required, unlike <see cref="Bio"/>.
        /// </summary>
        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = "";

        /// <summary>
        /// Optional free-text personal info.
        /// </summary>
        [MaxLength(2000)]
        public string? Bio { get; set; }
    }
}
