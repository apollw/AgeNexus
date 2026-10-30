using System.Collections.Concurrent;
using System.Data;
using System.Data.Common;
using System.Text.Json;
using AgeNexus.Application.Drafts;
using AgeNexus.Domain.Matches;
using AgeNexus.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AgeNexus.Infrastructure.Drafts;

public sealed class CivilizationDraftService(
    AgeNexusDbContext database,
    ICivilizationDraftNotifier notifier) : ICivilizationDraftService
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> Locks = new(StringComparer.OrdinalIgnoreCase);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<CivilizationDraftOperationResult> CreateAsync(
        Guid creatorPlayerProfileId,
        CreateCivilizationDraftRequest request,
        CancellationToken cancellationToken = default)
    {
        var title = request.Title.Trim();
        var teams = request.Teams.Where(x => x.PlayerProfileIds.Count > 0).ToArray();
        var playerIds = teams.SelectMany(x => x.PlayerProfileIds).ToArray();
        if (title.Length is 0 or > 120 || teams.Length is < 2 or > 8 || playerIds.Length is < 2 or > 8 ||
            playerIds.Distinct().Count() != playerIds.Length || teams.Any(x => x.Name.Trim().Length is 0 or > 40))
        {
            return CivilizationDraftOperationResult.Failure("InvalidSetup");
        }

        var edition = await database.GameEditions.AsNoTracking()
            .Where(x => x.Id == request.GameEditionId && x.IsActive)
            .Select(x => new { x.Id, x.Name })
            .SingleOrDefaultAsync(cancellationToken);
        var players = await database.PlayerProfiles.AsNoTracking()
            .Where(x => playerIds.Contains(x.Id))
            .Select(x => new { x.Id, x.DisplayName, x.AvatarUrl })
            .ToDictionaryAsync(x => x.Id, cancellationToken);
        var map = request.MapDefinitionId.HasValue
            ? await database.MapDefinitions.AsNoTracking()
                .Where(x => x.Id == request.MapDefinitionId && x.GameEditionId == request.GameEditionId)
                .Select(x => new { x.Id, x.Name }).SingleOrDefaultAsync(cancellationToken)
            : null;
        if (edition is null || players.Count != playerIds.Length || (request.MapDefinitionId.HasValue && map is null))
        {
            return CivilizationDraftOperationResult.Failure("InvalidCatalog");
        }

        var factions = await database.Factions.AsNoTracking()
            .Where(x => x.GameEditionId == request.GameEditionId)
            .Select(x => new FactionState { Id = x.Id, Name = x.Name, Slug = x.Slug, ImageUrl = x.ImageUrl })
            .ToArrayAsync(cancellationToken);
        var poolSize = Math.Min(factions.Length, Math.Max(12, playerIds.Length * 4));
        if (poolSize < playerIds.Length * 3)
        {
            return CivilizationDraftOperationResult.Failure("InsufficientFactions");
        }

        var usedCounts = await GetPoolUsageCountsAsync(request.GameEditionId, cancellationToken);
        var pool = factions
            .OrderBy(x => usedCounts.GetValueOrDefault(x.Id))
            .ThenBy(_ => Random.Shared.Next())
            .Take(poolSize)
            .ToList();
        var state = new DraftState
        {
            Title = title,
            EditionName = edition.Name,
            MapName = map?.Name,
            GameMode = request.GameMode,
            Phase = CivilizationDraftPhase.Lobby,
            Pool = pool,
            Teams = teams.Select((team, index) => new TeamState
            {
                Name = team.Name.Trim(),
                Players = team.PlayerProfileIds.Select(id => new ParticipantState
                {
                    PlayerProfileId = id,
                    DisplayName = players[id].DisplayName,
                    AvatarUrl = players[id].AvatarUrl
                }).ToList()
            }).ToList()
        };
        var id = Guid.NewGuid();
        var shareCode = await CreateUniqueShareCodeAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;
        await database.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO public.civilization_drafts
                (id, share_code, created_by_player_profile_id, game_edition_id, map_definition_id,
                 game_mode, status, version, state_json, created_at_utc, updated_at_utc)
            VALUES
                ({id}, {shareCode}, {creatorPlayerProfileId}, {request.GameEditionId}, {request.MapDefinitionId},
                 {request.GameMode.ToString()}, {state.Phase.ToString()}, 1, {JsonSerializer.Serialize(state, JsonOptions)}::jsonb, {now}, {now})
            """, cancellationToken);
        return CivilizationDraftOperationResult.Success(shareCode);
    }

    public async Task<CivilizationDraftView?> GetAsync(string shareCode, Guid viewerPlayerProfileId, CancellationToken cancellationToken = default)
    {
        var row = await GetRowAsync(shareCode, cancellationToken);
        return row is null ? null : ToView(row, viewerPlayerProfileId);
    }

    public Task<CivilizationDraftOperationResult> SetReadyAsync(string shareCode, Guid playerProfileId, bool ready, CancellationToken cancellationToken = default)
        => MutateAsync(shareCode, playerProfileId, (_, state, participant) =>
        {
            if (state.Phase != CivilizationDraftPhase.Lobby || participant is null) return "InvalidPhase";
            participant.Ready = ready;
            return null;
        }, cancellationToken);

    public Task<CivilizationDraftOperationResult> StartAsync(string shareCode, Guid organizerPlayerProfileId, CancellationToken cancellationToken = default)
        => MutateAsync(shareCode, organizerPlayerProfileId, (row, state, _) =>
        {
            if (row.CreatorPlayerProfileId != organizerPlayerProfileId) return "OrganizerOnly";
            if (state.Phase != CivilizationDraftPhase.Lobby) return "InvalidPhase";
            if (state.Teams.SelectMany(x => x.Players).Any(x => !x.Ready)) return "PlayersNotReady";
            state.Queue = BuildSnakeQueue(state.Teams);
            state.ActionIndex = 0;
            state.Phase = CivilizationDraftPhase.Preferences;
            return null;
        }, cancellationToken);

    public Task<CivilizationDraftOperationResult> SubmitPreferencesAsync(string shareCode, Guid playerProfileId, IReadOnlyCollection<Guid> factionIds, CancellationToken cancellationToken = default)
        => MutateAsync(shareCode, playerProfileId, (_, state, participant) =>
        {
            var ids = factionIds.Distinct().ToArray();
            if (state.Phase != CivilizationDraftPhase.Preferences || participant is null || CurrentPlayer(state) != playerProfileId) return "NotYourTurn";
            if (ids.Length != 2 || ids.Any(id => state.Pool.All(x => x.Id != id))) return "ChooseTwoFactions";
            var reserved = state.Teams.SelectMany(x => x.Players).SelectMany(x => x.PreferenceFactionIds).ToHashSet();
            if (ids.Any(reserved.Contains)) return "FactionUnavailable";
            participant.PreferenceFactionIds = ids.ToList();
            Advance(state, CivilizationDraftPhase.Bans);
            return null;
        }, cancellationToken);

    public Task<CivilizationDraftOperationResult> SubmitBanAsync(string shareCode, Guid playerProfileId, Guid factionId, CancellationToken cancellationToken = default)
        => MutateAsync(shareCode, playerProfileId, (_, state, participant) =>
        {
            if (state.Phase != CivilizationDraftPhase.Bans || participant is null || CurrentPlayer(state) != playerProfileId) return "NotYourTurn";
            if (participant.PreferenceFactionIds.Contains(factionId)) return "CannotBanOwnPreference";
            if (state.BannedFactionIds.Contains(factionId) || state.ProtectedFactionIds.Contains(factionId) || state.Pool.All(x => x.Id != factionId)) return "FactionUnavailable";
            state.BannedFactionIds.Add(factionId);
            foreach (var target in state.Teams.SelectMany(x => x.Players).Where(x => x.PreferenceFactionIds.Contains(factionId) && !x.PickFactionId.HasValue))
            {
                var remaining = target.PreferenceFactionIds.Single(x => x != factionId);
                target.PickFactionId = remaining;
                target.PickWasProtected = true;
                state.ProtectedFactionIds.Add(remaining);
            }
            Advance(state, CivilizationDraftPhase.Picks);
            if (state.Phase == CivilizationDraftPhase.Picks)
            {
                state.Queue = state.Queue.Where(id => FindParticipant(state, id)?.PickFactionId is null).ToList();
                state.ActionIndex = 0;
                if (state.Queue.Count == 0) state.Phase = CivilizationDraftPhase.Review;
            }
            return null;
        }, cancellationToken);

    public Task<CivilizationDraftOperationResult> SubmitPickAsync(string shareCode, Guid playerProfileId, Guid factionId, CancellationToken cancellationToken = default)
        => MutateAsync(shareCode, playerProfileId, (_, state, participant) =>
        {
            if (state.Phase != CivilizationDraftPhase.Picks || participant is null || CurrentPlayer(state) != playerProfileId) return "NotYourTurn";
            if (!participant.PreferenceFactionIds.Contains(factionId) || state.BannedFactionIds.Contains(factionId)) return "FactionUnavailable";
            participant.PickFactionId = factionId;
            Advance(state, CivilizationDraftPhase.Review);
            return null;
        }, cancellationToken);

    public Task<CivilizationDraftOperationResult> ConfirmFinalAsync(string shareCode, Guid playerProfileId, CancellationToken cancellationToken = default)
        => MutateAsync(shareCode, playerProfileId, (_, state, participant) =>
        {
            if (state.Phase != CivilizationDraftPhase.Review || participant is null) return "InvalidPhase";
            participant.FinalConfirmed = true;
            if (state.Teams.SelectMany(x => x.Players).All(x => x.FinalConfirmed)) state.Phase = CivilizationDraftPhase.Completed;
            return null;
        }, cancellationToken);

    public Task<CivilizationDraftOperationResult> CancelAsync(string shareCode, Guid organizerPlayerProfileId, CancellationToken cancellationToken = default)
        => MutateAsync(shareCode, organizerPlayerProfileId, (row, state, _) =>
        {
            if (row.CreatorPlayerProfileId != organizerPlayerProfileId) return "OrganizerOnly";
            if (state.Phase is CivilizationDraftPhase.Completed or CivilizationDraftPhase.Cancelled) return "InvalidPhase";
            state.Phase = CivilizationDraftPhase.Cancelled;
            return null;
        }, cancellationToken);

    private async Task<CivilizationDraftOperationResult> MutateAsync(
        string shareCode,
        Guid actorId,
        Func<DraftRow, DraftState, ParticipantState?, string?> mutation,
        CancellationToken cancellationToken)
    {
        shareCode = NormalizeCode(shareCode);
        var gate = Locks.GetOrAdd(shareCode, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            var row = await GetRowAsync(shareCode, cancellationToken);
            if (row is null) return CivilizationDraftOperationResult.Failure("NotFound");
            var error = mutation(row, row.State, FindParticipant(row.State, actorId));
            if (error is not null) return CivilizationDraftOperationResult.Failure(error);
            var now = DateTimeOffset.UtcNow;
            var affected = await database.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE public.civilization_drafts
                SET status = {row.State.Phase.ToString()}, version = version + 1,
                    state_json = {JsonSerializer.Serialize(row.State, JsonOptions)}::jsonb, updated_at_utc = {now}
                WHERE id = {row.Id} AND version = {row.Version}
                """, cancellationToken);
            if (affected != 1) return CivilizationDraftOperationResult.Failure("ConcurrentChange");
        }
        finally
        {
            gate.Release();
        }
        await notifier.NotifyAsync(shareCode);
        return CivilizationDraftOperationResult.Success(shareCode);
    }

    private static void Advance(DraftState state, CivilizationDraftPhase nextPhase)
    {
        state.ActionIndex++;
        if (state.ActionIndex < state.Queue.Count) return;
        state.Phase = nextPhase;
        state.ActionIndex = 0;
    }

    private static Guid? CurrentPlayer(DraftState state) =>
        (state.Phase is CivilizationDraftPhase.Preferences or CivilizationDraftPhase.Bans or CivilizationDraftPhase.Picks) &&
        state.ActionIndex >= 0 && state.ActionIndex < state.Queue.Count
            ? state.Queue[state.ActionIndex]
            : null;

    private static List<Guid> BuildSnakeQueue(IReadOnlyList<TeamState> teams)
    {
        var queue = new List<Guid>();
        var maxPlayers = teams.Max(x => x.Players.Count);
        for (var round = 0; round < maxPlayers; round++)
        {
            var ordered = round % 2 == 0 ? teams : teams.Reverse();
            queue.AddRange(ordered.Where(x => x.Players.Count > round).Select(x => x.Players[round].PlayerProfileId));
        }
        return queue;
    }

    private static ParticipantState? FindParticipant(DraftState state, Guid id) =>
        state.Teams.SelectMany(x => x.Players).SingleOrDefault(x => x.PlayerProfileId == id);

    private CivilizationDraftView ToView(DraftRow row, Guid viewerId)
    {
        var state = row.State;
        var own = FindParticipant(state, viewerId);
        var revealBans = state.Phase is CivilizationDraftPhase.Picks or CivilizationDraftPhase.Review or CivilizationDraftPhase.Completed;
        var reserved = state.Teams.SelectMany(x => x.Players).SelectMany(x => x.PreferenceFactionIds).ToHashSet();
        var factions = state.Pool.Select(faction => new CivilizationDraftFactionView(
            faction.Id, faction.Name, faction.ImageUrl ?? $"/images/civilizations/{faction.Slug}.png",
            revealBans && state.BannedFactionIds.Contains(faction.Id), state.ProtectedFactionIds.Contains(faction.Id),
            reserved.Contains(faction.Id) || state.BannedFactionIds.Contains(faction.Id))).ToArray();
        CivilizationDraftParticipantView MapPlayer(ParticipantState player)
        {
            var picked = player.PickFactionId.HasValue ? state.Pool.SingleOrDefault(x => x.Id == player.PickFactionId.Value) : null;
            return new(player.PlayerProfileId, player.DisplayName, player.AvatarUrl, player.Ready, player.FinalConfirmed,
                player.PickFactionId, picked?.Name, picked is null ? null : picked.ImageUrl ?? $"/images/civilizations/{picked.Slug}.png", player.PickWasProtected);
        }
        var totalPlayers = state.Teams.Sum(x => x.Players.Count);
        var total = state.Phase switch
        {
            CivilizationDraftPhase.Lobby => totalPlayers,
            CivilizationDraftPhase.Preferences => totalPlayers,
            CivilizationDraftPhase.Bans => totalPlayers,
            CivilizationDraftPhase.Picks => state.Queue.Count,
            CivilizationDraftPhase.Review => totalPlayers,
            _ => totalPlayers
        };
        return new(row.Id, row.ShareCode, state.Title, state.EditionName, state.MapName, state.GameMode, state.Phase,
            row.CreatorPlayerProfileId, CurrentPlayer(state), state.Teams.Select(x => new CivilizationDraftTeamView(x.Name, x.Players.Select(MapPlayer).ToArray())).ToArray(),
            factions, own?.PreferenceFactionIds.ToArray() ?? [], revealBans ? state.BannedFactionIds.ToArray() : [],
            state.Phase == CivilizationDraftPhase.Lobby ? state.Teams.SelectMany(x => x.Players).Count(x => x.Ready) : state.ActionIndex,
            total, row.Version, row.UpdatedAtUtc);
    }

    private async Task<DraftRow?> GetRowAsync(string shareCode, CancellationToken cancellationToken)
    {
        await using var command = database.Database.GetDbConnection().CreateCommand();
        command.CommandText = """
            SELECT id, share_code, created_by_player_profile_id, game_edition_id, version, state_json::text, updated_at_utc
            FROM public.civilization_drafts WHERE share_code = @code LIMIT 1
            """;
        AddParameter(command, "@code", NormalizeCode(shareCode));
        if (command.Connection!.State != ConnectionState.Open) await command.Connection.OpenAsync(cancellationToken);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        return new DraftRow(reader.GetGuid(0), reader.GetString(1), reader.GetGuid(2), reader.GetGuid(3), reader.GetInt32(4),
            JsonSerializer.Deserialize<DraftState>(reader.GetString(5), JsonOptions)!, new DateTimeOffset(DateTime.SpecifyKind(reader.GetDateTime(6), DateTimeKind.Utc)));
    }

    private async Task<Dictionary<Guid, int>> GetPoolUsageCountsAsync(Guid editionId, CancellationToken cancellationToken)
    {
        var result = new Dictionary<Guid, int>();
        await using var command = database.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT state_json::text FROM public.civilization_drafts WHERE game_edition_id = @edition AND status <> 'Cancelled'";
        AddParameter(command, "@edition", editionId);
        if (command.Connection!.State != ConnectionState.Open) await command.Connection.OpenAsync(cancellationToken);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var state = JsonSerializer.Deserialize<DraftState>(reader.GetString(0), JsonOptions);
            if (state is null) continue;
            foreach (var faction in state.Pool.Where(x => !state.BannedFactionIds.Contains(x.Id)))
                result[faction.Id] = result.GetValueOrDefault(faction.Id) + 1;
        }
        return result;
    }

    private async Task<string> CreateUniqueShareCodeAsync(CancellationToken cancellationToken)
    {
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var code = string.Concat(Enumerable.Range(0, 8).Select(_ => alphabet[Random.Shared.Next(alphabet.Length)]));
            await using var command = database.Database.GetDbConnection().CreateCommand();
            command.CommandText = "SELECT EXISTS (SELECT 1 FROM public.civilization_drafts WHERE share_code = @code)";
            AddParameter(command, "@code", code);
            if (command.Connection!.State != ConnectionState.Open) await command.Connection.OpenAsync(cancellationToken);
            if (!(bool)(await command.ExecuteScalarAsync(cancellationToken))!) return code;
        }
        throw new InvalidOperationException("Não foi possível gerar um código único para o draft.");
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private static string NormalizeCode(string code) => code.Trim().ToUpperInvariant();

    private sealed record DraftRow(Guid Id, string ShareCode, Guid CreatorPlayerProfileId, Guid GameEditionId, int Version, DraftState State, DateTimeOffset UpdatedAtUtc);
    private sealed class DraftState
    {
        public string Title { get; set; } = "";
        public string EditionName { get; set; } = "";
        public string? MapName { get; set; }
        public MatchGameMode GameMode { get; set; }
        public CivilizationDraftPhase Phase { get; set; }
        public List<TeamState> Teams { get; set; } = [];
        public List<FactionState> Pool { get; set; } = [];
        public List<Guid> Queue { get; set; } = [];
        public int ActionIndex { get; set; }
        public List<Guid> BannedFactionIds { get; set; } = [];
        public List<Guid> ProtectedFactionIds { get; set; } = [];
    }
    private sealed class TeamState
    {
        public string Name { get; set; } = "";
        public List<ParticipantState> Players { get; set; } = [];
    }
    private sealed class ParticipantState
    {
        public Guid PlayerProfileId { get; set; }
        public string DisplayName { get; set; } = "";
        public string? AvatarUrl { get; set; }
        public bool Ready { get; set; }
        public List<Guid> PreferenceFactionIds { get; set; } = [];
        public Guid? PickFactionId { get; set; }
        public bool PickWasProtected { get; set; }
        public bool FinalConfirmed { get; set; }
    }
    private sealed class FactionState
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = "";
        public string Slug { get; set; } = "";
        public string? ImageUrl { get; set; }
    }
}
