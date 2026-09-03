namespace AgeNexus.Application.GameCatalog;

public interface ICatalogSetupService
{
    Task<CatalogSetupResult> InitializeAsync(
        string gameName,
        string editionName,
        CancellationToken cancellationToken = default);

    Task<CatalogSyncResult> SyncAge2DefinitiveEditionAsync(
        Guid? gameEditionId = null,
        CancellationToken cancellationToken = default);
}

public sealed record CatalogSyncResult(
    bool Succeeded,
    Guid? GameEditionId,
    int CivilizationsAdded,
    int MapsAdded,
    int TotalCivilizations,
    int TotalMaps,
    string? ErrorCode)
{
    public static CatalogSyncResult Success(
        Guid gameEditionId,
        int civilizationsAdded,
        int mapsAdded,
        int totalCivilizations,
        int totalMaps) =>
        new(true, gameEditionId, civilizationsAdded, mapsAdded, totalCivilizations, totalMaps, null);

    public static CatalogSyncResult Failure(string errorCode) =>
        new(false, null, 0, 0, 0, 0, errorCode);
}

public sealed record CatalogSetupResult(bool Succeeded, Guid? GameEditionId, string? ErrorCode)
{
    public static CatalogSetupResult Success(Guid gameEditionId) => new(true, gameEditionId, null);

    public static CatalogSetupResult Failure(string errorCode) => new(false, null, errorCode);
}
