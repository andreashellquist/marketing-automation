namespace MarketingAutomation.SharedKernel;

/// <summary>Kinds of identifier used to resolve a person to a single contact.</summary>
public enum IdentifierType
{
    Email = 1,
    Phone = 2,
    DeviceId = 3,
    ExternalId = 4,
    AnonymousId = 5,
}
