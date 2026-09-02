using AgeNexus.Infrastructure.Persistence;
using AgeNexus.Infrastructure.Identity;
using AgeNexus.Infrastructure.Competition;
using AgeNexus.Application.Matches;
using AgeNexus.Application.Queries;
using AgeNexus.Infrastructure.Queries;
using AgeNexus.Application.MatchPerformance;
using AgeNexus.Infrastructure.MatchPerformance;
using AgeNexus.Infrastructure.ReplayAnalysis;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
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

        services
            .AddIdentityCore<ApplicationUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Password.RequiredLength = 10;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = true;
                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                options.SignIn.RequireConfirmedAccount = false;
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<AgeNexusDbContext>()
            .AddSignInManager()
            .AddDefaultTokenProviders();

        services
            .AddAuthentication(IdentityConstants.ApplicationScheme)
            .AddIdentityCookies();
        services.AddAuthorization();

        services.ConfigureApplicationCookie(options =>
        {
            options.Cookie.Name = "AgeNexus.Auth";
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
            options.ExpireTimeSpan = TimeSpan.FromDays(14);
            options.SlidingExpiration = true;
            options.LoginPath = "/conta/login";
            options.AccessDeniedPath = "/conta/acesso-negado";
        });

        services.AddScoped<AccountService>();
        services.AddScoped<IMatchWorkflowService, MatchWorkflowService>();
        services.AddScoped<IPerformanceStatisticsService, PerformanceStatisticsService>();
        services.AddScoped<IReplayStatisticsExtractor, PythonReplayStatisticsExtractor>();
        services.AddScoped<CompetitionQueryService>();
        services.AddScoped<IRankingQueryService>(x => x.GetRequiredService<CompetitionQueryService>());
        services.AddScoped<IMatchHistoryQueryService>(x => x.GetRequiredService<CompetitionQueryService>());
        services.AddScoped<IPlayerDirectoryQueryService>(x => x.GetRequiredService<CompetitionQueryService>());
        services.AddScoped<IStatisticsQueryService>(x => x.GetRequiredService<CompetitionQueryService>());
        services.AddScoped<IClanQueryService>(x => x.GetRequiredService<CompetitionQueryService>());
        services.AddScoped<ICatalogQueryService>(x => x.GetRequiredService<CompetitionQueryService>());

        return services;
    }
}
