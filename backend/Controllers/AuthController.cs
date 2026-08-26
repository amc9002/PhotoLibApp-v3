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
    /// Sign-in, sign-out, session lookup, self-service account creation,
    /// and an admin-key-gated endpoint for claiming legacy pre-auth data.
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

            await SignInAsync(user);

            return Ok(new { id = user.Id, email = user.Email });
        }

        /// <summary>
        /// Self-service account creation. Signs the new account in
        /// immediately, same as a successful <see cref="Login"/>.
        /// </summary>
        /// <remarks>
        /// Deliberately never claims legacy pre-auth data (galleries with
        /// <c>OwnerId == null</c>) - that's exclusive to
        /// <see cref="AdminRegister"/>, so only someone with the admin key
        /// can ever end up owning galleries that existed before any user
        /// account did. Open registration is safe here because reachability
        /// is expected to be restricted at the network level (e.g. a
        /// private Tailscale network) rather than by an account-creation
        /// gate - see the access guide for how that's set up.
        /// </remarks>
        /// <param name="request">Email and password for the new account.</param>
        /// <response code="201">Account created and signed in.</response>
        /// <response code="400">Invalid request data.</response>
        /// <response code="409">An account with this email already exists.</response>
        [HttpPost("register")]
        [AllowAnonymous]
        [EnableRateLimiting("AuthRegister")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var user = await TryCreateUserAsync(request.Email, request.Password);
            if (user == null)
                return Conflict(new { message = "An account with this email already exists." });

            if (!await TrySaveNewUserAsync())
                return Conflict(new { message = "An account with this email already exists." });

            await SignInAsync(user);

            return StatusCode(StatusCodes.Status201Created, new { id = user.Id, email = user.Email });
        }

        /// <summary>
        /// Changes the signed-in user's own password. Requires the current
        /// password, same as most account settings pages - proves the
        /// caller isn't just riding an unattended session cookie.
        /// </summary>
        /// <remarks>
        /// This is the only way to change a password today - there's no
        /// forgot-password/reset flow (would need email delivery, out of
        /// scope for now). Losing a password with no active session means
        /// starting over with a new account.
        /// </remarks>
        /// <param name="request">Current and new password.</param>
        /// <response code="204">Password changed.</response>
        /// <response code="400">Invalid request data (e.g. new password too short).</response>
        /// <response code="401">Not signed in, or the session has expired.</response>
        /// <response code="403">Current password is incorrect.</response>
        [HttpPost("change-password")]
        [Authorize]
        [EnableRateLimiting("AuthChangePassword")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var id = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id);
            if (user == null)
                return Unauthorized();

            var hasher = new PasswordHasher<User>();
            var verification = hasher.VerifyHashedPassword(user, user.PasswordHash, request.CurrentPassword);
            if (verification == PasswordVerificationResult.Failed)
                // 403, not 401: this endpoint is already [Authorize]-gated, so a 401 here
                // is reserved for "the session cookie itself is missing/expired" - the
                // frontend interceptor relies on that distinction to decide whether to
                // sign the user out (see auth.interceptor.ts).
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "Current password is incorrect." });

            user.PasswordHash = hasher.HashPassword(user, request.NewPassword);
            await _db.SaveChangesAsync();

            return NoContent();
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
        /// Creates a new account and claims any legacy pre-auth data.
        /// Requires the shared <c>X-Admin-Key</c> header (see
        /// <c>Auth:AdminKey</c> config) - normally only ever used once, to
        /// create the app owner's own account; friends should use the
        /// open <see cref="Register"/> endpoint instead.
        /// </summary>
        /// <remarks>
        /// If this is the very first account ever created, every gallery
        /// left over from before authentication existed (<c>OwnerId ==
        /// null</c>) is reassigned to it in the same save.
        /// </remarks>
        /// <param name="adminKey">
        /// The shared admin secret, sent as the <c>X-Admin-Key</c> header.
        /// </param>
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
        public async Task<IActionResult> AdminRegister(
            [FromHeader(Name = "X-Admin-Key")] string? adminKey,
            [FromBody] AdminRegisterRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            if (!HasValidAdminKey(adminKey))
                return StatusCode(StatusCodes.Status403Forbidden);

            var isFirstUser = !await _db.Users.AnyAsync();

            var user = await TryCreateUserAsync(request.Email, request.Password);
            if (user == null)
                return Conflict(new { message = "An account with this email already exists." });

            if (isFirstUser)
            {
                var orphaned = await _db.Galleries.Where(g => g.OwnerId == null).ToListAsync();
                foreach (var gallery in orphaned)
                {
                    gallery.OwnerId = user.Id;
                }
            }

            if (!await TrySaveNewUserAsync())
                return Conflict(new { message = "An account with this email already exists." });

            return StatusCode(StatusCodes.Status201Created, new { id = user.Id, email = user.Email });
        }

        /// <summary>
        /// Constant-time check of <paramref name="providedKey"/> against the
        /// configured <c>Auth:AdminKey</c> secret, so response timing can't
        /// leak how much of a guessed key was correct.
        /// </summary>
        private bool HasValidAdminKey(string? providedKey)
        {
            var configuredKey = _configuration["Auth:AdminKey"];
            if (string.IsNullOrEmpty(configuredKey) || string.IsNullOrEmpty(providedKey))
                return false;

            var configuredBytes = Encoding.UTF8.GetBytes(configuredKey);
            var providedBytes = Encoding.UTF8.GetBytes(providedKey);

            return CryptographicOperations.FixedTimeEquals(configuredBytes, providedBytes);
        }

        /// <summary>
        /// Builds, hashes, and stages (but does not save) a new
        /// <see cref="User"/> for the given email/password, or returns
        /// <c>null</c> if the (normalized) email is already registered.
        /// Shared by <see cref="Register"/> and <see cref="AdminRegister"/>.
        /// </summary>
        private async Task<User?> TryCreateUserAsync(string email, string password)
        {
            var normalizedEmail = email.Trim().ToLowerInvariant();

            if (await _db.Users.AnyAsync(u => u.Email == normalizedEmail))
                return null;

            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = normalizedEmail,
                CreatedAtUtc = DateTime.UtcNow,
            };
            user.PasswordHash = new PasswordHasher<User>().HashPassword(user, password);

            _db.Users.Add(user);
            return user;
        }

        /// <summary>
        /// Saves a user staged by <see cref="TryCreateUserAsync"/>, returning
        /// <c>false</c> instead of throwing if a concurrent request won the
        /// race to register the same email first.
        /// </summary>
        /// <remarks>
        /// <see cref="TryCreateUserAsync"/>'s existence check and this save
        /// aren't atomic, so two simultaneous requests for the same email can
        /// both pass the check before either commits. The database's unique
        /// index on <c>Users.Email</c> is the real guard; this just turns the
        /// resulting <see cref="DbUpdateException"/> into the same 409
        /// response an unaffected caller would get from the check alone,
        /// rather than an unhandled 500.
        /// </remarks>
        private async Task<bool> TrySaveNewUserAsync()
        {
            try
            {
                await _db.SaveChangesAsync();
                return true;
            }
            catch (DbUpdateException)
            {
                return false;
            }
        }

        /// <summary>
        /// Issues the session cookie for <paramref name="user"/>. Shared by
        /// <see cref="Login"/> and <see cref="Register"/> (the latter signs
        /// a brand-new account straight in).
        /// </summary>
        private async Task SignInAsync(User user)
        {
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
        }
    }
}
