using System.Text;
using System.Text.Json;

namespace AgeNexus.Application.MatchPerformance;

public sealed record AgeExtractorJsonCompositionResult(
    bool Succeeded,
    string? Json = null,
    string? Error = null);

public sealed class AgeExtractorJsonComposer
{
    private static readonly string[] Categories = ["placar", "militar", "economia", "tecnologia", "sociedade"];

    public AgeExtractorJsonCompositionResult Compose(IReadOnlyCollection<string> fragments)
    {
        if (fragments.Count != Categories.Length)
        {
            return Failure("São necessários resultados das cinco categorias.");
        }

        var documents = new List<JsonDocument>();
        try
        {
            var byCategory = new Dictionary<string, (JsonElement Root, Dictionary<int, JsonElement> Players)>(StringComparer.Ordinal);
            int? playerCount = null;
            foreach (var fragment in fragments)
            {
                var document = JsonDocument.Parse(fragment);
                documents.Add(document);
                var root = document.RootElement;
                if (root.ValueKind != JsonValueKind.Object ||
                    !root.TryGetProperty("categoria", out var categoryElement) ||
                    categoryElement.ValueKind != JsonValueKind.String ||
                    categoryElement.GetString() is not { } category ||
                    !Categories.Contains(category, StringComparer.Ordinal) ||
                    byCategory.ContainsKey(category))
                {
                    return Failure("Um fragmento possui categoria ausente, desconhecida ou repetida.");
                }

                if (!root.TryGetProperty("quantidade_jogadores", out var countElement) ||
                    !countElement.TryGetInt32(out var count) || count is < 2 or > 8 ||
                    playerCount.HasValue && playerCount.Value != count)
                {
                    return Failure("Os fragmentos possuem quantidade de jogadores inválida ou divergente.");
                }
                playerCount = count;

                if (!root.TryGetProperty("jogadores", out var playersElement) ||
                    playersElement.ValueKind != JsonValueKind.Array)
                {
                    return Failure($"O fragmento de {category} não contém jogadores.");
                }

                var players = new Dictionary<int, JsonElement>();
                foreach (var player in playersElement.EnumerateArray())
                {
                    if (player.ValueKind != JsonValueKind.Object ||
                        !player.TryGetProperty("jogador", out var numberElement) ||
                        !numberElement.TryGetInt32(out var number) || number < 1 || number > count ||
                        !player.TryGetProperty(category, out var values) || values.ValueKind != JsonValueKind.Object ||
                        !players.TryAdd(number, values))
                    {
                        return Failure($"O fragmento de {category} possui um jogador inválido ou repetido.");
                    }
                }
                if (players.Count != count)
                {
                    return Failure($"O fragmento de {category} está incompleto.");
                }
                byCategory.Add(category, (root, players));
            }

            if (playerCount is null || Categories.Any(category => !byCategory.ContainsKey(category)))
            {
                return Failure("Nem todas as categorias foram identificadas.");
            }

            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
            {
                writer.WriteStartObject();
                writer.WriteNumber("quantidade_jogadores", playerCount.Value);
                writer.WriteStartArray("jogadores");
                for (var number = 1; number <= playerCount.Value; number++)
                {
                    writer.WriteStartObject();
                    writer.WriteNumber("jogador", number);
                    foreach (var category in Categories)
                    {
                        writer.WritePropertyName(category);
                        byCategory[category].Players[number].WriteTo(writer);
                    }
                    writer.WriteEndObject();
                }
                writer.WriteEndArray();

                var hasWarnings = byCategory.Values.Any(value =>
                    value.Root.TryGetProperty("avisos", out var warnings) &&
                    warnings.ValueKind == JsonValueKind.Array && warnings.GetArrayLength() > 0);
                if (hasWarnings)
                {
                    writer.WriteStartArray("avisos");
                    foreach (var category in Categories)
                    {
                        if (byCategory[category].Root.TryGetProperty("avisos", out var warnings) &&
                            warnings.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var warning in warnings.EnumerateArray())
                            {
                                warning.WriteTo(writer);
                            }
                        }
                    }
                    writer.WriteEndArray();
                }
                writer.WriteEndObject();
            }

            return new(true, Encoding.UTF8.GetString(stream.ToArray()));
        }
        catch (JsonException)
        {
            return Failure("Um dos resultados parciais não contém JSON válido.");
        }
        finally
        {
            foreach (var document in documents)
            {
                document.Dispose();
            }
        }
    }

    private static AgeExtractorJsonCompositionResult Failure(string error) => new(false, Error: error);
}
