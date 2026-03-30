using LotusCode.Domain.Entities;

namespace LotusCode.Application.Interfaces
{
    /// <summary>
    /// Defines JWT token generation operations for authenticated users.
    /// Responsible for creating signed access tokens and expiration metadata.
    /// </summary>
    public interface IJwtTokenService
    {
        (string Token, DateTime ExpiresAtUtc) GenerateToken(User user);
    }
}
