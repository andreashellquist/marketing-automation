using FluentValidation;
using MarketingAutomation.Modules.Templates.Domain;
using MarketingAutomation.Modules.Templates.Infrastructure;
using MarketingAutomation.SharedKernel.Contracts;
using MarketingAutomation.SharedKernel.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MarketingAutomation.Modules.Templates;

public static class TemplatesModule
{
    public static IServiceCollection AddTemplatesModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<TemplatesDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("Postgres")));
        services.AddScoped<IOutboxStore>(sp => sp.GetRequiredService<TemplatesDbContext>());

        services.AddSingleton<LiquidRenderer>();
        services.AddScoped<ITemplateRenderer, TemplateRenderer>();

        services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<TemplatesModuleMarker>());
        services.AddValidatorsFromAssemblyContaining<TemplatesModuleMarker>();

        return services;
    }
}

/// <summary>Assembly marker for MediatR/FluentValidation scanning.</summary>
public sealed class TemplatesModuleMarker;
