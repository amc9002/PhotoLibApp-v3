using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace PhotoLibApi.Models
{
    /// <summary>
    /// Request model used to persist a new drag-and-drop display order for
    /// the photos within a gallery.
    /// </summary>
    public class ReorderPhotosRequest
    {
        /// <summary>
        /// Gallery the photos belong to. Photo ids outside this gallery are ignored.
        /// </summary>
        [Required]
        public Guid GalleryId { get; set; }

        /// <summary>
        /// Photo identifiers in the desired display order.
        /// </summary>
        [Required]
        public List<Guid> PhotoIds { get; set; } = new();
    }
}
