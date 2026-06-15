namespace MarketingAutomation.Modules.Contacts.Domain;

/// <summary>Kinds of identifier used to resolve a person to a single contact.</summary>
public enum IdentifierType
{
    Email = 1,
    Phone = 2,
    DeviceId = 3,
    ExternalId = 4,
    AnonymousId = 5,
}

public enum ConsentPurpose
{
    Marketing = 1,
    Transactional = 2,
}

public enum ConsentStatus
{
    Granted = 1,
    Revoked = 2,
}

public enum SuppressionReason
{
    HardBounce = 1,
    SpamComplaint = 2,
    Unsubscribe = 3,
    StopReply = 4,
    Manual = 5,
}
