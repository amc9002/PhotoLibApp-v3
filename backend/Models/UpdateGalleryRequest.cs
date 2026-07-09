using System.ComponentModel.DataAnnotations;

namespace PhotoLibApi.Models
{
    /// <summary>
    /// Request model used to update gallery metadata.
    /// </summary>
    public class UpdateGalleryRequest
    {
        /// <summary>
        /// Updated gallery title.
        /// </summary>
        [Required]
        public string Title { get; set; } = "";

        /// <summary>
        /// Updated gallery description.
        /// </summary>
        public string? Description { get; set; }
    }
}
