using LotusCode.Application.Interfaces;
using LotusCode.Infrastructure.Auth;
using LotusCode.Infrastructure.Persistence;
using LotusCode.Infrastructure.Persistence.Seed;
using LotusCode.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LotusCode.Infrastructure.DependencyInjection
{
    /// <summary>
    /// Registers infrastructure layer services such as database, authentication helpers,
    /// and application service implementations.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Adds infrastructure services and persistence dependencies to the service collection.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="configuration">The application configuration.</param>
        /// <returns>The updated service collection.</returns>
        public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
        {
            var connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("Connection string 'DefaultConnection' was not found.");

            services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));

            services.AddDbContext<AppDbContext>(options =>
            {
                options.UseSqlServer(connectionString);
            });

            services.AddHttpContextAccessor();

            services.AddScoped<ICurrentUserService, CurrentUserService>();
            services.AddScoped<IPasswordHasher, PasswordHasher>();
            services.AddScoped<IJwtTokenService, JwtTokenService>();
            services.AddScoped<IAuthService, AuthService>();
            services.AddScoped<DbSeeder>();
            services.AddScoped<IFaultReportService, FaultReportService>();

            return services;
        }
    }
}
