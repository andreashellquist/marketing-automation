using FluentValidation;
using MarketingAutomation.Modules.Events.Infrastructure;
using MarketingAutomation.SharedKernel.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MarketingAutomation.Modules.Events;

public static class EventsModule
{
    public static IServiceCollection AddEventsModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<EventsDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("Postgres")));
        services.AddScoped<IOutboxStore>(sp => sp.GetRequiredService<EventsDbContext>());

        services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<EventsModuleMarker>());
        services.AddValidatorsFromAssemblyContaining<EventsModuleMarker>();

        return services;
    }
}

/// <summary>Assembly marker for MediatR/FluentValidation scanning.</summary>
public sealed class EventsModuleMarker;
