using FluentValidation;
using MarketingAutomation.Modules.Campaigns.Infrastructure;
using MarketingAutomation.SharedKernel.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MarketingAutomation.Modules.Campaigns;

public static class CampaignsModule
{
    public static IServiceCollection AddCampaignsModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<CampaignsDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("Postgres")));
        services.AddScoped<IOutboxStore>(sp => sp.GetRequiredService<CampaignsDbContext>());

        services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<CampaignsModuleMarker>());
        services.AddValidatorsFromAssemblyContaining<CampaignsModuleMarker>();

        return services;
    }
}

/// <summary>Assembly marker for MediatR/FluentValidation scanning.</summary>
public sealed class CampaignsModuleMarker;
