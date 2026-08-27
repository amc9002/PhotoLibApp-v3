using System;
using System.IO;

namespace PhotoLibApi.Services
{
    /// <summary>
    /// Provides the file system path for a user's avatar image. One file
    /// per user (already resized to a fixed square by
    /// <see cref="AvatarImageProcessingService"/>) - unlike photos, there's
    /// no separate original/thumbnail pair.
    /// </summary>
    public class AvatarFilePathHelper
    {
        private readonly string _avatarsRootPath;

        /// <param name="avatarsRootPath">Directory where avatar images are stored.</param>
        public AvatarFilePathHelper(string avatarsRootPath)
        {
            _avatarsRootPath = avatarsRootPath;
        }

        /// <summary>
        /// Gets the directory path where avatar images are stored.
        /// </summary>
        public string GetAvatarsDirectory() => _avatarsRootPath;

        /// <summary>
        /// Gets the file path for the avatar of the specified user ID.
        /// </summary>
        public string GetAvatarFilePath(Guid userId) =>
            Path.Combine(_avatarsRootPath, $"{userId}.jpg");

        /// <summary>
        /// Checks if an avatar file exists for the specified user ID.
        /// </summary>
        public bool AvatarExists(Guid userId) =>
            File.Exists(GetAvatarFilePath(userId));

        /// <summary>
        /// Deletes the avatar file for the specified user ID, if any.
        /// </summary>
        public void DeleteAvatar(Guid userId)
        {
            var path = GetAvatarFilePath(userId);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
