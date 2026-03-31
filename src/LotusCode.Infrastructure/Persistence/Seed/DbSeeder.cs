using LotusCode.Application.Interfaces;
using LotusCode.Domain.Entities;
using LotusCode.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace LotusCode.Infrastructure.Persistence.Seed
{
    /// <summary>
    /// Seeds initial application data required for development and testing.
    /// </summary>
    public sealed class DbSeeder
    {
        private readonly AppDbContext dbContext;
        private readonly IPasswordHasher passwordHasher;

        /// <summary>
        /// Initializes a new instance of the <see cref="DbSeeder"/> class.
        /// </summary>
        /// <param name="dbContext">The database context.</param>
        /// <param name="passwordHasher">The password hasher.</param>
        public DbSeeder(AppDbContext dbContext, IPasswordHasher passwordHasher)
        {
            this.dbContext = dbContext;
            this.passwordHasher = passwordHasher;
        }

        /// <summary>
        /// Seeds the initial data if it does not already exist.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        public async Task SeedAsync(CancellationToken cancellationToken = default)
        {
            if (await this.dbContext.Users.AnyAsync(cancellationToken))
            {
                return;
            }

            var now = DateTime.UtcNow;

            var adminUser = new User
            {
                Id = Guid.NewGuid(),
                FullName = "System Admin",
                Email = "admin@lotus.local",
                PasswordHash = this.passwordHasher.Hash("Admin123!"),
                Role = UserRole.Admin,
                CreatedAtUtc = now
            };

            var normalUser = new User
            {
                Id = Guid.NewGuid(),
                FullName = "Field User",
                Email = "user@lotus.local",
                PasswordHash = this.passwordHasher.Hash("User123!"),
                Role = UserRole.User,
                CreatedAtUtc = now
            };

            await this.dbContext.Users.AddRangeAsync(new[] { adminUser, normalUser }, cancellationToken);
            await this.dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
