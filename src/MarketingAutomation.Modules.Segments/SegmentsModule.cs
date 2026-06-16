using FluentValidation;
using MarketingAutomation.Modules.Segments.Infrastructure;
using MarketingAutomation.SharedKernel.Contracts;
using MarketingAutomation.SharedKernel.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MarketingAutomation.Modules.Segments;

public static class SegmentsModule
{
    public static IServiceCollection AddSegmentsModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<SegmentsDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("Postgres")));
        services.AddScoped<IOutboxStore>(sp => sp.GetRequiredService<SegmentsDbContext>());

        // Segments now owns audience resolution (replaces the Contacts placeholder).
        services.AddScoped<IAudienceResolver, SegmentAudienceResolver>();

        services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<SegmentsModuleMarker>());
        services.AddValidatorsFromAssemblyContaining<SegmentsModuleMarker>();

        return services;
    }
}

/// <summary>Assembly marker for MediatR/FluentValidation scanning.</summary>
public sealed class SegmentsModuleMarker;
