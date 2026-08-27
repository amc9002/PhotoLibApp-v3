using System;
using System.IO;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace PhotoLibApi.Services
{
    /// <summary>
    /// Resizes an uploaded avatar image to a fixed square and saves it as
    /// the user's only avatar file, replacing any previous one. Kept
    /// separate from <see cref="PhotoImageProcessingService"/> since
    /// avatars have no original/thumbnail distinction - just one small
    /// square image, the same way most apps treat profile pictures.
    /// </summary>
    public class AvatarImageProcessingService
    {
        private const int AvatarSize = 256;

        private readonly AvatarFilePathHelper _filePathHelper;

        /// <param name="filePathHelper">Resolves where avatar files are read from and saved to.</param>
        public AvatarImageProcessingService(AvatarFilePathHelper filePathHelper)
        {
            _filePathHelper = filePathHelper;
        }

        /// <summary>
        /// Reads an image from <paramref name="source"/>, center-crops it to
        /// a square, resizes to <see cref="AvatarSize"/>, and saves it as
        /// <paramref name="userId"/>'s avatar. Throws
        /// <see cref="SixLabors.ImageSharp.UnknownImageFormatException"/> if
        /// the stream isn't a decodable image - callers should turn that
        /// into a 400 response.
        /// </summary>
        public void SaveResized(Stream source, Guid userId)
        {
            Directory.CreateDirectory(_filePathHelper.GetAvatarsDirectory());
            var path = _filePathHelper.GetAvatarFilePath(userId);

            using var image = Image.Load(source);
            image.Mutate(x => x.Resize(new ResizeOptions
            {
                Size = new Size(AvatarSize, AvatarSize),
                Mode = ResizeMode.Crop,
            }));

            image.Save(path);
        }
    }
}
