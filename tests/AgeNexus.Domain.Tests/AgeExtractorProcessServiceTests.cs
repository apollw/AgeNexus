using AgeNexus.Application.MatchPerformance;
using AgeNexus.Infrastructure.MatchPerformance;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgeNexus.Domain.Tests;

public sealed class AgeExtractorProcessServiceTests
{
    private static readonly AgeExtractorPoint[] Corners =
    [
        new(10, 10), new(900, 10), new(900, 600), new(10, 600)
    ];

    [Fact]
    public async Task Rejects_missing_or_forged_images_before_starting_python()
    {
        using var service = CreateService();
        var missing = await service.ExtractCategoryAsync(2, new("desconhecida", [], Corners));
        Assert.Equal("InvalidRequest", missing.ErrorCode);

        var invalid = await service.ExtractCategoryAsync(
            2, new("placar", "not an image"u8.ToArray(), Corners));
        Assert.Equal("InvalidImage", invalid.ErrorCode);
    }

    [Fact]
    public async Task Accepts_a_supported_image_signature_before_checking_runtime()
    {
        using var service = CreateService();
        byte[] png = [0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a];
        var result = await service.ExtractCategoryAsync(2, new("placar", png, Corners));

        Assert.Equal("Unavailable", result.ErrorCode);
    }

    [Fact]
    public async Task Rejects_an_invalid_table_region_before_starting_python()
    {
        using var service = CreateService();
        byte[] png = [0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a];
        var invalidCorners = new[] { new AgeExtractorPoint(10, 10) };
        var result = await service.ExtractCategoryAsync(2, new("placar", png, invalidCorners));

        Assert.Equal("InvalidImage", result.ErrorCode);
    }

    [Fact]
    public void Parses_structured_progress_and_ignores_unstructured_stderr()
    {
        var diagnostic = AgeExtractorProcessService.ParseDiagnostic(
            "{\"tipo\":\"progresso\",\"concluidas\":12,\"total\":60,\"percentual\":20.0," +
            "\"mensagem\":\"Tecnologia | jogador 2/4 | pesquisas\"}");

        Assert.NotNull(diagnostic);
        Assert.Equal("progresso", diagnostic.Type);
        Assert.Equal(12, diagnostic.Completed);
        Assert.Equal(20.0m, diagnostic.Percentage);
        Assert.Null(AgeExtractorProcessService.ParseDiagnostic("tesseract warning"));
    }

    private static AgeExtractorProcessService CreateService()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AgeExtractor:PythonExecutable"] = "/path/that/does/not/exist",
            ["AgeExtractor:WorkingDirectory"] = "/path/that/does/not/exist"
        }).Build();
        return new(configuration, NullLogger<AgeExtractorProcessService>.Instance);
    }
}
