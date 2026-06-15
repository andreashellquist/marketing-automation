namespace MarketingAutomation.Modules.Contacts.Domain;

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
