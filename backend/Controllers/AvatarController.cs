using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhotoLibApi.Data;
using PhotoLibApi.Models;
using PhotoLibApi.Services;

namespace PhotoLibApi.Controllers
{
    /// <summary>
    /// Upload and retrieval of the signed-in user's own avatar image. Split
    /// out from <see cref="AuthController"/> so that controller stays
    /// focused on auth/session concerns - this one owns avatar file
    /// storage/serving only, mirroring the split <see cref="PhotoController"/>
    /// already has for photo files.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class AvatarController : ControllerBase
    {
        /// <summary>Upper bound on how large an uploaded avatar file may be.</summary>
        private const long MaxAvatarBytes = 5 * 1024 * 1024;

        private readonly PhotoDbContext _db;
        private readonly AvatarFilePathHelper _avatarFilePathHelper;
        private readonly AvatarImageProcessingService _avatarProcessing;
        private readonly CurrentUserService _currentUser;

        /// <summary>Creates the controller with its DB context and supporting services.</summary>
        public AvatarController(
            PhotoDbContext db,
            AvatarFilePathHelper avatarFilePathHelper,
            AvatarImageProcessingService avatarProcessing,
            CurrentUserService currentUser)
        {
            _db = db;
            _avatarFilePathHelper = avatarFilePathHelper;
            _avatarProcessing = avatarProcessing;
            _currentUser = currentUser;
        }

        /// <summary>
        /// Uploads (or replaces) the signed-in user's avatar. The image is
        /// center-cropped to a square and resized server-side - any image
        /// format/aspect ratio can be sent as-is.
        /// </summary>
        /// <param name="file">Avatar image file.</param>
        /// <response code="200">Avatar saved.</response>
        /// <response code="400">Missing, empty, too large, or undecodable file.</response>
        /// <response code="401">Not signed in, or the session has expired.</response>
        [HttpPost("me")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> UploadAvatar([FromForm] IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { message = "File is required." });

            if (file.Length > MaxAvatarBytes)
                return BadRequest(new { message = "File is too large." });

            var id = _currentUser.UserId;
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id);
            if (user == null)
                return Unauthorized();

            try
            {
                await using var stream = file.OpenReadStream();
                _avatarProcessing.SaveResized(stream, id);
            }
            catch (SixLabors.ImageSharp.UnknownImageFormatException)
            {
                return BadRequest(new { message = "File is not a recognizable image." });
            }

            user.HasAvatar = true;
            await _db.SaveChangesAsync();

            return Ok(UserResponseMapper.ToResponse(user));
        }

        /// <summary>
        /// Returns the signed-in user's own avatar image.
        /// </summary>
        /// <response code="200">Avatar image returned.</response>
        /// <response code="404">No avatar set.</response>
        [HttpGet("me")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetMyAvatar()
        {
            var id = _currentUser.UserId;
            var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id);
            if (user == null || !user.HasAvatar)
                return NotFound();

            if (!_avatarFilePathHelper.AvatarExists(id))
                return NotFound();

            return PhysicalFile(_avatarFilePathHelper.GetAvatarFilePath(id), ImageContentTypes.Jpeg);
        }
    }
}
