using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace MarketingAutomation.SharedKernel.Application;

public static class SharedApplicationExtensions
{
    /// <summary>
    /// Registers cross-cutting application services once for the whole app. The
    /// validation behavior is an open generic, so it applies to every module's
    /// MediatR requests without per-module registration (which would double-run it).
    /// </summary>
    public static IServiceCollection AddSharedApplication(this IServiceCollection services)
    {
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        return services;
    }
}
