using LotusCode.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace LotusCode.Infrastructure.Persistence
{
    /// <summary>
    /// Represents the application's database context.
    /// Provides access to application entities and automatically applies
    /// entity configurations from the infrastructure assembly.
    /// </summary>
    public sealed class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        {
        }

        public DbSet<User> Users => Set<User>();

        public DbSet<FaultReport> FaultReports => Set<FaultReport>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        }
    }
}
