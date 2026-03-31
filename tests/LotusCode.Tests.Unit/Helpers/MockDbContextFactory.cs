using LotusCode.Domain.Entities;
using LotusCode.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LotusCode.Tests.Unit.Helpers
{
    /// <summary>
    /// Factory for creating configured in-memory database contexts for testing.
    /// Provides methods to create isolated database instances and seed test data.
    /// </summary>
    public static class MockDbContextFactory
    {
        /// <summary>
        /// Creates an in-memory database context with a unique database name.
        /// Each call creates an isolated database instance for test isolation.
        /// </summary>
        /// <param name="databaseName">Optional database name. If not provided, a unique GUID is used.</param>
        /// <returns>A configured AppDbContext using in-memory database provider.</returns>
        public static AppDbContext CreateInMemoryDbContext(string? databaseName = null)
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName ?? Guid.NewGuid().ToString())
                .Options;

            return new AppDbContext(options);
        }

        /// <summary>
        /// Creates an in-memory database context pre-seeded with test data.
        /// Useful for tests that require existing data in the database.
        /// </summary>
        /// <param name="faultReports">Optional list of fault reports to seed.</param>
        /// <param name="users">Optional list of users to seed.</param>
        /// <returns>A configured AppDbContext with seeded data.</returns>
        public static AppDbContext CreateDbContextWithData(
            List<FaultReport>? faultReports = null,
            List<User>? users = null)
        {
            var context = CreateInMemoryDbContext();

            if (users != null)
            {
                context.Users.AddRange(users);
            }

            if (faultReports != null)
            {
                context.FaultReports.AddRange(faultReports);
            }

            context.SaveChanges();
            return context;
        }
    }
}
