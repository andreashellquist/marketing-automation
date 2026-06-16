using FluentValidation;
using MarketingAutomation.Modules.Journeys.Domain;
using MarketingAutomation.Modules.Journeys.Infrastructure;
using MarketingAutomation.SharedKernel.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MarketingAutomation.Modules.Journeys;

public static class JourneysModule
{
    public static IServiceCollection AddJourneysModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<JourneysDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("Postgres")));
        services.AddScoped<IOutboxStore>(sp => sp.GetRequiredService<JourneysDbContext>());

        services.AddScoped<JourneyRunner>();
        services.AddHostedService<JourneyScheduler>();

        services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<JourneysModuleMarker>());
        services.AddValidatorsFromAssemblyContaining<JourneysModuleMarker>();

        return services;
    }
}

/// <summary>Assembly marker for MediatR/FluentValidation scanning.</summary>
public sealed class JourneysModuleMarker;
