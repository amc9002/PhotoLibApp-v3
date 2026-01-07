using System;
using System.ComponentModel.DataAnnotations;

namespace PhotoLibApi.Models
{
    /// <summary>
    /// Request model used to create a photo metadata record.
    /// </summary>
    public class CreatePhotoRequest
    {
        /// <summary>
        /// Identifier of the gallery this photo belongs to.
        /// </summary>
        [Required]
        public Guid GalleryId { get; set; }

        /// <summary>
        /// Optional human-readable photo title.
        /// </summary>
        [Required]
        public string Title { get; set; } = "";

        /// <summary>
        /// Optional short description of the photo.
        /// </summary>
        public string? Description { get; set; }
    }
}
