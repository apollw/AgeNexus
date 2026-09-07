using System.Globalization;
using System.Text.Json;
using AgeNexus.Domain.MatchPerformance;

namespace AgeNexus.Application.MatchPerformance;

public enum JsonImportStatus { Valid, Partial, Invalid }
public enum JsonImportIssueSeverity { Warning, Error }

public sealed record JsonImportIssue(
    int? PlayerNumber,
    string Path,
    string? OriginalValue,
    string Reason,
    string Effect,
    JsonImportIssueSeverity Severity = JsonImportIssueSeverity.Error);

public sealed record JsonImportNormalization(
    int PlayerNumber,
    string Path,
    string OriginalValue,
    string NormalizedValue);

public sealed record ImportedPlayerStatistics(
    int PlayerNumber,
    MatchStatisticValues Values);

public sealed record AgeExtractorJsonImportResult(
    JsonImportStatus Status,
    string OriginalJson,
    int? PlayerCount,
    IReadOnlyCollection<ImportedPlayerStatistics> Players,
    IReadOnlyCollection<JsonImportIssue> Issues,
    IReadOnlyCollection<JsonImportNormalization> Normalizations)
{
    public bool CanApply => Status is JsonImportStatus.Valid or JsonImportStatus.Partial;
}

public sealed class AgeExtractorJsonImporter
{
    private static readonly string[] ScoreFields = ["militar", "economia", "tecnologia", "sociedade", "pontuacao_total"];
    private static readonly string[] MilitaryFields = ["unidades_mortas", "unidades_perdidas", "construcoes_destruidas", "construcoes_perdidas", "unidades_convertidas", "maior_exercito"];
    private static readonly string[] EconomyFields = ["comida", "madeira", "pedra", "ouro", "lucro_comercial", "tributo_enviado", "tributo_recebido"];
    private static readonly string[] TechnologyFields = ["idade_feudal", "idade_castelos", "idade_imperial", "mapa_explorado", "pesquisas", "percentual_pesquisas"];
    private static readonly string[] SocietyFields = ["maravilhas", "castelos", "reliquias_capturadas", "ouro_reliquias", "maximo_aldeoes", "sobreviveu"];

    public AgeExtractorJsonImportResult Import(string content)
    {
        var original = RemoveOptionalMarkdownFence((content ?? string.Empty).TrimStart('\uFEFF').Trim());
        var issues = new List<JsonImportIssue>();
        var normalizations = new List<JsonImportNormalization>();
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(original);
        }
        catch (JsonException)
        {
            return Invalid(original, "Não foi possível interpretar o conteúdo como JSON.");
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return Invalid(original, "A raiz do JSON precisa ser um objeto.");
            }

            if (!root.TryGetProperty("quantidade_jogadores", out var countElement) ||
                countElement.ValueKind != JsonValueKind.Number || !countElement.TryGetInt32(out var playerCount) ||
                playerCount is < 2 or > 8)
            {
                return Invalid(original, "quantidade_jogadores deve ser um inteiro entre 2 e 8.", "quantidade_jogadores");
            }

            if (!root.TryGetProperty("jogadores", out var playersElement) || playersElement.ValueKind != JsonValueKind.Array)
            {
                return Invalid(original, "jogadores deve ser um array.", "jogadores", playerCount);
            }

            var playerElements = playersElement.EnumerateArray().ToArray();
            if (playerElements.Length != playerCount)
            {
                return Invalid(original, $"O arquivo informa {playerCount} jogadores, mas contém {playerElements.Length} registros.", "jogadores", playerCount);
            }

            var identified = new List<(int Number, JsonElement Element)>();
            foreach (var element in playerElements)
            {
                if (element.ValueKind != JsonValueKind.Object ||
                    !element.TryGetProperty("jogador", out var numberElement) ||
                    numberElement.ValueKind != JsonValueKind.Number || !numberElement.TryGetInt32(out var number) ||
                    number < 1 || number > playerCount)
                {
                    return Invalid(original, $"Cada participante deve possuir jogador entre 1 e {playerCount}.", "jogadores[].jogador", playerCount);
                }

                identified.Add((number, element));
            }

            if (identified.Select(x => x.Number).Distinct().Count() != playerCount ||
                !identified.Select(x => x.Number).Order().SequenceEqual(Enumerable.Range(1, playerCount)))
            {
                return Invalid(original, $"Os identificadores devem formar o conjunto de 1 até {playerCount}, sem repetição.", "jogadores[].jogador", playerCount);
            }

