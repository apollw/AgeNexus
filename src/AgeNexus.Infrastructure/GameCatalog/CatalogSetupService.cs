using System.Globalization;
using System.Text;
using AgeNexus.Application.GameCatalog;
using AgeNexus.Domain.Common;
using AgeNexus.Domain.GameCatalog;
using AgeNexus.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AgeNexus.Infrastructure.GameCatalog;

public sealed class CatalogSetupService(AgeNexusDbContext database) : ICatalogSetupService
{
    public async Task<CatalogSetupResult> InitializeAsync(
        string gameName,
        string editionName,
        CancellationToken cancellationToken = default)
    {
        gameName = gameName.Trim();
        editionName = editionName.Trim();
        if (gameName.Length is 0 or > 120 || editionName.Length is 0 or > 120)
        {
            return CatalogSetupResult.Failure("InvalidNames");
        }

        var gameSlug = Slugify(gameName);
        var editionSlug = Slugify(editionName);
        if (gameSlug.Length == 0 || editionSlug.Length == 0)
        {
            return CatalogSetupResult.Failure("InvalidNames");
        }

        var strategy = database.Database.CreateExecutionStrategy();
        try
        {
            return await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
                if (await database.GameEditions.AnyAsync(cancellationToken))
                {
                    return CatalogSetupResult.Failure("AlreadyInitialized");
                }

                var game = new Game(Guid.NewGuid(), gameName, gameSlug);
                var edition = new GameEdition(Guid.NewGuid(), game.Id, editionName, editionSlug);
                database.Games.Add(game);
                database.GameEditions.Add(edition);
                await database.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return CatalogSetupResult.Success(edition.Id);
            });
        }
        catch (DomainRuleException)
        {
            return CatalogSetupResult.Failure("InvalidNames");
        }
        catch (DbUpdateException)
        {
            return CatalogSetupResult.Failure("ConcurrentSetup");
        }
    }

    public async Task<CatalogSyncResult> SyncAge2DefinitiveEditionAsync(
        Guid? gameEditionId = null,
        CancellationToken cancellationToken = default)
    {
        var editions = await (
            from edition in database.GameEditions.AsNoTracking()
            join game in database.Games.AsNoTracking() on edition.GameId equals game.Id
            where edition.IsActive
            select new CatalogTarget(edition.Id, game.Name, edition.Name))
            .ToArrayAsync(cancellationToken);

        var target = gameEditionId.HasValue
            ? editions.SingleOrDefault(x => x.EditionId == gameEditionId.Value)
            : FindAge2Edition(editions);
        if (target is null)
        {
            return CatalogSyncResult.Failure(editions.Length == 0 ? "EditionNotFound" : "EditionIsAmbiguous");
        }

        var existingCivilizations = (await database.Factions
            .Where(x => x.GameEditionId == target.EditionId)
            .Select(x => x.Slug)
            .ToArrayAsync(cancellationToken)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var existingMaps = (await database.MapDefinitions
            .Where(x => x.GameEditionId == target.EditionId)
            .Select(x => x.Slug)
            .ToArrayAsync(cancellationToken)).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var civilizationsToAdd = Age2DefinitiveEditionCatalog.Civilizations
            .Where(x => !existingCivilizations.Contains(x.Slug))
            .Select(x => new Faction(Guid.NewGuid(), target.EditionId, x.Name, x.Slug))
            .ToArray();
        var mapsToAdd = Age2DefinitiveEditionCatalog.Maps
            .Where(x => !existingMaps.Contains(x.Slug))
            .Select(x => new MapDefinition(Guid.NewGuid(), target.EditionId, x.Name, x.Slug))
            .ToArray();

        database.Factions.AddRange(civilizationsToAdd);
        database.MapDefinitions.AddRange(mapsToAdd);
        try
        {
            await database.SaveChangesAsync(cancellationToken);
            return CatalogSyncResult.Success(
                target.EditionId,
                civilizationsToAdd.Length,
                mapsToAdd.Length,
                existingCivilizations.Count + civilizationsToAdd.Length,
                existingMaps.Count + mapsToAdd.Length);
        }
        catch (DbUpdateException)
        {
            return CatalogSyncResult.Failure("CatalogSyncConflict");
        }
    }

    private static CatalogTarget? FindAge2Edition(IReadOnlyCollection<CatalogTarget> editions)
    {
        var age2Edition = editions.FirstOrDefault(x =>
            x.GameName.Contains("Age of Empires", StringComparison.OrdinalIgnoreCase) &&
            (x.GameName.Contains("II", StringComparison.OrdinalIgnoreCase) ||
             x.GameName.Contains('2')) &&
            (x.EditionName.Contains("Definitive", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(x.EditionName.Trim(), "DE", StringComparison.OrdinalIgnoreCase)));
        return age2Edition ?? (editions.Count == 1 ? editions.Single() : null);
    }

    private static string Slugify(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormD);
        var result = new StringBuilder(normalized.Length);
        var pendingSeparator = false;

        foreach (var character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsAsciiLetterOrDigit(character))
            {
                if (pendingSeparator && result.Length > 0)
                {
                    result.Append('-');
                }

                result.Append(char.ToLowerInvariant(character));
                pendingSeparator = false;
            }
            else
            {
                pendingSeparator = true;
            }
        }

        var slug = result.ToString().Trim('-');
        return slug.Length <= 80 ? slug : slug[..80].TrimEnd('-');
    }

    private sealed record CatalogTarget(Guid EditionId, string GameName, string EditionName);
}
