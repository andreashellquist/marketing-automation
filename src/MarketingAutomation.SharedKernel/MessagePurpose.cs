namespace MarketingAutomation.SharedKernel;

/// <summary>
/// Whether a message is marketing (subject to consent, suppression, quiet hours,
/// frequency caps) or transactional (exempt from marketing policy).
/// </summary>
public enum MessagePurpose
{
    Marketing = 1,
    Transactional = 2,
}
