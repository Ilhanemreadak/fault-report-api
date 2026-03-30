namespace LotusCode.Application.DTOs.Auth
{
    public sealed class AuthenticatedUserDto
    {
        public Guid Id { get; init; }

        public string FullName { get; init; } = string.Empty;

        public string Email { get; init; } = string.Empty;

        public string Role { get; init; } = string.Empty;
    }
}
