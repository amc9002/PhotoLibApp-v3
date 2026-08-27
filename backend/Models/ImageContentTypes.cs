namespace PhotoLibApi.Models
{
    /// <summary>
    /// Well-known image content-type strings shared by the file-serving
    /// endpoints (<see cref="Controllers.PhotoController"/>,
    /// <see cref="Controllers.AvatarController"/>) - every image this API
    /// serves is re-encoded to JPEG server-side, so this is the one value
    /// they all need.
    /// </summary>
    public static class ImageContentTypes
    {
        /// <summary>MIME type for the JPEG images this API stores and serves.</summary>
        public const string Jpeg = "image/jpeg";
    }
}
