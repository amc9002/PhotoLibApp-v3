using System;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace PhotoLibApi.Services
{
    /// <summary>
    /// Resolves the signed-in user's id from the current request's claims,
    /// set by <see cref="Controllers.AuthController.Login"/> at sign-in.
    /// </summary>
    public class CurrentUserService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public CurrentUserService(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        /// <summary>
        /// Id of the currently authenticated user. Only valid to read from
        /// an action guarded by <c>[Authorize]</c> - throws otherwise.
        /// </summary>
        public Guid UserId
        {
            get
            {
                var value = _httpContextAccessor.HttpContext?.User
                    .FindFirstValue(ClaimTypes.NameIdentifier);

                if (value == null || !Guid.TryParse(value, out var id))
                    throw new InvalidOperationException(
                        "No authenticated user on the current request.");

                return id;
            }
        }
    }
}
