using AgeNexus.Application.Queries;
using AgeNexus.Application.Evidence;
using AgeNexus.Domain.Clans;
using AgeNexus.Domain.Common;
using AgeNexus.Infrastructure.Identity;
using AgeNexus.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Security.Cryptography;

namespace AgeNexus.Infrastructure.Clans;

public sealed class ClanService(AgeNexusDbContext database, AccountService accounts, IEvidenceObjectStorage storage) : IClanService
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

    public async Task<ClanOperationResult> UpdateAsync(ClaimsPrincipal principal, Guid clanId,
        string name, string tag, string? description, string? motto, bool isRecruiting,
        CancellationToken cancellationToken = default)
    {
        if (!await CanManageAsync(principal, clanId, cancellationToken)) return ClanOperationResult.Failure("NotAuthorized");
        var clan = await database.Clans.SingleOrDefaultAsync(x => x.Id == clanId, cancellationToken);
        if (clan is null) return ClanOperationResult.Failure("ClanNotFound");
        var normalizedName = name?.Trim() ?? "";
        var normalizedTag = tag?.Trim().ToUpperInvariant() ?? "";
        if (await database.Clans.AsNoTracking().AnyAsync(x => x.Id != clanId &&
            (x.Name.ToLower() == normalizedName.ToLower() || x.Tag == normalizedTag), cancellationToken))
            return ClanOperationResult.Failure("DuplicateClan");
        try { clan.Update(normalizedName, normalizedTag, description, motto, isRecruiting); await database.SaveChangesAsync(cancellationToken); return ClanOperationResult.Success(clanId); }
        catch (DomainRuleException) { return ClanOperationResult.Failure("InvalidClan"); }
        catch (DbUpdateException) { return ClanOperationResult.Failure("DuplicateClan"); }
    }

    public async Task<ClanOperationResult> UploadImageAsync(ClaimsPrincipal principal, Guid clanId,
        string fileName, byte[] content, CancellationToken cancellationToken = default)
    {
        if (!await CanManageAsync(principal, clanId, cancellationToken)) return ClanOperationResult.Failure("NotAuthorized");
        if (!TryIdentifyImage(fileName, content, out var extension, out var type)) return ClanOperationResult.Failure("InvalidImage");
        var clan = await database.Clans.SingleOrDefaultAsync(x => x.Id == clanId, cancellationToken);
        if (clan is null) return ClanOperationResult.Failure("ClanNotFound");
        if (!storage.IsConfigured)
        {
            return await SaveDatabaseEmblemAsync(clan, type, content, cancellationToken);
        }
        var oldKey = clan.ImageUrl is null ? null : storage.TryGetObjectKey(clan.ImageUrl);
        var key = $"clans/{clanId:N}/emblem-{Guid.NewGuid():N}.{extension}";
        try
        {
            await storage.UploadAsync(key, type, content, cancellationToken);
            clan.UpdateImage(storage.GetPublicUrl(key));
            var databaseEmblem = await database.ClanEmblems.SingleOrDefaultAsync(
                x => x.ClanId == clanId, cancellationToken);
            if (databaseEmblem is not null) database.ClanEmblems.Remove(databaseEmblem);
            await database.SaveChangesAsync(cancellationToken);
            if (oldKey is not null && oldKey.StartsWith($"clans/{clanId:N}/", StringComparison.Ordinal))
                try { await storage.DeleteAsync(oldKey, CancellationToken.None); } catch (HttpRequestException) { }
            return ClanOperationResult.Success(clanId);
        }
        catch (Exception ex) when (ex is HttpRequestException or DbUpdateException)
        {
            try { await storage.DeleteAsync(key, CancellationToken.None); } catch { }
            return ClanOperationResult.Failure("ImageUploadFailed");
        }
    }

    public async Task<ClanOperationResult> AddMemberAsync(ClaimsPrincipal principal, Guid clanId, Guid playerProfileId,
        CancellationToken cancellationToken = default)
    {
        if (!await CanManageAsync(principal, clanId, cancellationToken)) return ClanOperationResult.Failure("NotAuthorized");
        if (await database.ClanMemberships.CountAsync(x => x.ClanId == clanId && !x.EndedAtUtc.HasValue, cancellationToken) >= 8)
            return ClanOperationResult.Failure("ClanFull");
        if (!await database.PlayerProfiles.AnyAsync(x => x.Id == playerProfileId, cancellationToken) ||
            await database.ClanMemberships.AnyAsync(x => x.PlayerProfileId == playerProfileId && !x.EndedAtUtc.HasValue, cancellationToken))
            return ClanOperationResult.Failure("MemberAlreadyInClan");
        database.ClanMemberships.Add(new ClanMembership(Guid.NewGuid(), clanId, playerProfileId, ClanRole.Member, DateTimeOffset.UtcNow));
        await database.SaveChangesAsync(cancellationToken); return ClanOperationResult.Success(clanId);
    }

    public async Task<ClanOperationResult> RemoveMemberAsync(ClaimsPrincipal principal, Guid clanId, Guid playerProfileId,
        CancellationToken cancellationToken = default)
    {
        if (!await CanManageAsync(principal, clanId, cancellationToken)) return ClanOperationResult.Failure("NotAuthorized");
        var clan = await database.Clans.AsNoTracking().SingleOrDefaultAsync(x => x.Id == clanId, cancellationToken);
        if (clan is null || clan.CreatedByPlayerProfileId == playerProfileId) return ClanOperationResult.Failure("CannotRemoveLeader");
        var membership = await database.ClanMemberships.SingleOrDefaultAsync(x => x.ClanId == clanId && x.PlayerProfileId == playerProfileId && !x.EndedAtUtc.HasValue, cancellationToken);
        if (membership is null) return ClanOperationResult.Failure("MemberNotFound");
        membership.End(DateTimeOffset.UtcNow); await database.SaveChangesAsync(cancellationToken); return ClanOperationResult.Success(clanId);
    }

    public async Task<ClanOperationResult> ChangeRoleAsync(ClaimsPrincipal principal, Guid clanId, Guid playerProfileId,
        ClanRole role, CancellationToken cancellationToken = default)
    {
        if (!await CanManageAsync(principal, clanId, cancellationToken)) return ClanOperationResult.Failure("NotAuthorized");
        var clan = await database.Clans.AsNoTracking().SingleOrDefaultAsync(x => x.Id == clanId, cancellationToken);
        if (clan is null || clan.CreatedByPlayerProfileId == playerProfileId || role == ClanRole.Leader)
            return ClanOperationResult.Failure("InvalidRole");
        var membership = await database.ClanMemberships.SingleOrDefaultAsync(x => x.ClanId == clanId && x.PlayerProfileId == playerProfileId && !x.EndedAtUtc.HasValue, cancellationToken);
        if (membership is null) return ClanOperationResult.Failure("MemberNotFound");
        membership.ChangeRole(role); await database.SaveChangesAsync(cancellationToken); return ClanOperationResult.Success(clanId);
    }

    public async Task<ClanOperationResult> UpdateOwnMembershipAsync(ClaimsPrincipal principal, Guid clanId,
        string? title, string? note, CancellationToken cancellationToken = default)
    {
        var profile = await accounts.GetProfileAsync(principal, cancellationToken);
        if (profile is null) return ClanOperationResult.Failure("NotAuthorized");
        var membership = await database.ClanMemberships.SingleOrDefaultAsync(x => x.ClanId == clanId && x.PlayerProfileId == profile.Id && !x.EndedAtUtc.HasValue, cancellationToken);
        if (membership is null) return ClanOperationResult.Failure("MemberNotFound");
        try { membership.UpdateMemberProfile(title, note); await database.SaveChangesAsync(cancellationToken); return ClanOperationResult.Success(clanId); }
        catch (DomainRuleException) { return ClanOperationResult.Failure("InvalidMemberProfile"); }
    }

    public async Task<ClanOperationResult> DeleteAsync(ClaimsPrincipal principal, Guid clanId, CancellationToken cancellationToken = default)
    {
        if (!await CanManageAsync(principal, clanId, cancellationToken)) return ClanOperationResult.Failure("NotAuthorized");
        var clan = await database.Clans.SingleOrDefaultAsync(x => x.Id == clanId, cancellationToken);
        if (clan is null) return ClanOperationResult.Failure("ClanNotFound");
        var key = clan.ImageUrl is null ? null : storage.TryGetObjectKey(clan.ImageUrl);
        database.RatingEvents.RemoveRange(await database.RatingEvents.Where(x =>
            x.BeneficiaryId == clanId && x.Scope == AgeNexus.Domain.Competition.RatingScopeKind.ClanCompetitive)
            .ToArrayAsync(cancellationToken));
        database.PointEvents.RemoveRange(await database.PointEvents.Where(x =>
            x.BeneficiaryId == clanId &&
            (x.Scope == AgeNexus.Domain.Competition.PointScopeKind.ClanPrestige ||
             x.Scope == AgeNexus.Domain.Competition.PointScopeKind.ClanPve))
            .ToArrayAsync(cancellationToken));
        database.Clans.Remove(clan); await database.SaveChangesAsync(cancellationToken);
        if (key is not null && storage.IsConfigured) try { await storage.DeleteAsync(key, CancellationToken.None); } catch { }
        return ClanOperationResult.Success(clanId);
    }

    private async Task<bool> CanManageAsync(ClaimsPrincipal principal, Guid clanId, CancellationToken token)
    {
        if (await accounts.IsAdministratorAsync(principal, token)) return true;
        var profile = await accounts.GetProfileAsync(principal, token);
        return profile is not null && await database.Clans.AsNoTracking().AnyAsync(x => x.Id == clanId && x.CreatedByPlayerProfileId == profile.Id, token);
    }

    private static bool TryIdentifyImage(string fileName, byte[] content, out string extension, out string type)
    {
        extension = ""; type = "";
        if (content.Length == 0 || content.Length > 2 * 1024 * 1024) return false;
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        if (content.Length >= 8 && ext == ".png" && content.AsSpan(0, 8).SequenceEqual(new byte[] { 0x89,0x50,0x4e,0x47,0x0d,0x0a,0x1a,0x0a })) { extension="png"; type="image/png"; return true; }
        if (content.Length >= 3 && ext is ".jpg" or ".jpeg" && content[0]==0xff && content[1]==0xd8 && content[2]==0xff) { extension="jpg"; type="image/jpeg"; return true; }
        if (content.Length >= 12 && ext == ".webp" && content.AsSpan(0,4).SequenceEqual("RIFF"u8) && content.AsSpan(8,4).SequenceEqual("WEBP"u8)) { extension="webp"; type="image/webp"; return true; }
        return false;
    }

    private async Task<ClanOperationResult> SaveDatabaseEmblemAsync(
        Clan clan, string contentType, byte[] content, CancellationToken cancellationToken)
    {
        try
        {
            var hash = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
            var emblem = await database.ClanEmblems.SingleOrDefaultAsync(
                x => x.ClanId == clan.Id, cancellationToken);
            if (emblem is null)
            {
                database.ClanEmblems.Add(new ClanEmblem(
                    clan.Id, contentType, content, hash, DateTimeOffset.UtcNow));
            }
            else
            {
                emblem.Replace(contentType, content, hash, DateTimeOffset.UtcNow);
            }

            clan.UpdateImage($"/clan-images/{clan.Id:D}?v={hash[..12]}");
            await database.SaveChangesAsync(cancellationToken);
            return ClanOperationResult.Success(clan.Id);
        }
        catch (Exception exception) when (exception is DomainRuleException or DbUpdateException)
        {
            return ClanOperationResult.Failure("ImageUploadFailed");
        }
    }
}
