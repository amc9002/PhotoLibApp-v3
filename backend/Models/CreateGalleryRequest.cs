using System.ComponentModel.DataAnnotations;

namespace PhotoLibApi.Models
{
    /// <summary>
    /// Request model used to create a new gallery.
    /// </summary>
    public class CreateGalleryRequest
    {
        /// <summary>
        /// Human-readable name of the gallery.
        /// </summary>
        [Required]
        public string Title { get; set; } = "";

        /// <summary>
        /// Optional client-generated id for idempotent offline-sync replay.
        /// </summary>
        public string? ClientTempId { get; set; }
    }
}

