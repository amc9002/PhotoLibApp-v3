using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace PhotoLibApi.Models
{
    /// <summary>
    /// Request model used to persist a new drag-and-drop display order for
    /// the user's galleries.
    /// </summary>
    public class ReorderGalleriesRequest
    {
        /// <summary>
        /// Gallery identifiers in the desired display order.
        /// </summary>
        [Required]
        public List<Guid> GalleryIds { get; set; } = new();
    }
}
