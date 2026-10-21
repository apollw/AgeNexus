namespace AgeNexus.Application.Queries;

public enum AchievementMetric
{
    Matches,
    Victories,
    UnitsKilled,
    BuildingsDestroyed,
    FoodCollected,
    WoodCollected,
    GoldCollected,
    StoneCollected,
    TradeGold,
    RelicGold,
    UnitsConverted,
    ResearchCount,
    CastlesBuilt,
    WondersBuilt,
    RelicsCaptured,
    TeamMvpAwards,
    MatchesSurvived
}

public sealed record AchievementDefinition(
    string Key,
    string Category,
    string Title,
    string Description,
    string Icon,
    AchievementMetric Metric,
    decimal Target);

public static class AchievementCatalog
{
    public static IReadOnlyList<AchievementDefinition> All { get; } =
    [
        new("first-battle", "Jornada", "O primeiro capítulo", "Participe de 1 partida com estatísticas validadas.", "banner", AchievementMetric.Matches, 1),
        new("five-battles", "Jornada", "Escudeiro do Nexus", "Participe de 5 partidas com estatísticas validadas.", "banner", AchievementMetric.Matches, 5),
        new("ten-battles", "Jornada", "Veterano de campanha", "Participe de 10 partidas com estatísticas validadas.", "banner", AchievementMetric.Matches, 10),
        new("twenty-five-battles", "Jornada", "Cronista de guerras", "Participe de 25 partidas com estatísticas validadas.", "banner", AchievementMetric.Matches, 25),
        new("fifty-battles", "Jornada", "General incansável", "Participe de 50 partidas com estatísticas validadas.", "banner", AchievementMetric.Matches, 50),
        new("hundred-battles", "Jornada", "Lenda do campo de batalha", "Participe de 100 partidas com estatísticas validadas.", "crown", AchievementMetric.Matches, 100),

        new("first-victory", "Vitórias", "Primeiro estandarte", "Conquiste sua primeira vitória validada.", "trophy", AchievementMetric.Victories, 1),
        new("five-victories", "Vitórias", "Comandante promissor", "Conquiste 5 vitórias validadas.", "trophy", AchievementMetric.Victories, 5),
        new("ten-victories", "Vitórias", "Marcha triunfal", "Conquiste 10 vitórias validadas.", "trophy", AchievementMetric.Victories, 10),
        new("twenty-five-victories", "Vitórias", "Senhor da guerra", "Conquiste 25 vitórias validadas.", "crown", AchievementMetric.Victories, 25),
        new("fifty-victories", "Vitórias", "Invicto na memória", "Conquiste 50 vitórias validadas.", "crown", AchievementMetric.Victories, 50),

        new("hundred-kills", "Combate", "Lâmina afiada", "Elimine 100 unidades inimigas no total.", "swords", AchievementMetric.UnitsKilled, 100),
        new("five-hundred-kills", "Combate", "Colhedor de exércitos", "Elimine 500 unidades inimigas no total.", "swords", AchievementMetric.UnitsKilled, 500),
        new("thousand-kills", "Combate", "Ceifador de mil", "Elimine 1.000 unidades inimigas no total.", "skull", AchievementMetric.UnitsKilled, 1_000),
        new("twenty-five-hundred-kills", "Combate", "Flagelo das legiões", "Elimine 2.500 unidades inimigas no total.", "skull", AchievementMetric.UnitsKilled, 2_500),
        new("five-thousand-kills", "Combate", "Onde exércitos terminam", "Elimine 5.000 unidades inimigas no total.", "skull", AchievementMetric.UnitsKilled, 5_000),

        new("twenty-five-buildings", "Cerco", "Martelo de portões", "Destrua 25 construções inimigas no total.", "siege", AchievementMetric.BuildingsDestroyed, 25),
        new("hundred-buildings", "Cerco", "Demolidor de muralhas", "Destrua 100 construções inimigas no total.", "siege", AchievementMetric.BuildingsDestroyed, 100),
        new("five-hundred-buildings", "Cerco", "Terra arrasada", "Destrua 500 construções inimigas no total.", "fire", AchievementMetric.BuildingsDestroyed, 500),
        new("thousand-buildings", "Cerco", "Ruína dos impérios", "Destrua 1.000 construções inimigas no total.", "fire", AchievementMetric.BuildingsDestroyed, 1_000),

        new("ten-thousand-food", "Economia", "Celeiros abastecidos", "Colete 10.000 de comida no total.", "food", AchievementMetric.FoodCollected, 10_000),
        new("hundred-thousand-food", "Economia", "Banquete do reino", "Colete 100.000 de comida no total.", "food", AchievementMetric.FoodCollected, 100_000),
        new("ten-thousand-wood", "Economia", "Mestre das florestas", "Colete 10.000 de madeira no total.", "wood", AchievementMetric.WoodCollected, 10_000),
        new("hundred-thousand-wood", "Economia", "Uma frota de carvalho", "Colete 100.000 de madeira no total.", "wood", AchievementMetric.WoodCollected, 100_000),
        new("ten-thousand-gold", "Economia", "Cofres reais", "Colete 10.000 de ouro no total.", "gold", AchievementMetric.GoldCollected, 10_000),
        new("hundred-thousand-gold", "Economia", "Toque de Midas", "Colete 100.000 de ouro no total.", "gold", AchievementMetric.GoldCollected, 100_000),
        new("five-thousand-stone", "Economia", "Fundação de pedra", "Colete 5.000 de pedra no total.", "stone", AchievementMetric.StoneCollected, 5_000),
        new("fifty-thousand-stone", "Economia", "Montanha particular", "Colete 50.000 de pedra no total.", "stone", AchievementMetric.StoneCollected, 50_000),

        new("thousand-trade-gold", "Comércio", "Primeira rota comercial", "Produza 1.000 de ouro por comércio no total.", "trade", AchievementMetric.TradeGold, 1_000),
        new("ten-thousand-trade-gold", "Comércio", "Mercador das quatro rotas", "Produza 10.000 de ouro por comércio no total.", "trade", AchievementMetric.TradeGold, 10_000),
        new("thousand-relic-gold", "Religião", "Dízimo sagrado", "Produza 1.000 de ouro por relíquias no total.", "relic", AchievementMetric.RelicGold, 1_000),
        new("ten-thousand-relic-gold", "Religião", "Tesouro do mosteiro", "Produza 10.000 de ouro por relíquias no total.", "relic", AchievementMetric.RelicGold, 10_000),

        new("ten-conversions", "Religião", "Wololo!", "Converta 10 unidades inimigas no total.", "monk", AchievementMetric.UnitsConverted, 10),
        new("hundred-conversions", "Religião", "A palavra convence", "Converta 100 unidades inimigas no total.", "monk", AchievementMetric.UnitsConverted, 100),
        new("five-hundred-conversions", "Religião", "Fé irresistível", "Converta 500 unidades inimigas no total.", "monk", AchievementMetric.UnitsConverted, 500),

        new("twenty-five-researches", "Tecnologia", "Aprendiz da universidade", "Conclua 25 tecnologias no total.", "research", AchievementMetric.ResearchCount, 25),
        new("hundred-researches", "Tecnologia", "Século das descobertas", "Conclua 100 tecnologias no total.", "research", AchievementMetric.ResearchCount, 100),
        new("five-hundred-researches", "Tecnologia", "À frente do seu tempo", "Conclua 500 tecnologias no total.", "research", AchievementMetric.ResearchCount, 500),

        new("first-castle", "Sociedade", "Pedra sobre pedra", "Construa seu primeiro castelo registrado.", "castle", AchievementMetric.CastlesBuilt, 1),
        new("ten-castles", "Sociedade", "Senhor dos castelos", "Construa 10 castelos no total.", "castle", AchievementMetric.CastlesBuilt, 10),
        new("fifty-castles", "Sociedade", "Horizonte fortificado", "Construa 50 castelos no total.", "castle", AchievementMetric.CastlesBuilt, 50),
        new("first-wonder", "Sociedade", "Algo maravilhoso", "Construa sua primeira maravilha registrada.", "wonder", AchievementMetric.WondersBuilt, 1),
        new("five-wonders", "Sociedade", "Arquiteto das eras", "Construa 5 maravilhas no total.", "wonder", AchievementMetric.WondersBuilt, 5),
        new("ten-wonders", "Sociedade", "O mundo vai se lembrar", "Construa 10 maravilhas no total.", "wonder", AchievementMetric.WondersBuilt, 10),

        new("first-relic", "Exploração", "Relíquia encontrada", "Capture sua primeira relíquia registrada.", "relic", AchievementMetric.RelicsCaptured, 1),
        new("ten-relics", "Exploração", "Guardião das relíquias", "Capture 10 relíquias no total.", "relic", AchievementMetric.RelicsCaptured, 10),
        new("fifty-relics", "Exploração", "Museu dos séculos", "Capture 50 relíquias no total.", "relic", AchievementMetric.RelicsCaptured, 50),

        new("first-mvp", "Prestígio", "Destaque da equipe", "Seja escolhido como melhor jogador da equipe pela primeira vez.", "star", AchievementMetric.TeamMvpAwards, 1),
        new("ten-mvp", "Prestígio", "Carregador de estandartes", "Seja escolhido como melhor jogador da equipe 10 vezes.", "star", AchievementMetric.TeamMvpAwards, 10),
        new("ten-survivals", "Prestígio", "Último de pé", "Sobreviva ao fim de 10 partidas registradas.", "shield", AchievementMetric.MatchesSurvived, 10)
    ];
}

public sealed record PlayerAchievement(
    AchievementDefinition Definition,
    decimal Progress,
    DateTimeOffset? UnlockedAtUtc)
{
    public bool IsUnlocked => UnlockedAtUtc.HasValue;
    public decimal ProgressPercent => Definition.Target <= 0
        ? 0
        : Math.Min(100m, Progress * 100m / Definition.Target);
}

public sealed record AchievementFeedEntry(
    Guid PlayerId,
    string DisplayName,
    string? AvatarUrl,
    AchievementDefinition Definition,
    DateTimeOffset UnlockedAtUtc);

public sealed record PagedAchievementFeed(
    IReadOnlyCollection<AchievementFeedEntry> Items,
    int Page,
    int PageSize,
    int TotalItems)
{
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalItems / (double)PageSize));
}

public interface IAchievementQueryService
{
    Task<IReadOnlyCollection<PlayerAchievement>> GetPlayerAchievementsAsync(
        Guid playerId,
        CancellationToken cancellationToken = default);

    Task<PagedAchievementFeed> GetRecentUnlocksAsync(
        int page = 1,
        int pageSize = 8,
        CancellationToken cancellationToken = default);
}
