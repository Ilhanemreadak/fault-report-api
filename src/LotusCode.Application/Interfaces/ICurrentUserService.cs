using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LotusCode.Application.Interfaces
{
    /// <summary>
    /// Provides information about the currently authenticated user.
    /// Abstracts access to user context from HTTP layer for use in application services.
    /// </summary>
    public interface ICurrentUserService
    {
        Guid UserId { get; }

        string Email { get; }

        string Role { get; }

        bool IsAuthenticated { get; }
    }
}
