using System;
using System.ComponentModel.DataAnnotations;

namespace PhotoLibApi.Models
{
    /// <summary>
    /// Records that a user opened a photo full-screen in the viewer. Used
    /// only in aggregate (grouped by tag, per user) to bias the Discovery
    /// feed toward themes the user actually engages with - never exposed
    /// or queried per-row, so there's no back-navigation to <see cref="User"/>
    /// or <see cref="Photo"/>, same as <see cref="Tag"/>.
    /// </summary>
    public class PhotoView
    {
        /// <summary>
        /// Unique identifier of the view event.
        /// </summary>
        [Key]
        public Guid Id { get; set; }

        /// <summary>
        /// Id of the user who viewed the photo.
        /// </summary>
        public Guid UserId { get; set; }

        /// <summary>
        /// Id of the photo that was viewed.
        /// </summary>
        public Guid PhotoId { get; set; }

        /// <summary>
        /// When the photo was opened, in UTC.
        /// </summary>
        public DateTime ViewedAtUtc { get; set; }
    }
}
