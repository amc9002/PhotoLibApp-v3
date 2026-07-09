namespace PhotoLibApi.Models
{
    /// <summary>
    /// Request model used to replace the full tag set of a photo or gallery.
    /// </summary>
    public class SetTagsRequest
    {
        /// <summary>
        /// The complete list of tag names that should be attached.
        /// Existing tags not present here are detached; unknown names are created.
        /// </summary>
        public List<string> TagNames { get; set; } = new();
    }
}
