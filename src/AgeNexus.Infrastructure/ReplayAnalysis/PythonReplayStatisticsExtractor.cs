using System.Diagnostics;
using System.Text.Json;
using AgeNexus.Application.MatchPerformance;
using AgeNexus.Domain.MatchPerformance;
using Microsoft.Extensions.Configuration;

namespace AgeNexus.Infrastructure.ReplayAnalysis;

public sealed class PythonReplayStatisticsExtractor(IConfiguration configuration) : IReplayStatisticsExtractor
{
    public const int MaximumReplayBytes = 50 * 1024 * 1024;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public async Task<ReplayExtractionResult> ExtractAsync(
        string fileName,
        byte[] content,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(fileName) || !HasSupportedReplayExtension(fileName) ||
            content.Length is 0 or > MaximumReplayBytes)
        {
            return Failure("InvalidReplayFile");
        }

        var scriptPath = configuration["ReplayExtractor:ScriptPath"];
        if (string.IsNullOrWhiteSpace(scriptPath))
        {
            scriptPath = Path.Combine(AppContext.BaseDirectory, "ReplayAnalysis", "extract_replay.py");
        }

        if (!File.Exists(scriptPath))
        {
            return Failure("ReplayExtractorUnavailable");
        }

        var temporaryPath = Path.Combine(Path.GetTempPath(), $"agenexus-replay-{Guid.NewGuid():N}.aoe2record");
        try
        {
            await File.WriteAllBytesAsync(temporaryPath, content, cancellationToken);
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = configuration["ReplayExtractor:PythonExecutable"] ?? "python3",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };
            process.StartInfo.ArgumentList.Add(scriptPath);
            process.StartInfo.ArgumentList.Add(temporaryPath);
            try
            {
                if (!process.Start())
                {
                    return Failure("ReplayExtractorUnavailable");
                }
            }
            catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or InvalidOperationException)
            {
                return Failure("ReplayExtractorUnavailable");
            }

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(45));
            try
            {
                await process.WaitForExitAsync(timeout.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                process.Kill(entireProcessTree: true);
                return Failure("ReplayExtractionTimedOut");
            }

            var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(output))
            {
                return Failure("ReplayParseFailed");
            }

            var payload = JsonSerializer.Deserialize<ExtractorPayload>(output, JsonOptions);
            if (payload is null || !payload.Succeeded)
            {
                return Failure(payload?.ErrorCode ?? "ReplayParseFailed", payload?.Warnings);
            }

            return new ReplayExtractionResult(
                true,
                payload.ExtractorVersion,
                payload.CoverageDetails ?? "{}",
                payload.Players.Select(x => new ReplayPlayerStatistics(
                    x.Name,
                    x.IsHuman,
                    x.TeamNumber,
                    x.Values ?? new MatchStatisticValues())).ToArray(),
                Warnings: payload.Warnings ?? []);
        }
        catch (JsonException)
        {
            return Failure("ReplayExtractorInvalidOutput");
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private static ReplayExtractionResult Failure(string code, IReadOnlyCollection<string>? warnings = null) =>
        new(false, null, "{}", [], code, warnings ?? []);

    private static bool HasSupportedReplayExtension(string fileName) =>
        fileName.EndsWith(".aoe2record", StringComparison.OrdinalIgnoreCase) ||
        fileName.EndsWith(".mgz", StringComparison.OrdinalIgnoreCase) ||
        fileName.EndsWith(".mgx", StringComparison.OrdinalIgnoreCase);

    private sealed record ExtractorPayload(
        bool Succeeded,
        string? ExtractorVersion,
        string? CoverageDetails,
        IReadOnlyCollection<ExtractorPlayer> Players,
        string? ErrorCode,
        IReadOnlyCollection<string>? Warnings);

    private sealed record ExtractorPlayer(
        string Name,
        bool IsHuman,
        int TeamNumber,
        MatchStatisticValues? Values);
}
