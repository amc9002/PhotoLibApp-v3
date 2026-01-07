using System.ComponentModel.DataAnnotations;

namespace PhotoLibApi.Models
{
    /// <summary>
    /// Request model used to update photo metadata.
    /// </summary>
    public class UpdatePhotoRequest
    {
        /// <summary>
        /// Updated photo title.
        /// </summary>
        [Required]
        public string Title { get; set; } = "";

        /// <summary>
        /// Updated photo description.
        /// </summary>
        public string? Description { get; set; }
    }
}
