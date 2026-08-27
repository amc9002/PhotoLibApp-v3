namespace PhotoLibApi.Models
{
    /// <summary>
    /// Shapes a <see cref="User"/> into the response every account/profile
    /// endpoint returns - never includes <see cref="User.PasswordHash"/>.
    /// Shared by <see cref="Controllers.AuthController"/> and
    /// <see cref="Controllers.AvatarController"/>.
    /// </summary>
    public static class UserResponseMapper
    {
        /// <summary>Builds the public-facing response shape for <paramref name="user"/>.</summary>
        public static object ToResponse(User user) => new
        {
            id = user.Id,
            email = user.Email,
            name = user.Name,
            bio = user.Bio,
            hasAvatar = user.HasAvatar,
        };
    }
}
