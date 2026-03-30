using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LotusCode.Application.DTOs.Auth
{
    /// <summary>
    /// Represents login response containing JWT token, expiration time
    /// and authenticated user information.
    /// </summary>
    public sealed class LoginResponse
    {
        public string Token { get; init; } = string.Empty;

        public DateTime ExpiresAtUtc { get; init; }

        public AuthenticatedUserDto User { get; init; } = default!;
    }
}
