using MarketingAutomation.Modules.Platform.Infrastructure;
using MarketingAutomation.Modules.Platform.Infrastructure.Outbox;
using MarketingAutomation.SharedKernel;
using MarketingAutomation.SharedKernel.Outbox;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MarketingAutomation.Modules.Platform;

public static class PlatformModule
{
    public static IServiceCollection AddPlatformModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<TenantContext>();
        services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext>());

        services.AddDbContext<PlatformDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("Postgres")));
        services.AddScoped<IOutboxStore>(sp => sp.GetRequiredService<PlatformDbContext>());

        services.AddHostedService<OutboxProcessor>();

        services.AddMassTransit(bus =>
        {
            bus.SetKebabCaseEndpointNameFormatter();
            bus.UsingRabbitMq((context, cfg) =>
            {
                cfg.Host(configuration.GetConnectionString("RabbitMq") ?? "rabbitmq://localhost");
                cfg.ConfigureEndpoints(context);
            });
        });

        return services;
    }
}
