using System.Diagnostics;
using System.Text.Json;
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

    public async Task<AgeExtractorExecutionResult> ExtractAsync(
        int playerCount,
        IReadOnlyCollection<AgeExtractorImage> images,
        CancellationToken cancellationToken = default)
    {
        if (playerCount is < 2 or > 8 || images.Count != Categories.Length)
        {
            return AgeExtractorExecutionResult.Failure("InvalidRequest");
        }

        var byCategory = images.GroupBy(x => x.Category, StringComparer.Ordinal)
            .ToDictionary(x => x.Key, x => x.ToArray(), StringComparer.Ordinal);
        if (Categories.Any(category => !byCategory.TryGetValue(category, out var matches) ||
                                       matches.Length != 1 || !IsSupportedImage(matches[0].Content)))
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
            foreach (var category in Categories)
            {
                await File.WriteAllBytesAsync(
                    Path.Combine(temporaryDirectory, $"{category}.jpeg"),
                    byCategory[category][0].Content,
                    cancellationToken);
            }

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
            startInfo.Environment["PYTHONPATH"] = workingDirectory;
            startInfo.Environment["OPENCV_IO_MAX_IMAGE_PIXELS"] = "40000000";

            process = new Process { StartInfo = startInfo };
            if (!process.Start())
            {
                return AgeExtractorExecutionResult.Failure("Failed");
            }

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromMinutes(5));
            var stdoutTask = process.StandardOutput.ReadToEndAsync(timeout.Token);
            var stderrTask = process.StandardError.ReadToEndAsync(timeout.Token);
            try
            {
                await process.WaitForExitAsync(timeout.Token);
                var output = await stdoutTask;
                _ = await stderrTask;
                if (process.ExitCode != 0)
                {
                    logger.LogWarning("AgeXtractor exited with code {ExitCode}.", process.ExitCode);
                    return AgeExtractorExecutionResult.Failure("Failed");
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
                return AgeExtractorExecutionResult.Failure("Timeout");
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
