namespace MarketingAutomation.SharedKernel.Application;

/// <summary>Thrown when a requested entity does not exist (or is filtered out by tenancy).</summary>
public sealed class NotFoundException(string resource, object key)
    : Exception($"{resource} '{key}' was not found.")
{
    public string Resource { get; } = resource;
}

/// <summary>Thrown when a request is well-formed but violates a domain rule.</summary>
public sealed class DomainConflictException(string message) : Exception(message);
