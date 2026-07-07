using System;
using System.ComponentModel.DataAnnotations;

namespace PhotoLibApi.Models
{
    /// <summary>
    /// Request model used to create a photo by downloading an image from a URL.
    /// </summary>
    public class CreatePhotoFromUrlRequest
    {
        /// <summary>
        /// Identifier of the gallery this photo belongs to.
        /// </summary>
        [Required]
        public Guid GalleryId { get; set; }

        /// <summary>
        /// Absolute http(s) URL of the image to download.
        /// </summary>
        [Required]
        [Url]
        public string ImageUrl { get; set; } = "";

        /// <summary>
        /// Optional title; defaults to the file name from the URL.
        /// </summary>
        public string? Title { get; set; }
    }
}
