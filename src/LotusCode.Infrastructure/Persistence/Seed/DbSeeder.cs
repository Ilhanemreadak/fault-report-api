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
            var now = DateTime.UtcNow;

            var adminUser = await this.dbContext.Users
                .FirstOrDefaultAsync(x => x.Email == "admin@lotus.local", cancellationToken);

            if (adminUser is null)
            {
                adminUser = new User
                {
                    Id = Guid.NewGuid(),
                    FullName = "System Admin",
                    Email = "admin@lotus.local",
                    PasswordHash = this.passwordHasher.Hash("Admin123!"),
                    Role = UserRole.Admin,
                    CreatedAtUtc = now
                };

                await this.dbContext.Users.AddAsync(adminUser, cancellationToken);
            }

            var normalUser = await this.dbContext.Users
                .FirstOrDefaultAsync(x => x.Email == "user@lotus.local", cancellationToken);

            if (normalUser is null)
            {
                normalUser = new User
                {
                    Id = Guid.NewGuid(),
                    FullName = "Field User",
                    Email = "user@lotus.local",
                    PasswordHash = this.passwordHasher.Hash("User123!"),
                    Role = UserRole.User,
                    CreatedAtUtc = now
                };

                await this.dbContext.Users.AddAsync(normalUser, cancellationToken);
            }

            await this.dbContext.SaveChangesAsync(cancellationToken);

            var statuses = new[]
            {
                FaultReportStatus.New,
                FaultReportStatus.Reviewing,
                FaultReportStatus.Assigned,
                FaultReportStatus.InProgress,
                FaultReportStatus.Completed,
                FaultReportStatus.Cancelled,
                FaultReportStatus.FalseAlarm
            };

            var priorities = new[]
            {
                PriorityLevel.Low,
                PriorityLevel.Medium,
                PriorityLevel.High
            };

            var desiredReportCount = 12;
            var seedTitles = Enumerable.Range(1, desiredReportCount)
                .Select(x => $"Seed Fault Report {x}")
                .ToArray();

            var existingSeedTitles = await this.dbContext.FaultReports
                .AsNoTracking()
                .Where(x => seedTitles.Contains(x.Title))
                .Select(x => x.Title)
                .ToListAsync(cancellationToken);

            var existingSeedTitleSet = existingSeedTitles
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var seedReports = new List<FaultReport>();

            for (var i = 0; i < desiredReportCount; i++)
            {
                var title = $"Seed Fault Report {i + 1}";

                if (existingSeedTitleSet.Contains(title))
                {
                    continue;
                }

                var createdAt = now.AddHours(-(i + 2));
                var createdBy = i % 2 == 0 ? adminUser : normalUser;

                seedReports.Add(new FaultReport
                {
                    Id = Guid.NewGuid(),
                    Title = title,
                    Description = $"Seed data report description {i + 1}.",
                    Location = $"Ankara/Çankaya/Mahalle-{i + 1}",
                    Priority = priorities[i % priorities.Length],
                    Status = statuses[i % statuses.Length],
                    CreatedByUserId = createdBy.Id,
                    CreatedAtUtc = createdAt,
                    UpdatedAtUtc = createdAt.AddMinutes(30)
                });
            }

            if (seedReports.Count > 0)
            {
                await this.dbContext.FaultReports.AddRangeAsync(seedReports, cancellationToken);
                await this.dbContext.SaveChangesAsync(cancellationToken);
            }
        }
    }
}
