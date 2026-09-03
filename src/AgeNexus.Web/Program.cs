using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using AgeNexus.Infrastructure;
using AgeNexus.Application;
using AgeNexus.Infrastructure.Persistence;
using AgeNexus.Web.Components;
using AgeNexus.Web.Identity;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole(options => options.TimestampFormat = "HH:mm:ss ");

builder.Services.AddAgeNexusApplication();
builder.Services.AddAgeNexusInfrastructure(builder.Configuration);
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<AuthenticationStateProvider, HttpContextAuthenticationStateProvider>();
builder.Services.AddCascadingAuthenticationState();

if (builder.Environment.IsDevelopment())
{
    var keyDirectory = new DirectoryInfo(Path.Combine(builder.Environment.ContentRootPath, ".keys"));
    builder.Services
        .AddDataProtection()
        .SetApplicationName("AgeNexus")
        .PersistKeysToFileSystem(keyDirectory);
}

var app = builder.Build();

if (args.Contains("--finalize-latest-match", StringComparer.OrdinalIgnoreCase))
{
    await using var scope = app.Services.CreateAsyncScope();
    var database = scope.ServiceProvider.GetRequiredService<AgeNexusDbContext>();
    var reportId = await database.MatchStatisticsReports.AsNoTracking()
        .OrderByDescending(x => x.CreatedAtUtc)
        .Select(x => (Guid?)x.Id)
        .FirstOrDefaultAsync();
    if (!reportId.HasValue)
    {
        Console.WriteLine("Nenhum relatório encontrado.");
        return;
    }

    var performance = scope.ServiceProvider
        .GetRequiredService<AgeNexus.Application.MatchPerformance.IPerformanceStatisticsService>();
    var result = await performance.FinalizeAsync(reportId.Value);
    Console.WriteLine(result.Succeeded
        ? $"Relatório {reportId} finalizado com sucesso."
        : $"Falha ao finalizar relatório {reportId}: {result.ErrorCode}.");
    return;
}

if (args.Contains("--diagnose-latest-match", StringComparer.OrdinalIgnoreCase))
{
    await using var scope = app.Services.CreateAsyncScope();
    var database = scope.ServiceProvider.GetRequiredService<AgeNexusDbContext>();
    var match = await database.Matches.AsNoTracking()
        .OrderByDescending(x => x.PlayedAtUtc)
        .FirstOrDefaultAsync();
    if (match is null)
    {
        Console.WriteLine("Nenhuma partida encontrada.");
        return;
    }

    var report = await database.MatchStatisticsReports.AsNoTracking()
        .SingleOrDefaultAsync(x => x.MatchId == match.Id);
    var statisticCount = report is null
        ? 0
        : await database.PlayerMatchStatistics.AsNoTracking().CountAsync(x => x.ReportId == report.Id);
    var scoreCount = report is null
        ? 0
        : await database.PlayerPerformanceScores.AsNoTracking().CountAsync(x => x.ReportId == report.Id);
    Console.WriteLine(
        $"Partida={match.Id}; MatchStatus={match.Status}; Report={report?.Id}; " +
        $"ReportStatus={report?.Status}; Estatísticas={statisticCount}; Scores={scoreCount}");
    return;
}

if (args.Contains("--sync-aoe2-catalog", StringComparer.OrdinalIgnoreCase))
{
    await using var scope = app.Services.CreateAsyncScope();
    var catalog = scope.ServiceProvider.GetRequiredService<AgeNexus.Application.GameCatalog.ICatalogSetupService>();
    var result = await catalog.SyncAge2DefinitiveEditionAsync();
    Console.WriteLine(result.Succeeded
        ? $"Catálogo AoE II: DE sincronizado: {result.TotalCivilizations} civilizações e {result.TotalMaps} mapas."
        : $"Falha ao sincronizar catálogo AoE II: DE: {result.ErrorCode}.");
    return;
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapGet("/health/database", async (AgeNexusDbContext database, CancellationToken cancellationToken) =>
    await database.Database.CanConnectAsync(cancellationToken)
        ? Results.Ok(new { status = "healthy", database = "postgresql" })
        : Results.StatusCode(StatusCodes.Status503ServiceUnavailable));
app.MapAgeNexusAccountEndpoints();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

app.Run();

public partial class Program;
