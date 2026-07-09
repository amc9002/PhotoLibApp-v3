using System.ComponentModel.DataAnnotations;

namespace PhotoLibApi.Models
{
    /// <summary>
    /// Request model used to ask the AI to draft a title, description and
    /// tags for a photo. Nothing is saved by this call - the draft is
    /// returned for the caller to review before submitting a normal update.
    /// </summary>
    public class GenerateDescriptionRequest
    {
        /// <summary>
        /// Writing style for the generated description:
        /// "artistic", "informative", "scientific" or "journalistic".
        /// </summary>
        [Required]
        public string Style { get; set; } = "informative";

        /// <summary>
        /// Desired length of the generated description: "short", "medium" or "long".
        /// </summary>
        [Required]
        public string Length { get; set; } = "medium";
    }
}
