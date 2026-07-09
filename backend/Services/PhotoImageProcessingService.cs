using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using PhotoLibApi.Models;

namespace PhotoLibApi.Services
{
    /// <summary>
    /// Thrown by <see cref="PhotoImageProcessingService.SaveStreamToFileAsync"/> when the
    /// source stream exceeds the configured byte limit. The partially written destination
    /// file has already been deleted by the time this is thrown.
    /// </summary>
    public class StreamTooLargeException : Exception
    {
    }

    /// <summary>
    /// Handles the image file I/O for photos: saving downloaded/uploaded bytes to disk and
    /// generating a thumbnail plus EXIF metadata from a saved original. Kept separate from
    /// <see cref="Controllers.PhotoController"/> so the controller's job stays request
    /// handling and validation rather than image processing.
    /// </summary>
    public class PhotoImageProcessingService
    {
        private const int CopyBufferSize = 81920;

        private readonly PhotoFilePathHelper _filePathHelper;

        /// <summary>
        /// Creates the service with the file path helper used to locate originals/thumbnails.
        /// </summary>
        public PhotoImageProcessingService(PhotoFilePathHelper filePathHelper)
        {
            _filePathHelper = filePathHelper;
        }

        /// <summary>
        /// Copies <paramref name="source"/> to a new file at <paramref name="destinationPath"/>
        /// in fixed-size chunks, aborting (and deleting the partial file) as soon as more than
        /// <paramref name="maxBytes"/> bytes have been read. Used instead of buffering an entire
        /// download into memory (e.g. via <c>ReadAsByteArrayAsync</c>) before checking its size -
        /// that would let a large or malicious response balloon memory before ever being
        /// rejected, especially when the source doesn't send a <c>Content-Length</c> header.
        /// </summary>
        /// <param name="source">Stream to read from; not disposed by this method.</param>
        /// <param name="destinationPath">Path of the file to create and write to.</param>
        /// <param name="maxBytes">Maximum number of bytes allowed before aborting.</param>
        /// <returns>The number of bytes written.</returns>
        /// <exception cref="StreamTooLargeException">
        /// <paramref name="source"/> exceeded <paramref name="maxBytes"/>; the partial file has
        /// already been deleted.
        /// </exception>
        public async Task<long> SaveStreamToFileAsync(Stream source, string destinationPath, long maxBytes)
        {
            var buffer = new byte[CopyBufferSize];
            long totalBytes = 0;

            await using (var destination = File.Create(destinationPath))
            {
                int bytesRead;
                while ((bytesRead = await source.ReadAsync(buffer)) > 0)
                {
                    totalBytes += bytesRead;

                    if (totalBytes > maxBytes)
                    {
                        destination.Close();
                        File.Delete(destinationPath);
                        throw new StreamTooLargeException();
                    }

                    await destination.WriteAsync(buffer.AsMemory(0, bytesRead));
                }
            }

            return totalBytes;
        }

        /// <summary>
        /// Generates a thumbnail and reads EXIF metadata for an original file already saved on
        /// disk, updating the given photo's flags/fields in place. Does not save changes to the
        /// database - callers are expected to do that.
        /// </summary>
        /// <param name="photo">Photo entity to update with thumbnail/EXIF results.</param>
        /// <param name="originalFilePath">Path of the original file already on disk.</param>
        public void GenerateThumbnailAndExif(Photo photo, string originalFilePath)
        {
            Directory.CreateDirectory(_filePathHelper.GetThumbnailsDirectory());
            var thumbnailPath = _filePathHelper.GetThumbnailFilePath(photo.Id);

            using (var image = Image.Load(originalFilePath))
            {
                image.Mutate(x => x.Resize(new ResizeOptions
                {
                    Size = new Size(300, 300),
                    Mode = ResizeMode.Max
                }));

                image.Save(thumbnailPath);
            }

            photo.HasThumbnail = true;

            var exif = ExifReader.Read(originalFilePath);
            photo.ExifJson = exif.Json;
            photo.Latitude = exif.Latitude;
            photo.Longitude = exif.Longitude;
        }
    }
}
