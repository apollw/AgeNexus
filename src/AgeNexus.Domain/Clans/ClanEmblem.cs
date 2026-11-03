using AgeNexus.Domain.Common;

namespace AgeNexus.Domain.Clans;

public sealed class ClanEmblem
{
    private ClanEmblem()
    {
    }

    public ClanEmblem(Guid clanId, string contentType, byte[] content, string sha256, DateTimeOffset updatedAtUtc)
    {
        ClanId = clanId;
        Replace(contentType, content, sha256, updatedAtUtc);
    }

    public Guid ClanId { get; private set; }
    public string ContentType { get; private set; } = string.Empty;
    public byte[] Content { get; private set; } = [];
    public string Sha256 { get; private set; } = string.Empty;
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public void Replace(string contentType, byte[] content, string sha256, DateTimeOffset updatedAtUtc)
    {
        if (ClanId == Guid.Empty ||
            contentType is not ("image/jpeg" or "image/png" or "image/webp") ||
            content.Length is 0 or > 2 * 1024 * 1024 ||
            sha256.Length != 64 ||
            updatedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new DomainRuleException("A valid clan emblem is required.");
        }

        ContentType = contentType;
        Content = content.ToArray();
        Sha256 = sha256;
        UpdatedAtUtc = updatedAtUtc;
    }
}
