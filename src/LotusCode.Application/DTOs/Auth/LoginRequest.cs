namespace LotusCode.Application.DTOs.Auth
{
    /// <summary>
    /// Represents login request payload containing user credentials.
    /// Used to authenticate user and generate JWT token.
    /// </summary>
    public sealed class LoginRequest
    {
        public string Email { get; init; } = string.Empty;

        public string Password { get; init; } = string.Empty;
    }
}
