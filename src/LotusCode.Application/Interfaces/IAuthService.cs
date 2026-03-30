using LotusCode.Application.DTOs.Auth;

namespace LotusCode.Application.Interfaces
{
    /// <summary>
    /// Defines authentication related operations.
    /// Responsible for validating user credentials and generating JWT tokens.
    /// </summary>
    public interface IAuthService
    {
        Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken);
    }
}
