using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using FluentValidation;

namespace LotusCode.Application.DependencyInjection
{
    /// <summary>
    /// Registers application layer services such as validators and future application services.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Adds application layer dependencies to the service collection.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <returns>The updated service collection.</returns>
        public static IServiceCollection AddApplication(this IServiceCollection services)
        {
            services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());

            return services;
        }
    }
}
