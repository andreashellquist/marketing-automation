using MarketingAutomation.SharedKernel;

namespace MarketingAutomation.Modules.Contacts.Domain;

/// <summary>
/// Canonicalizes identifier values so resolution and suppression match reliably.
/// Phone normalization here is deliberately minimal (strip spacing); a full E.164
/// library is wired in when the SMS channel lands.
/// </summary>
public static class Normalize
{
    public static string Identifier(IdentifierType type, string value) => type switch
    {
        IdentifierType.Email => Email(value),
        IdentifierType.Phone => Phone(value),
        _ => value.Trim(),
    };

    public static string Email(string value) => value.Trim().ToLowerInvariant();

    public static string Phone(string value) =>
        new string(value.Where(c => char.IsDigit(c) || c == '+').ToArray());
}
