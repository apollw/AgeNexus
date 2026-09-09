using AgeNexus.Application.MatchPerformance;
using System.Text.Json;

namespace AgeNexus.Domain.Tests;

public sealed class AgeExtractorJsonImporterTests
{
    private readonly AgeExtractorJsonImporter importer = new();

    [Fact]
    public void Imports_complete_document_and_preserves_player_identity()
    {
        var result = importer.Import(ValidJson(playersReversed: true));

        Assert.Equal(JsonImportStatus.Valid, result.Status);
        Assert.Equal(2, result.PlayerCount);
        var first = Assert.Single(result.Players, x => x.PlayerNumber == 1);
        Assert.Equal(23744, first.Values.TotalScore);
        Assert.Equal(53345, first.Values.WoodCollected);
        Assert.Equal(768, first.Values.FeudalAgeSeconds);
        Assert.False(first.Values.Survived);
    }

    [Fact]
    public void Accepts_a_single_markdown_fence_and_numeric_strings()
    {
        var json = ValidJson().Replace("\"madeira\": 53345", "\"madeira\": \" 53345 \"");
        var result = importer.Import($"```json\n{json}\n```");

        Assert.Equal(JsonImportStatus.Valid, result.Status);
        Assert.Single(result.Normalizations, x => x.Path == "economia.madeira" && x.PlayerNumber == 1);
        Assert.Equal(53345, result.Players.Single(x => x.PlayerNumber == 1).Values.WoodCollected);
    }

    [Fact]
    public void Rejects_incoherent_player_structure()
    {
        var result = importer.Import(ValidJson().Replace("\"jogador\": 2", "\"jogador\": 1"));

        Assert.Equal(JsonImportStatus.Invalid, result.Status);
        Assert.False(result.CanApply);
        Assert.Contains(result.Issues, x => x.Path == "jogadores[].jogador");
    }

    [Fact]
    public void Keeps_valid_values_when_some_ocr_fields_are_invalid()
    {
        var json = ValidJson()
            .Replace("\"idade_feudal\": \"00:12:48\"", "\"idade_feudal\": \"70011311\"", StringComparison.Ordinal)
            .Replace("\"mapa_explorado\": 73", "\"mapa_explorado\": 173", StringComparison.Ordinal)
            .Replace("\"sobreviveu\": false", "\"sobreviveu\": \"false\"", StringComparison.Ordinal);

        var result = importer.Import(json);

        Assert.Equal(JsonImportStatus.Partial, result.Status);
        var first = result.Players.Single(x => x.PlayerNumber == 1);
        Assert.Null(first.Values.FeudalAgeSeconds);
        Assert.Null(first.Values.ExploredPercent);
        Assert.Null(first.Values.Survived);
        Assert.Equal(291, first.Values.UnitsKilled);
        Assert.Contains(result.Issues, x => x.PlayerNumber == 1 && x.Path == "tecnologia.idade_feudal");
    }

    [Fact]
    public void Preserves_zero_and_false_as_real_values()
    {
        var result = importer.Import(ValidJson());
        var first = result.Players.Single(x => x.PlayerNumber == 1);

        Assert.Equal(0, first.Values.BuildingsLost);
        Assert.Equal(0, first.Values.TributeSent);
        Assert.False(first.Values.Survived);
    }

    [Fact]
    public void Does_not_keep_the_original_document_after_import()
    {
        var json = ValidJson();
        json = json.Insert(json.IndexOf('{') + 1, " \"temporary_marker\": \"do-not-retain\",");

        var result = importer.Import(json);

        Assert.Equal(JsonImportStatus.Valid, result.Status);
        Assert.DoesNotContain("do-not-retain", JsonSerializer.Serialize(result));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("[]")]
    public void Rejects_content_that_is_not_a_valid_document(string content)
    {
        Assert.Equal(JsonImportStatus.Invalid, importer.Import(content).Status);
    }

    private static string ValidJson(bool playersReversed = false)
    {
        const string first = """
            {
              "jogador": 1,
              "placar": { "militar": 5974, "economia": 13677, "tecnologia": 3833, "sociedade": 260, "pontuacao_total": 23744 },
              "militar": { "unidades_mortas": 291, "unidades_perdidas": 142, "construcoes_destruidas": 71, "construcoes_perdidas": 0, "unidades_convertidas": 0, "maior_exercito": 94 },
              "economia": { "comida": 49067, "madeira": 53345, "pedra": 5644, "ouro": 30985, "lucro_comercial": 18414, "tributo_enviado": 0, "tributo_recebido": 0 },
              "tecnologia": { "idade_feudal": "00:12:48", "idade_castelos": "00:30:18", "idade_imperial": "00:51:23", "mapa_explorado": 73, "pesquisas": 42, "percentual_pesquisas": 54 },
              "sociedade": { "maravilhas": 0, "castelos": 2, "reliquias_capturadas": 0, "ouro_reliquias": 0, "maximo_aldeoes": 206, "sobreviveu": false }
            }
            """;
        const string second = """
            {
              "jogador": 2,
              "placar": { "militar": 100, "economia": 200, "tecnologia": 300, "sociedade": 400, "pontuacao_total": 1000 },
              "militar": { "unidades_mortas": 1, "unidades_perdidas": 0, "construcoes_destruidas": 0, "construcoes_perdidas": 0, "unidades_convertidas": 0, "maior_exercito": 1 },
              "economia": { "comida": 1, "madeira": 2, "pedra": 3, "ouro": 4, "lucro_comercial": 0, "tributo_enviado": 0, "tributo_recebido": 0 },
              "tecnologia": { "idade_feudal": "0:10:00", "idade_castelos": "0:20:00", "idade_imperial": "0:30:00", "mapa_explorado": 50, "pesquisas": 10, "percentual_pesquisas": 20 },
              "sociedade": { "maravilhas": 0, "castelos": 0, "reliquias_capturadas": 0, "ouro_reliquias": 0, "maximo_aldeoes": 10, "sobreviveu": true }
            }
            """;
        return $$"""
            {
              "quantidade_jogadores": 2,
              "jogadores": [{{(playersReversed ? second : first)}}, {{(playersReversed ? first : second)}}]
            }
            """;
    }
}
