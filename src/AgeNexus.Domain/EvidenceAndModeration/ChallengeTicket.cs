using AgeNexus.Domain.Common;

namespace AgeNexus.Domain.EvidenceAndModeration;

public sealed class ChallengeTicket
{
    private ChallengeTicket()
    {
        Code = null!;
        ConfigurationFingerprint = null!;
    }

    public ChallengeTicket(
        Guid id,
        Guid playerProfileId,
        Guid gameEditionId,
        string code,
        string configurationFingerprint,
        DateTimeOffset issuedAtUtc,
        TimeSpan validity)
    {
        if (id == Guid.Empty || playerProfileId == Guid.Empty || gameEditionId == Guid.Empty)
        {
            throw new DomainRuleException("Ticket, player and game edition ids are required.");
        }

        if (string.IsNullOrWhiteSpace(code) || code.Trim().Length is < 4 or > 16)
        {
            throw new DomainRuleException("Challenge code must contain between 4 and 16 characters.");
        }

        if (string.IsNullOrWhiteSpace(configurationFingerprint))
        {
            throw new DomainRuleException("Configuration fingerprint is required.");
        }

        if (issuedAtUtc.Offset != TimeSpan.Zero || validity <= TimeSpan.Zero || validity > TimeSpan.FromHours(1))
        {
            throw new DomainRuleException("Challenge ticket requires UTC time and validity up to one hour.");
        }

        Id = id;
        PlayerProfileId = playerProfileId;
        GameEditionId = gameEditionId;
        Code = code.Trim().ToUpperInvariant();
        ConfigurationFingerprint = configurationFingerprint.Trim().ToLowerInvariant();
        IssuedAtUtc = issuedAtUtc;
        ExpiresAtUtc = issuedAtUtc.Add(validity);
    }

    public Guid Id { get; private set; }
    public Guid PlayerProfileId { get; private set; }
    public Guid GameEditionId { get; private set; }
    public string Code { get; private set; }
    public string ConfigurationFingerprint { get; private set; }
    public DateTimeOffset IssuedAtUtc { get; private set; }
    public DateTimeOffset ExpiresAtUtc { get; private set; }
    public DateTimeOffset? UsedAtUtc { get; private set; }
    public bool IsUsed => UsedAtUtc.HasValue;

    public bool IsValidAt(DateTimeOffset instantUtc) =>
        instantUtc.Offset == TimeSpan.Zero && !IsUsed && instantUtc >= IssuedAtUtc && instantUtc <= ExpiresAtUtc;

    public void Consume(DateTimeOffset instantUtc)
    {
        if (!IsValidAt(instantUtc))
        {
            throw new DomainRuleException("Challenge ticket is expired, already used or not yet valid.");
        }

        UsedAtUtc = instantUtc;
    }
}
