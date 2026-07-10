using System.ComponentModel.DataAnnotations;

namespace PhotoLibApi.Models
{
    /// <summary>
    /// Request model used to sign in with an existing account.
    /// </summary>
    public class LoginRequest
    {
        /// <summary>
        /// Account email address.
        /// </summary>
        [Required]
        public string Email { get; set; } = "";

        /// <summary>
        /// Account password.
        /// </summary>
        [Required]
        public string Password { get; set; } = "";
    }
}
