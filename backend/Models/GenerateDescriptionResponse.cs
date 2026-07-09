namespace PhotoLibApi.Models
{
    /// <summary>
    /// AI-drafted title, description and tags for a photo, returned for
    /// review - not yet persisted.
    /// </summary>
    public class GenerateDescriptionResponse
    {
        /// <summary>
        /// Suggested title.
        /// </summary>
        public string Title { get; set; } = "";

        /// <summary>
        /// Suggested description, written in the requested style and length.
        /// </summary>
        public string Description { get; set; } = "";

        /// <summary>
        /// Suggested tag names.
        /// </summary>
        public List<string> SuggestedTags { get; set; } = new();
    }
}
