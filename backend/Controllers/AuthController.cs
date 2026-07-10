using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using PhotoLibApi.Data;
using PhotoLibApi.Models;

namespace PhotoLibApi.Controllers
{
    /// <summary>
    /// Sign-in, sign-out, session lookup, and invite-only account creation.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly PhotoDbContext _db;
        private readonly IConfiguration _configuration;

        public AuthController(PhotoDbContext db, IConfiguration configuration)
        {
            _db = db;
            _configuration = configuration;
        }

        /// <summary>
        /// Signs in with an existing account and starts a session cookie.
        /// </summary>
        /// <param name="request">Login credentials.</param>
        /// <response code="200">Signed in.</response>
        /// <response code="401">Invalid email or password.</response>
        [HttpPost("login")]
        [AllowAnonymous]
        [EnableRateLimiting("AuthLogin")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var email = request.Email.Trim().ToLowerInvariant();
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);

            // Same generic message whether the account doesn't exist or the
            // password is wrong - distinguishing the two would let a caller
            // enumerate registered emails.
            if (user == null)
                return Unauthorized(new { message = "Invalid email or password." });

            var hasher = new PasswordHasher<User>();
            var verification = hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
            if (verification == PasswordVerificationResult.Failed)
                return Unauthorized(new { message = "Invalid email or password." });

            var identity = new ClaimsIdentity(
                new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                    new Claim(ClaimTypes.Email, user.Email),
                },
                CookieAuthenticationDefaults.AuthenticationScheme);

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(identity),
                new AuthenticationProperties
                {
                    IsPersistent = true,
                    ExpiresUtc = DateTimeOffset.UtcNow.AddDays(30),
                });

            return Ok(new { id = user.Id, email = user.Email });
        }

        /// <summary>
        /// Ends the current session.
        /// </summary>
        /// <response code="204">Signed out.</response>
        [HttpPost("logout")]
        [AllowAnonymous]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return NoContent();
        }

        /// <summary>
        /// Returns the signed-in user, if any.
        /// </summary>
        /// <response code="200">The current user.</response>
        /// <response code="401">Not signed in.</response>
        [HttpGet("me")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Me()
        {
            var id = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id);

            if (user == null)
                return Unauthorized();

            return Ok(new { id = user.Id, email = user.Email });
        }

        /// <summary>
        /// Creates a new invite-only account. Requires the shared
        /// <c>X-Admin-Key</c> header (see <c>Auth:AdminKey</c> config) - not
        /// a self-registration endpoint.
        /// </summary>
        /// <remarks>
        /// If this is the very first account ever created, every gallery
        /// left over from before authentication existed (<c>OwnerId ==
        /// null</c>) is reassigned to it in the same save. Registration is
        /// invite-only and gated by the admin key, so the first account is,
        /// by construction, the app owner's own - this is how today's
        /// single-owner data gets claimed once real accounts exist.
        /// </remarks>
        /// <param name="request">Email and password for the new account.</param>
        /// <response code="201">Account created.</response>
        /// <response code="403">Missing or incorrect <c>X-Admin-Key</c> header.</response>
        /// <response code="409">An account with this email already exists.</response>
        [HttpPost("admin/register")]
        [AllowAnonymous]
        [EnableRateLimiting("AuthAdminRegister")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> AdminRegister([FromBody] AdminRegisterRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            if (!HasValidAdminKey())
                return StatusCode(StatusCodes.Status403Forbidden);

            var email = request.Email.Trim().ToLowerInvariant();

            if (await _db.Users.AnyAsync(u => u.Email == email))
                return Conflict(new { message = "An account with this email already exists." });

            var isFirstUser = !await _db.Users.AnyAsync();

            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = email,
                CreatedAtUtc = DateTime.UtcNow,
            };
            user.PasswordHash = new PasswordHasher<User>().HashPassword(user, request.Password);

            _db.Users.Add(user);

            if (isFirstUser)
            {
                var orphaned = await _db.Galleries.Where(g => g.OwnerId == null).ToListAsync();
                foreach (var gallery in orphaned)
                {
                    gallery.OwnerId = user.Id;
                }
            }

            await _db.SaveChangesAsync();

            return StatusCode(StatusCodes.Status201Created, new { id = user.Id, email = user.Email });
        }

        /// <summary>
        /// Constant-time check of the <c>X-Admin-Key</c> request header
        /// against the configured <c>Auth:AdminKey</c> secret, so response
        /// timing can't leak how much of a guessed key was correct.
        /// </summary>
        private bool HasValidAdminKey()
        {
            var configuredKey = _configuration["Auth:AdminKey"];
            if (string.IsNullOrEmpty(configuredKey))
                return false;

            if (!Request.Headers.TryGetValue("X-Admin-Key", out var provided) || provided.Count == 0)
                return false;

            var configuredBytes = Encoding.UTF8.GetBytes(configuredKey);
            var providedBytes = Encoding.UTF8.GetBytes(provided.ToString());

            return CryptographicOperations.FixedTimeEquals(configuredBytes, providedBytes);
        }
    }
}
