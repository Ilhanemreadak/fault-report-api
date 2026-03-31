using LotusCode.Application.Interfaces;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace LotusCode.Infrastructure.Services
{
    /// <summary>
    /// Provides access to information about the currently authenticated user
    /// by reading claims from the current HTTP context.
    /// </summary>
    public sealed class CurrentUserService : ICurrentUserService
    {
        private readonly IHttpContextAccessor httpContextAccessor;

        public CurrentUserService(IHttpContextAccessor httpContextAccessor)
        {
            this.httpContextAccessor = httpContextAccessor;
        }

        public Guid UserId
        {
            get
            {
                var userIdValue = this.httpContextAccessor.HttpContext?.User?
                    .FindFirstValue(ClaimTypes.NameIdentifier);

                return Guid.TryParse(userIdValue, out var userId)
                    ? userId
                    : Guid.Empty;
            }
        }

        public string Email =>
            this.httpContextAccessor.HttpContext?.User?
                .FindFirstValue(ClaimTypes.Email) ?? string.Empty;

        public string Role =>
            this.httpContextAccessor.HttpContext?.User?
                .FindFirstValue(ClaimTypes.Role) ?? string.Empty;

        public bool IsAuthenticated =>
            this.httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated ?? false;
    }
}
