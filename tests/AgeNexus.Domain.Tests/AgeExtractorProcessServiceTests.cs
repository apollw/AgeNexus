using AgeNexus.Application.MatchPerformance;
using AgeNexus.Infrastructure.MatchPerformance;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgeNexus.Domain.Tests;

public sealed class AgeExtractorProcessServiceTests
{
    private static readonly string[] Categories = ["placar", "militar", "economia", "tecnologia", "sociedade"];
    private static readonly AgeExtractorPoint[] Corners =
    [
        new(10, 10), new(900, 10), new(900, 600), new(10, 600)
    ];

    [Fact]
    public async Task Rejects_missing_or_forged_images_before_starting_python()
    {
        using var service = CreateService();
        var missing = await service.ExtractAsync(2, []);
        Assert.Equal("InvalidRequest", missing.ErrorCode);

        var forged = Categories.Select(x => new AgeExtractorImage(x, "not an image"u8.ToArray(), Corners)).ToArray();
        var invalid = await service.ExtractAsync(2, forged);
        Assert.Equal("InvalidImage", invalid.ErrorCode);
    }

    [Fact]
    public async Task Accepts_five_supported_image_signatures_before_checking_runtime()
    {
        using var service = CreateService();
        byte[] png = [0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a];
        var images = Categories.Select(x => new AgeExtractorImage(x, png, Corners)).ToArray();

        var result = await service.ExtractAsync(2, images);

        Assert.Equal("Unavailable", result.ErrorCode);
    }

    [Fact]
    public async Task Rejects_an_invalid_table_region_before_starting_python()
    {
        using var service = CreateService();
        byte[] png = [0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a];
        var invalidCorners = new[] { new AgeExtractorPoint(10, 10) };
        var images = Categories.Select(x => new AgeExtractorImage(x, png, invalidCorners)).ToArray();

        var result = await service.ExtractAsync(2, images);

        Assert.Equal("InvalidImage", result.ErrorCode);
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
