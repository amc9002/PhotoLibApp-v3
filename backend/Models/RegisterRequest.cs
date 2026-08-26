using System.ComponentModel.DataAnnotations;

namespace PhotoLibApi.Models
{
    /// <summary>
    /// Request model for self-service account creation.
    /// </summary>
    public class RegisterRequest
    {
        /// <summary>
        /// Email address for the new account.
        /// </summary>
        [Required]
        [EmailAddress]
        public string Email { get; set; } = "";

        /// <summary>
        /// Password for the new account.
        /// </summary>
        [Required]
        [MinLength(8)]
        public string Password { get; set; } = "";
    }
}
