using System;
using System.ComponentModel.DataAnnotations;

namespace PhotoLibApi.Models
{
    /// <summary>
    /// A user-defined label that can be attached to photos and galleries.
    /// </summary>
    public class Tag
    {
        /// <summary>
        /// Unique identifier of the tag.
        /// </summary>
        [Key]
        public Guid Id { get; set; }

        /// <summary>
        /// Display name of the tag (matched case-insensitively when reused).
        /// </summary>
        [Required]
        public string Name { get; set; } = "";
    }
}
