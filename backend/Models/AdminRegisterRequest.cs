using System.ComponentModel.DataAnnotations;

namespace PhotoLibApi.Models
{
    /// <summary>
    /// Request model used by the app owner to create a new invite-only
    /// account. Requires the <c>X-Admin-Key</c> header - see
    /// <see cref="Controllers.AuthController.AdminRegister"/>.
    /// </summary>
    public class AdminRegisterRequest
    {
        /// <summary>
        /// Email address for the new account.
        /// </summary>
        [Required]
        public string Email { get; set; } = "";

        /// <summary>
        /// Password for the new account.
        /// </summary>
        [Required]
        public string Password { get; set; } = "";

        /// <summary>
        /// Display name for the new account.
        /// </summary>
        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = "";
    }
}
