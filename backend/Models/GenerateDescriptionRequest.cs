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

        /// <summary>
        /// Optional free-form instructions from the user (e.g. extra known
        /// facts, things to emphasize or avoid). Can also override
        /// <see cref="Language"/> by explicitly asking for a different one.
        /// </summary>
        public string? AdditionalInstructions { get; set; }

        /// <summary>
        /// UI language code (e.g. "be", "en") the generated text should be
        /// written in - defaults to the app's current language so a draft
        /// doesn't come back in a different language than the rest of the UI.
        /// </summary>
        public string? Language { get; set; }
    }
}
