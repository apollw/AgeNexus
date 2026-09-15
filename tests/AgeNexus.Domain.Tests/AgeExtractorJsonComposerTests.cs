using System.Text.Json;
using AgeNexus.Application.MatchPerformance;

namespace AgeNexus.Domain.Tests;

public sealed class AgeExtractorJsonComposerTests
{
    private static readonly string[] Categories = ["placar", "militar", "economia", "tecnologia", "sociedade"];

    [Fact]
    public void Combines_five_category_fragments_by_player()
    {
        var fragments = Categories.Select(category => $$"""
            {
              "quantidade_jogadores": 2,
              "categoria": "{{category}}",
              "jogadores": [
                { "jogador": 1, "{{category}}": { "valor": 10 } },
                { "jogador": 2, "{{category}}": { "valor": 20 } }
              ]
            }
            """).ToArray();

        var result = new AgeExtractorJsonComposer().Compose(fragments);

        Assert.True(result.Succeeded);
        using var document = JsonDocument.Parse(result.Json!);
        var players = document.RootElement.GetProperty("jogadores");
        Assert.Equal(2, players.GetArrayLength());
        Assert.Equal(10, players[0].GetProperty("placar").GetProperty("valor").GetInt32());
        Assert.Equal(20, players[1].GetProperty("sociedade").GetProperty("valor").GetInt32());
        Assert.False(document.RootElement.TryGetProperty("categoria", out _));
    }

    [Fact]
    public void Rejects_missing_or_duplicate_categories()
    {
        var duplicate = """
            { "quantidade_jogadores": 2, "categoria": "placar", "jogadores": [
              { "jogador": 1, "placar": {} }, { "jogador": 2, "placar": {} }
            ] }
            """;

        var result = new AgeExtractorJsonComposer().Compose(Enumerable.Repeat(duplicate, 5).ToArray());

        Assert.False(result.Succeeded);
        Assert.Contains("repetida", result.Error ?? string.Empty);
    }
}
