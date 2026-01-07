using System;
using System.IO;

namespace PhotoLibApi.Services
{
    /// <summary>
    /// Provides file system paths for photo originals and thumbnails.
    /// Centralizes file path logic to keep controllers clean.
    /// </summary>
    public class PhotoFilePathHelper
    {
        private readonly string _photosRootPath;

        public PhotoFilePathHelper(string photosRootPath)
        {
            _photosRootPath = photosRootPath;
        }

        /// <summary>
        /// Gets the directory path for original photos.
        /// </summary>
        public string GetOriginalsDirectory()
        {
            return Path.Combine(_photosRootPath, "originals");
        }

        /// <summary>
        /// Gets the directory path for thumbnails. 
        /// </summary>
        public string GetThumbnailsDirectory()
        {
            return Path.Combine(_photosRootPath, "thumbnails");
        }

        /// <summary>
        /// Gets the file path for the original of the specified photo ID.
        /// </summary>
        public string GetOriginalFilePath(Guid photoId)
        {
            return Path.Combine(GetOriginalsDirectory(), $"{photoId}.jpg");
        }

        /// <summary>
        /// Gets the file path for the thumbnail of the specified photo ID.
        /// </summary>
        public string GetThumbnailFilePath(Guid photoId)
        {
            return Path.Combine(GetThumbnailsDirectory(), $"{photoId}.jpg");
        }

        /// <summary>
        /// Checks if the original file exists for the specified photo ID.
        /// </summary>
        public bool OriginalExists(Guid photoId)
        {
            var path = GetOriginalFilePath(photoId);
            return File.Exists(path);
        }

        /// <summary>
        /// Checks if the thumbnail file exists for the specified photo ID. 
        /// </summary>
        public bool ThumbnailExists(Guid photoId)
        {
            var path = GetThumbnailFilePath(photoId);
            return File.Exists(path);
        }

        /// <summary>
        /// Deletes the original file for the specified photo ID.   
        /// </summary>
        public void DeleteOriginal(Guid photoId)
        {
            var path = GetOriginalFilePath(photoId);

            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        /// <summary>
        /// Deletes the thumbnail file for the specified photo ID.  
        /// </summary>
        public void DeleteThumbnail(Guid photoId)
        {
            var path = GetThumbnailFilePath(photoId);

            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
