using System.Security.Cryptography;
using System.Text;
using AgeNexus.Domain.Common;

namespace AgeNexus.Domain.Competition;

public sealed class TeamLineup
{
    private readonly List<TeamLineupMember> _members = [];

    private TeamLineup()
    {
        NormalizedKey = null!;
    }

    private TeamLineup(Guid id, Guid gameEditionId, string normalizedKey, DateTimeOffset createdAtUtc)
    {
        Id = id;
        GameEditionId = gameEditionId;
        NormalizedKey = normalizedKey;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid GameEditionId { get; private set; }
    public string NormalizedKey { get; private set; }
    public int MemberCount => _members.Count;
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public IReadOnlyCollection<TeamLineupMember> Members => _members.AsReadOnly();

    public static TeamLineup Create(
        Guid gameEditionId,
        IReadOnlyCollection<Guid> playerProfileIds,
        DateTimeOffset createdAtUtc)
    {
        ArgumentNullException.ThrowIfNull(playerProfileIds);
        if (gameEditionId == Guid.Empty || playerProfileIds.Count == 0 ||
            playerProfileIds.Any(x => x == Guid.Empty) ||
            playerProfileIds.Distinct().Count() != playerProfileIds.Count ||
            createdAtUtc.Offset != TimeSpan.Zero)
        {
            throw new DomainRuleException("A lineup requires an edition, unique players and UTC creation time.");
        }

        var orderedIds = playerProfileIds.Order().ToArray();
        var key = $"{gameEditionId:N}:{string.Join(",", orderedIds.Select(x => x.ToString("N")))}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(key));
        var guidBytes = hash[..16];
        guidBytes[6] = (byte)((guidBytes[6] & 0x0F) | 0x50);
        guidBytes[8] = (byte)((guidBytes[8] & 0x3F) | 0x80);
        var lineup = new TeamLineup(new Guid(guidBytes), gameEditionId, key, createdAtUtc);
        for (var index = 0; index < orderedIds.Length; index++)
        {
            lineup._members.Add(new TeamLineupMember(Guid.NewGuid(), orderedIds[index], index + 1));
        }

        return lineup;
    }
}

public sealed class TeamLineupMember
{
    private TeamLineupMember()
    {
    }

    internal TeamLineupMember(Guid id, Guid playerProfileId, int position)
    {
        Id = id;
        PlayerProfileId = playerProfileId;
        Position = position;
    }

    public Guid Id { get; private set; }
    public Guid PlayerProfileId { get; private set; }
    public int Position { get; private set; }
}
