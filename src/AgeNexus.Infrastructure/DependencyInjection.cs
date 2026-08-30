using AgeNexus.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AgeNexus.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddAgeNexusInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("AgeNexus");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Connection string 'AgeNexus' is missing. Configure it with .NET User Secrets or an environment variable.");
        }

        services.AddDbContext<AgeNexusDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsAssembly(typeof(AgeNexusDbContext).Assembly.FullName);
                npgsql.MigrationsHistoryTable("__ef_migrations_history", "public");
                npgsql.EnableRetryOnFailure(3);
            }));

        return services;
    }
}
