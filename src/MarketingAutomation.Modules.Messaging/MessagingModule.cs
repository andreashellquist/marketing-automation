using FluentValidation;
using MarketingAutomation.Modules.Messaging.Application;
using MarketingAutomation.Modules.Messaging.Domain;
using MarketingAutomation.Modules.Messaging.Infrastructure;
using MarketingAutomation.SharedKernel;
using MarketingAutomation.SharedKernel.Contracts;
using MarketingAutomation.SharedKernel.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MarketingAutomation.Modules.Messaging;

public static class MessagingModule
{
    public static IServiceCollection AddMessagingModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<MessagingDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("Postgres")));
        services.AddScoped<IOutboxStore>(sp => sp.GetRequiredService<MessagingDbContext>());

        services.AddScoped<SendPolicyGate>();
        services.AddScoped<IMessageSender, MessageSenderAdapter>();
        services.AddScoped<IMessageStatsProvider, MessageStatsProvider>();

        // Default dev senders — one per channel. Swap for SendGrid/Twilio/FCM/SMTP later.
        foreach (var channel in new[] { Channel.Email, Channel.Sms, Channel.Push, Channel.WhatsApp })
        {
            services.AddSingleton<IChannelSender>(sp =>
                new LoggingChannelSender(channel, sp.GetRequiredService<ILogger<LoggingChannelSender>>()));
        }

        services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<MessagingModuleMarker>());
        services.AddValidatorsFromAssemblyContaining<MessagingModuleMarker>();

        return services;
    }
}

/// <summary>Assembly marker for MediatR/FluentValidation scanning.</summary>
public sealed class MessagingModuleMarker;
