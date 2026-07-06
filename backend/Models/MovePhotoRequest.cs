using System;
using System.ComponentModel.DataAnnotations;

namespace PhotoLibApi.Models
{
    /// <summary>
    /// Request model used to move or copy a photo into another gallery.
    /// </summary>
    public class MovePhotoRequest
    {
        /// <summary>
        /// Identifier of the destination gallery.
        /// </summary>
        [Required]
        public Guid GalleryId { get; set; }
    }
}
