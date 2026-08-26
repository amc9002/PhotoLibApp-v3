using System.ComponentModel.DataAnnotations;

namespace PhotoLibApi.Models
{
    /// <summary>
    /// Request model for changing the current user's own password.
    /// </summary>
    public class ChangePasswordRequest
    {
        /// <summary>
        /// The account's current password, required to authorize the change.
        /// </summary>
        [Required]
        public string CurrentPassword { get; set; } = "";

        /// <summary>
        /// The new password to set.
        /// </summary>
        [Required]
        [MinLength(8)]
        public string NewPassword { get; set; } = "";
    }
}
