using AgeNexus.Application.Queries;
using AgeNexus.Domain.Clans;
using AgeNexus.Domain.Common;
using AgeNexus.Infrastructure.Identity;
using AgeNexus.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AgeNexus.Infrastructure.Clans;

public sealed class ClanService(AgeNexusDbContext database, AccountService accounts) : IClanService
{
    public async Task<ClanOperationResult> CreateAsync(
        Guid createdByPlayerProfileId,
        string name,
        string tag,
        IReadOnlyCollection<Guid> memberPlayerProfileIds,
        CancellationToken cancellationToken = default)
    {
        if (!await accounts.IsActiveLinkedProfileAsync(createdByPlayerProfileId, cancellationToken))
        {
            return ClanOperationResult.Failure("NotAuthorized");
        }

        var members = memberPlayerProfileIds
            .Where(x => x != Guid.Empty)
            .Append(createdByPlayerProfileId)
            .Distinct()
            .ToArray();
        if (members.Length is < 1 or > 8)
        {
            return ClanOperationResult.Failure("InvalidMembers");
        }

        var existingPlayers = await database.PlayerProfiles.AsNoTracking()
            .Where(x => members.Contains(x.Id))
            .Select(x => x.Id)
            .ToArrayAsync(cancellationToken);
        if (existingPlayers.Length != members.Length)
        {
            return ClanOperationResult.Failure("InvalidMembers");
        }

        if (await database.ClanMemberships.AsNoTracking().AnyAsync(
                x => members.Contains(x.PlayerProfileId) && !x.EndedAtUtc.HasValue,
                cancellationToken))
        {
            return ClanOperationResult.Failure("MemberAlreadyInClan");
        }

        var normalizedName = name?.Trim() ?? string.Empty;
        var normalizedTag = tag?.Trim().ToUpperInvariant() ?? string.Empty;
        if (await database.Clans.AsNoTracking().AnyAsync(
                x => x.Name.ToLower() == normalizedName.ToLower() || x.Tag == normalizedTag,
                cancellationToken))
        {
            return ClanOperationResult.Failure("DuplicateClan");
        }

        try
        {
            var clanId = Guid.NewGuid();
            var createdAtUtc = DateTimeOffset.UtcNow;
            database.Clans.Add(new Clan(clanId, normalizedName, normalizedTag, createdByPlayerProfileId, createdAtUtc));
            database.ClanMemberships.AddRange(members.Select(playerId => new ClanMembership(
                Guid.NewGuid(),
                clanId,
                playerId,
                playerId == createdByPlayerProfileId ? ClanRole.Leader : ClanRole.Member,
                createdAtUtc)));
            await database.SaveChangesAsync(cancellationToken);
            return ClanOperationResult.Success(clanId);
        }
        catch (DomainRuleException)
        {
            return ClanOperationResult.Failure("InvalidClan");
        }
        catch (DbUpdateException)
        {
            return ClanOperationResult.Failure("DuplicateOrConflictingClan");
        }
    }
}
