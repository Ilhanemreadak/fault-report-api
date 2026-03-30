using LotusCode.Application.Interfaces;
using LotusCode.Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace LotusCode.Infrastructure.Auth
{
    /// <summary>
    /// Provides password hashing and verification functionality
    /// using ASP.NET Core's built-in password hasher.
    /// </summary>
    public sealed class PasswordHasher : IPasswordHasher
    {
        private readonly Microsoft.AspNetCore.Identity.PasswordHasher<User> passwordHasher = new();

        public string Hash(string password)
        {
            return this.passwordHasher.HashPassword(user: null!, password);
        }

        public bool Verify(string password, string passwordHash)
        {
            var result = this.passwordHasher.VerifyHashedPassword(user: null!, passwordHash, password);

            return result == PasswordVerificationResult.Success
                || result == PasswordVerificationResult.SuccessRehashNeeded;
        }
    }
}
