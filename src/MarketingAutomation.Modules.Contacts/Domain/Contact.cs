using MarketingAutomation.SharedKernel;

namespace MarketingAutomation.Modules.Contacts.Domain;

/// <summary>
/// A unified customer profile (the CDP core). Standard fields plus tenant-defined
/// custom attributes; reachable via one or more <see cref="ContactIdentifier"/>s and
/// carrying an append-only <see cref="ConsentEntry"/> ledger.
/// </summary>
public sealed class Contact : TenantEntity
{
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Locale { get; set; }

    /// <summary>IANA timezone (e.g. "Europe/Stockholm"); drives quiet-hours resolution.</summary>
    public string? Timezone { get; set; }

    public Dictionary<string, object?> CustomAttributes { get; set; } = new();

    public List<ContactIdentifier> Identifiers { get; } = [];
    public List<ConsentEntry> ConsentEntries { get; } = [];

    /// <summary>Applies inbound traits without clobbering existing values with nulls.</summary>
    public void ApplyTraits(ContactTraits traits)
    {
        Email = traits.Email ?? Email;
        Phone = traits.Phone ?? Phone;
        FirstName = traits.FirstName ?? FirstName;
        LastName = traits.LastName ?? LastName;
        Locale = traits.Locale ?? Locale;
        Timezone = traits.Timezone ?? Timezone;

        foreach (var (key, value) in traits.CustomAttributes)
        {
            CustomAttributes[key] = value;
        }
    }

    public void AddIdentifierIfMissing(IdentifierType type, string value)
    {
        if (!Identifiers.Any(i => i.Type == type && i.Value == value))
        {
            Identifiers.Add(new ContactIdentifier { ContactId = Id, Type = type, Value = value });
        }
    }

    public ConsentEntry RecordConsent(
        Channel channel, ConsentPurpose purpose, ConsentStatus status,
        string source, string? ipAddress, string? consentText)
    {
        var entry = new ConsentEntry
        {
            ContactId = Id,
            Channel = channel,
            Purpose = purpose,
            Status = status,
            Source = source,
            IpAddress = ipAddress,
            ConsentText = consentText,
            RecordedAt = DateTimeOffset.UtcNow,
        };
        ConsentEntries.Add(entry);
        return entry;
    }

    /// <summary>Current consent for a channel+purpose = the most recent ledger entry.</summary>
    public ConsentStatus? CurrentConsent(Channel channel, ConsentPurpose purpose) =>
        ConsentEntries
            .Where(c => c.Channel == channel && c.Purpose == purpose)
            .OrderByDescending(c => c.RecordedAt)
            .FirstOrDefault()?.Status;
}

public sealed record ContactTraits(
    string? Email = null,
    string? Phone = null,
    string? FirstName = null,
    string? LastName = null,
    string? Locale = null,
    string? Timezone = null,
    Dictionary<string, object?>? Attributes = null)
{
    public Dictionary<string, object?> CustomAttributes => Attributes ?? new();
}

public sealed class ContactIdentifier : TenantEntity
{
    public Guid ContactId { get; set; }
    public IdentifierType Type { get; set; }
    public required string Value { get; set; }
}

/// <summary>Append-only consent record. Never updated or deleted — new entries supersede.</summary>
public sealed class ConsentEntry : TenantEntity
{
    public Guid ContactId { get; set; }
    public Channel Channel { get; set; }
    public ConsentPurpose Purpose { get; set; }
    public ConsentStatus Status { get; set; }
    public required string Source { get; set; }
    public string? IpAddress { get; set; }
    public string? ConsentText { get; set; }
    public DateTimeOffset RecordedAt { get; set; }
}
