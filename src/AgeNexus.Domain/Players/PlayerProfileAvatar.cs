using AgeNexus.Domain.Common;

namespace AgeNexus.Domain.Players;

public sealed class PlayerProfileAvatar
{
    private PlayerProfileAvatar()
    {
    }

    public PlayerProfileAvatar(
        Guid playerProfileId,
        string contentType,
        byte[] content,
        string sha256,
        DateTimeOffset updatedAtUtc)
    {
        PlayerProfileId = playerProfileId;
        Replace(contentType, content, sha256, updatedAtUtc);
    }

    public Guid PlayerProfileId { get; private set; }
    public string ContentType { get; private set; } = string.Empty;
    public byte[] Content { get; private set; } = [];
    public string Sha256 { get; private set; } = string.Empty;
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public void Replace(string contentType, byte[] content, string sha256, DateTimeOffset updatedAtUtc)
    {
        if (PlayerProfileId == Guid.Empty ||
            contentType is not ("image/jpeg" or "image/png" or "image/webp") ||
            content.Length is 0 or > 2 * 1024 * 1024 ||
            sha256.Length != 64 ||
            updatedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new DomainRuleException("A valid profile avatar is required.");
        }

        ContentType = contentType;
        Content = content.ToArray();
        Sha256 = sha256;
        UpdatedAtUtc = updatedAtUtc;
    }
}
