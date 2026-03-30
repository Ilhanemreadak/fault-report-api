namespace LotusCode.Application.DTOs.Auth
{
    /// <summary>
    /// Represents authenticated user information returned after successful login.
    /// Contains basic user identity and role information.
    /// </summary>
    public sealed class AuthenticatedUserDto
    {
        public Guid Id { get; init; }

        public string FullName { get; init; } = string.Empty;

        public string Email { get; init; } = string.Empty;

        public string Role { get; init; } = string.Empty;
    }
}