            var players = identified.OrderBy(x => x.Number)
                .Select(x => ReadPlayer(x.Number, x.Element, issues, normalizations)).ToArray();
            return new(
                issues.Any(x => x.Severity == JsonImportIssueSeverity.Error) ? JsonImportStatus.Partial : JsonImportStatus.Valid,
                original, playerCount, players, issues, normalizations);
        }
    }

    private static ImportedPlayerStatistics ReadPlayer(
        int number,
        JsonElement element,
        List<JsonImportIssue> issues,
        List<JsonImportNormalization> normalizations)
    {
        var reader = new PlayerReader(number, element, issues, normalizations);
        var militaryScore = reader.Int("placar", "militar", ScoreFields);
        var economyScore = reader.Int("placar", "economia", ScoreFields);
        var technologyScore = reader.Int("placar", "tecnologia", ScoreFields);
        var societyScore = reader.Int("placar", "sociedade", ScoreFields);
        var totalScore = reader.Int("placar", "pontuacao_total", ScoreFields);
        var feudal = reader.Time("tecnologia", "idade_feudal", TechnologyFields);
        var castle = reader.Time("tecnologia", "idade_castelos", TechnologyFields);
        var imperial = reader.Time("tecnologia", "idade_imperial", TechnologyFields);

        if (militaryScore.HasValue && economyScore.HasValue && technologyScore.HasValue && societyScore.HasValue && totalScore.HasValue &&
            (long)militaryScore + economyScore + technologyScore + societyScore != totalScore)
        {
            reader.Warning("placar.pontuacao_total", totalScore.Value.ToString(CultureInfo.InvariantCulture),
                "A pontuação total diverge da soma das quatro categorias.", "O valor original foi preservado para revisão.");
        }

        if ((feudal.HasValue && castle.HasValue && feudal > castle) ||
            (castle.HasValue && imperial.HasValue && castle > imperial))
        {
            reader.Warning("tecnologia", null, "Os tempos de avanço das idades estão fora de ordem.",
                "Os tempos foram preservados, mas devem ser revisados.");
        }

        var values = new MatchStatisticValues(
            reader.Int("militar", "unidades_mortas", MilitaryFields),
            reader.Int("militar", "unidades_perdidas", MilitaryFields),
            reader.Int("militar", "construcoes_destruidas", MilitaryFields),
            reader.Int("militar", "construcoes_perdidas", MilitaryFields),
            reader.Int("militar", "maior_exercito", MilitaryFields),
            reader.Int("sociedade", "maximo_aldeoes", SocietyFields),
            reader.Long("economia", "comida", EconomyFields),
            reader.Long("economia", "madeira", EconomyFields),
            reader.Long("economia", "ouro", EconomyFields),
            reader.Long("economia", "pedra", EconomyFields),
            militaryScore, economyScore, technologyScore, societyScore, totalScore,
            reader.Int("militar", "unidades_convertidas", MilitaryFields),
            reader.Long("economia", "lucro_comercial", EconomyFields),
            reader.Long("sociedade", "ouro_reliquias", SocietyFields),
            reader.Long("economia", "tributo_enviado", EconomyFields),
            reader.Long("economia", "tributo_recebido", EconomyFields),
            reader.Int("tecnologia", "pesquisas", TechnologyFields),
            reader.Percent("tecnologia", "mapa_explorado", TechnologyFields),
            feudal, castle, imperial, null,
            reader.Percent("tecnologia", "percentual_pesquisas", TechnologyFields),
            reader.Int("sociedade", "maravilhas", SocietyFields),
            reader.Int("sociedade", "castelos", SocietyFields),
            reader.Int("sociedade", "reliquias_capturadas", SocietyFields),
            false,
            reader.Boolean("sociedade", "sobreviveu", SocietyFields));
        return new(number, values);
    }

    private static AgeExtractorJsonImportResult Invalid(string original, string reason, string path = "$", int? count = null) =>
        new(JsonImportStatus.Invalid, original, count, [],
            [new(null, path, null, reason, "A importação foi interrompida.")], []);

    private static string RemoveOptionalMarkdownFence(string value)
    {
        if (!value.StartsWith("```json", StringComparison.OrdinalIgnoreCase) || !value.EndsWith("```", StringComparison.Ordinal))
        {
            return value;
        }

        var firstBreak = value.IndexOf('\n');
        return firstBreak < 0 ? value : value[(firstBreak + 1)..^3].Trim();
    }

    private sealed class PlayerReader(
        int playerNumber,
        JsonElement player,
        List<JsonImportIssue> issues,
        List<JsonImportNormalization> normalizations)
    {
        public int? Int(string group, string field, string[] knownFields)
        {
            var value = Field(group, field, knownFields);
            var parsed = ParseInteger(value, int.MaxValue, group, field);
            return parsed.HasValue ? (int)parsed.Value : null;
        }

        public long? Long(string group, string field, string[] knownFields) =>
            ParseInteger(Field(group, field, knownFields), long.MaxValue, group, field);

        public decimal? Percent(string group, string field, string[] knownFields)
        {
            var value = Int(group, field, knownFields);
            if (value is < 0 or > 100)
            {
                Error($"{group}.{field}", value?.ToString(CultureInfo.InvariantCulture), "O percentual deve estar entre 0 e 100.");
                return null;
            }
            return value;
        }

        public int? Time(string group, string field, string[] knownFields)
        {
            var value = Field(group, field, knownFields);
            if (!value.HasValue) return null;
            if (value.Value.ValueKind != JsonValueKind.String)
            {
                Error($"{group}.{field}", value.Value.GetRawText(), "O tempo deve ser uma string H:MM:SS ou HH:MM:SS.");
                return null;
            }
            var text = value.Value.GetString() ?? string.Empty;
            var parts = text.Split(':');
            if (parts.Length != 3 || parts.Any(x => x.Length == 0 || !x.All(IsAsciiDigit)) ||
                parts[1].Length != 2 || parts[2].Length != 2 ||
                !long.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var hours) ||
                !int.TryParse(parts[1], out var minutes) || !int.TryParse(parts[2], out var seconds) ||
                minutes > 59 || seconds > 59 || hours > (int.MaxValue - minutes * 60L - seconds) / 3600L)
            {
                Error($"{group}.{field}", value.Value.GetRawText(), "O tempo não segue o formato H:MM:SS com minutos e segundos entre 00 e 59.");
                return null;
            }
            return checked((int)(hours * 3600 + minutes * 60 + seconds));
        }

        public bool? Boolean(string group, string field, string[] knownFields)
        {
            var value = Field(group, field, knownFields);
            if (!value.HasValue) return null;
            if (value.Value.ValueKind is JsonValueKind.True or JsonValueKind.False) return value.Value.GetBoolean();
            Error($"{group}.{field}", value.Value.GetRawText(), "O valor deve ser booleano true ou false.");
            return null;
        }

        public void Warning(string path, string? original, string reason, string effect) =>
            issues.Add(new(playerNumber, path, original, reason, effect, JsonImportIssueSeverity.Warning));

        private JsonElement? Field(string groupName, string fieldName, string[] knownFields)
        {
            var path = $"{groupName}.{fieldName}";
            if (!player.TryGetProperty(groupName, out var group) || group.ValueKind != JsonValueKind.Object)
            {
                Error(path, null, $"O grupo {groupName} está ausente ou não é um objeto.");
                return null;
            }
            if (!knownFields.Contains(fieldName) || !group.TryGetProperty(fieldName, out var value) ||
                value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            {
                Error(path, null, "O campo está ausente ou nulo.");
                return null;
            }
            return value;
        }

        private long? ParseInteger(JsonElement? value, long maximum, string group, string field)
        {
            if (!value.HasValue) return null;
            long parsed;
            if (value.Value.ValueKind == JsonValueKind.Number && value.Value.TryGetInt64(out parsed))
            {
                // Parsed below.
            }
            else if (value.Value.ValueKind == JsonValueKind.String)
            {
                var original = value.Value.GetString() ?? string.Empty;
                var trimmed = original.Trim();
                if (trimmed.Length == 0 || !trimmed.All(IsAsciiDigit) ||
                    !long.TryParse(trimmed, NumberStyles.None, CultureInfo.InvariantCulture, out parsed))
                {
                    Error($"{group}.{field}", value.Value.GetRawText(), "O valor deve ser um inteiro não negativo.");
                    return null;
                }
                normalizations.Add(new(playerNumber, $"{group}.{field}", value.Value.GetRawText(), parsed.ToString(CultureInfo.InvariantCulture)));
            }
            else
            {
                Error($"{group}.{field}", value.Value.GetRawText(), "O valor deve ser um inteiro não negativo.");
                return null;
            }
            if (parsed < 0 || parsed > maximum)
            {
                Error($"{group}.{field}", value.Value.GetRawText(), "O valor está fora da faixa aceita.");
                return null;
            }
            return parsed;
        }

        private void Error(string path, string? original, string reason) =>
            issues.Add(new(playerNumber, path, original, reason, "O campo ficará fora dos cálculos que dependem dele."));

        private static bool IsAsciiDigit(char value) => value is >= '0' and <= '9';
    }
}
