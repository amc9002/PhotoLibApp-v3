using System.Text.Json;
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;

namespace PhotoLibApi.Services
{
    /// <summary>
    /// Result of reading EXIF metadata from an image file.
    /// </summary>
    public class ExifReadResult
    {
        /// <summary>JSON: a list of {name, tags:[{name, description}]} groups.</summary>
        public string? Json { get; set; }

        /// <summary>GPS latitude in decimal degrees, if the image carried a location.</summary>
        public double? Latitude { get; set; }

        /// <summary>GPS longitude in decimal degrees, if the image carried a location.</summary>
        public double? Longitude { get; set; }
    }

    /// <summary>
    /// Reads EXIF metadata (and GPS coordinates, if present) from an image file on disk.
    /// </summary>
    public static class ExifReader
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };

        /// <summary>
        /// Reads EXIF metadata from the image at <paramref name="filePath"/>.
        /// Returns an empty result (never throws) if the file has no readable EXIF.
        /// </summary>
        public static ExifReadResult Read(string filePath)
        {
            try
            {
                using var stream = File.OpenRead(filePath);
                var directories = ImageMetadataReader.ReadMetadata(stream);

                var summary = directories.Select(d => new
                {
                    d.Name,
                    Tags = d.Tags.Select(t => new { t.Name, Description = t.Description ?? "" }).ToList(),
                }).ToList();

                var result = new ExifReadResult
                {
                    Json = JsonSerializer.Serialize(summary, JsonOptions),
                };

                var gps = directories.OfType<GpsDirectory>().FirstOrDefault();
                var location = gps?.GetGeoLocation();
                if (location != null && !location.IsZero)
                {
                    result.Latitude = location.Latitude;
                    result.Longitude = location.Longitude;
                }

                return result;
            }
            catch
            {
                // Not every image format carries EXIF (e.g. PNG) — safe to skip.
                return new ExifReadResult();
            }
        }
    }
}
