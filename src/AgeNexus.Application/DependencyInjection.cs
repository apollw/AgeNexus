using AgeNexus.Application.Ratings;
using AgeNexus.Domain.Competition;
using Microsoft.Extensions.DependencyInjection;

namespace AgeNexus.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddAgeNexusApplication(this IServiceCollection services)
    {
        services.AddSingleton<ScoringRuleSet>();
        services.AddSingleton<IRatingCalculator, EloRatingCalculator>();
        services.AddSingleton<ICareerPointCalculator, CareerPointCalculator>();
        services.AddSingleton<IPvePointCalculator, PvePointCalculator>();
        return services;
    }
}
