using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LotusCode.Application.DTOs.Auth
{
    public sealed class LoginResponse
    {
        public string Token { get; init; } = string.Empty;

        public DateTime ExpiresAtUtc { get; init; }

        public AuthenticatedUserDto User { get; init; } = default!;
    }
}
