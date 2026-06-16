using FluentValidation;
using MarketingAutomation.Modules.Contacts.Infrastructure;
using MarketingAutomation.SharedKernel.Contracts;
using MarketingAutomation.SharedKernel.Outbox;
using MarketingAutomation.SharedKernel.Segments;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MarketingAutomation.Modules.Contacts;

public static class ContactsModule
{
    public static IServiceCollection AddContactsModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<ContactsDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("Postgres")));
        services.AddScoped<IOutboxStore>(sp => sp.GetRequiredService<ContactsDbContext>());
        services.AddScoped<ISuppressionChecker, SuppressionChecker>();
        services.AddScoped<ISegmentEvaluator, SegmentEvaluator>();

        services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<ContactsModuleMarker>());
        services.AddValidatorsFromAssemblyContaining<ContactsModuleMarker>();

        return services;
    }
}

/// <summary>Assembly marker for MediatR/FluentValidation scanning.</summary>
public sealed class ContactsModuleMarker;
