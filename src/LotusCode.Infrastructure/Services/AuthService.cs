using LotusCode.Application.DTOs.Auth;
using LotusCode.Application.Exceptions;
using LotusCode.Application.Interfaces;
using LotusCode.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LotusCode.Infrastructure.Services
{
    /// <summary>
    /// Handles authentication logic including validating user credentials
    /// and generating JWT tokens.
    /// </summary>
    public sealed class AuthService : IAuthService
    {
        private readonly AppDbContext dbContext;
        private readonly IPasswordHasher passwordHasher;
        private readonly IJwtTokenService jwtTokenService;

        public AuthService(
            AppDbContext dbContext,
            IPasswordHasher passwordHasher,
            IJwtTokenService jwtTokenService)
        {
            this.dbContext = dbContext;
            this.passwordHasher = passwordHasher;
            this.jwtTokenService = jwtTokenService;
        }

        public async Task<LoginResponse> LoginAsync(
            LoginRequest request,
            CancellationToken cancellationToken)
        {
            var user = await this.dbContext.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Email == request.Email, cancellationToken);

            if (user is null)
            {
                throw new UnauthorizedException("Invalid email or password.");
            }

            var isPasswordValid = this.passwordHasher.Verify(
                request.Password,
                user.PasswordHash);

            if (!isPasswordValid)
            {
                throw new UnauthorizedException("Invalid email or password.");
            }

            var (token, expiresAtUtc) = this.jwtTokenService.GenerateToken(user);

            return new LoginResponse
            {
                Token = token,
                ExpiresAtUtc = expiresAtUtc,
                User = new AuthenticatedUserDto
                {
                    Id = user.Id,
                    FullName = user.FullName,
                    Email = user.Email,
                    Role = user.Role.ToString()
                }
            };
        }
    }
}
