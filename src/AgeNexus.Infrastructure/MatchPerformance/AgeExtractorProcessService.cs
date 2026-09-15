using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using AgeNexus.Application.MatchPerformance;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AgeNexus.Infrastructure.MatchPerformance;

public sealed class AgeExtractorProcessService(
    IConfiguration configuration,
    ILogger<AgeExtractorProcessService> logger) : IAgeExtractorService, IDisposable
{
    private const int MaximumImageBytes = 5 * 1024 * 1024;
    private const int MaximumJsonCharacters = 1024 * 1024;
    private static readonly string[] Categories = ["placar", "militar", "economia", "tecnologia", "sociedade"];
    private readonly SemaphoreSlim gate = new(1, 1);

    public async Task<AgeExtractorExecutionResult> ExtractCategoryAsync(
        int playerCount,
        AgeExtractorImage image,
        IProgress<AgeExtractorProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (playerCount is < 2 or > 8 || !Categories.Contains(image.Category, StringComparer.Ordinal))
        {
            return AgeExtractorExecutionResult.Failure("InvalidRequest");
        }

        if (!IsSupportedImage(image.Content) || !IsValidRegion(image.TableCorners))
        {
            return AgeExtractorExecutionResult.Failure("InvalidImage");
        }

        if (!await gate.WaitAsync(TimeSpan.FromSeconds(3), cancellationToken))
        {
            return AgeExtractorExecutionResult.Failure("Busy");
        }

        var temporaryDirectory = Path.Combine(Path.GetTempPath(), $"agenexus-ocr-{Guid.NewGuid():N}");
        Process? process = null;
        try
        {
            var python = configuration["AgeExtractor:PythonExecutable"] ?? "/opt/agextractor-venv/bin/python";
            var workingDirectory = configuration["AgeExtractor:WorkingDirectory"] ?? "/opt/agextractor";
            if (!File.Exists(python) || !Directory.Exists(workingDirectory))
            {
                logger.LogError("AgeXtractor runtime is unavailable.");
                return AgeExtractorExecutionResult.Failure("Unavailable");
            }

            Directory.CreateDirectory(temporaryDirectory);
            await File.WriteAllBytesAsync(
                Path.Combine(temporaryDirectory, $"{image.Category}.jpeg"),
                image.Content,
                cancellationToken);
            var regionsPath = Path.Combine(temporaryDirectory, "regioes.json");
            var regions = new Dictionary<string, double[][]>(StringComparer.Ordinal)
            {
                [image.Category] = image.TableCorners.Select(point => new[] { point.X, point.Y }).ToArray()
            };
            await File.WriteAllTextAsync(
                regionsPath,
                JsonSerializer.Serialize(regions),
                cancellationToken);

            var startInfo = new ProcessStartInfo
            {
                FileName = python,
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            startInfo.ArgumentList.Add("-m");
            startInfo.ArgumentList.Add("agextractor.interfaces.server");
            startInfo.ArgumentList.Add("--entrada");
            startInfo.ArgumentList.Add(temporaryDirectory);
            startInfo.ArgumentList.Add("--jogadores");
            startInfo.ArgumentList.Add(playerCount.ToString(System.Globalization.CultureInfo.InvariantCulture));
            startInfo.ArgumentList.Add("--categoria");
            startInfo.ArgumentList.Add(image.Category);
            startInfo.ArgumentList.Add("--regioes");
            startInfo.ArgumentList.Add(regionsPath);
            startInfo.Environment["PYTHONPATH"] = workingDirectory;
            startInfo.Environment["OPENCV_IO_MAX_IMAGE_PIXELS"] = "40000000";

            process = new Process { StartInfo = startInfo };
            if (!process.Start())
            {
                return AgeExtractorExecutionResult.Failure("Failed");
            }

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromMinutes(5));
            AgeExtractorProgress? lastProgress = null;
            var stdoutTask = process.StandardOutput.ReadToEndAsync(timeout.Token);
            var diagnosticsTask = ReadDiagnosticsAsync(
                process.StandardError,
                value =>
                {
                    lastProgress = value;
                    progress?.Report(value);
                },
                timeout.Token);
            try
            {
                await process.WaitForExitAsync(timeout.Token);
                var output = await stdoutTask;
                var diagnosticError = await diagnosticsTask;
                if (process.ExitCode != 0)
                {
                    logger.LogWarning("AgeXtractor exited with code {ExitCode}.", process.ExitCode);
                    var detail = diagnosticError ?? (lastProgress is null
                        ? $"O processo foi encerrado pelo servidor com o código {process.ExitCode}."
                        : $"{lastProgress.Message} → o processo foi encerrado pelo servidor com o código {process.ExitCode}.");
                    return AgeExtractorExecutionResult.Failure(
                        "Failed",
                        SanitizeDiagnostic(detail, temporaryDirectory));
                }

                if (string.IsNullOrWhiteSpace(output) || output.Length > MaximumJsonCharacters)
                {
                    return AgeExtractorExecutionResult.Failure("InvalidOutput");
                }

                using var document = JsonDocument.Parse(output);
                if (document.RootElement.ValueKind != JsonValueKind.Object)
                {
                    return AgeExtractorExecutionResult.Failure("InvalidOutput");
                }

                return AgeExtractorExecutionResult.Success(output);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                Kill(process);
                logger.LogWarning("AgeXtractor exceeded the five-minute processing limit.");
                var detail = lastProgress is null
                    ? "Nenhuma etapa foi concluída antes do limite de cinco minutos."
                    : $"Última etapa: {lastProgress.Message}.";
                return AgeExtractorExecutionResult.Failure("Timeout", detail);
            }
        }
        catch (OperationCanceledException)
        {
            Kill(process);
            throw;
        }
        catch (Exception exception)
        {
            Kill(process);
            logger.LogError(exception, "AgeXtractor processing failed.");
            return AgeExtractorExecutionResult.Failure("Failed");
        }
        finally
        {
            process?.Dispose();
            try
            {
                if (Directory.Exists(temporaryDirectory))
                {
                    Directory.Delete(temporaryDirectory, recursive: true);
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                logger.LogWarning(exception, "Could not remove an AgeXtractor temporary directory.");
            }
            gate.Release();
        }
    }

    private static bool IsSupportedImage(byte[] content)
    {
        if (content.Length is 0 or > MaximumImageBytes)
        {
            return false;
        }

        var jpeg = content.Length >= 3 && content[0] == 0xff && content[1] == 0xd8 && content[2] == 0xff;
        var png = content.Length >= 8 && content.AsSpan(0, 8).SequenceEqual(new byte[] { 0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a });
        var webp = content.Length >= 12 && content.AsSpan(0, 4).SequenceEqual("RIFF"u8) &&
                   content.AsSpan(8, 4).SequenceEqual("WEBP"u8);
        return jpeg || png || webp;
    }

    private static bool IsValidRegion(IReadOnlyCollection<AgeExtractorPoint> corners) =>
        corners.Count == 4 && corners.All(point =>
            double.IsFinite(point.X) && double.IsFinite(point.Y) &&
            point.X >= 0 && point.Y >= 0 && point.X <= 100_000 && point.Y <= 100_000);

    private static async Task<string?> ReadDiagnosticsAsync(
        StreamReader reader,
        Action<AgeExtractorProgress> reportProgress,
        CancellationToken cancellationToken)
    {
        string? error = null;
        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            var diagnostic = ParseDiagnostic(line);
            if (diagnostic?.Type == "progresso" &&
                diagnostic.Completed is >= 0 && diagnostic.Total is > 0 &&
                diagnostic.Percentage is >= 0 and <= 100 &&
                !string.IsNullOrWhiteSpace(diagnostic.Message))
            {
                reportProgress(new(
                    diagnostic.Completed.Value,
                    diagnostic.Total.Value,
                    diagnostic.Percentage.Value,
                    SanitizeDiagnostic(diagnostic.Message, null)));
            }
            else if (diagnostic?.Type == "erro" && !string.IsNullOrWhiteSpace(diagnostic.Message))
            {
                error = SanitizeDiagnostic(diagnostic.Message, null);
            }
        }

        return error;
    }

    internal static AgeExtractorDiagnostic? ParseDiagnostic(string line)
    {
        if (string.IsNullOrWhiteSpace(line) || line.Length > 4096)
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<AgeExtractorDiagnostic>(line);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string SanitizeDiagnostic(string value, string? temporaryDirectory)
    {
        var sanitized = value.Replace('\r', ' ').Replace('\n', ' ').Trim();
        if (!string.IsNullOrWhiteSpace(temporaryDirectory))
        {
            sanitized = sanitized.Replace(temporaryDirectory, "arquivo temporário", StringComparison.Ordinal);
        }

        return sanitized.Length <= 600 ? sanitized : sanitized[..600] + "…";
    }

    internal sealed record AgeExtractorDiagnostic(
        [property: JsonPropertyName("tipo")] string? Type,
        [property: JsonPropertyName("concluidas")] int? Completed,
        [property: JsonPropertyName("total")] int? Total,
        [property: JsonPropertyName("percentual")] decimal? Percentage,
        [property: JsonPropertyName("mensagem")] string? Message,
        [property: JsonPropertyName("etapa")] string? Stage,
        [property: JsonPropertyName("codigo")] string? Code);

    private static void Kill(Process? process)
    {
        if (process is null)
        {
            return;
        }

        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException) { }
    }

    public void Dispose() => gate.Dispose();
}
