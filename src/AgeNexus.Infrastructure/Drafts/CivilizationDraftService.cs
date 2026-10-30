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
    private static readonly TimeSpan TurnDuration = TimeSpan.FromSeconds(60);

    public async Task<CivilizationDraftOperationResult> CreateAsync(
        Guid creatorPlayerProfileId,
        CreateCivilizationDraftRequest request,
        CancellationToken cancellationToken = default)
    {
        var title = request.Title.Trim();
        var teams = request.Teams.Where(x => x.PlayerProfileIds.Count > 0).ToArray();
        var playerIds = teams.SelectMany(x => x.PlayerProfileIds).ToArray();
        var balancedTeams = teams.Length == 2 && teams[0].PlayerProfileIds.Count == teams[1].PlayerProfileIds.Count;
        var validRepresentatives = teams.All(x =>
            x.RepresentativePlayerProfileId != Guid.Empty && x.PlayerProfileIds.Contains(x.RepresentativePlayerProfileId));
        if (title.Length is 0 or > 120 || !balancedTeams || teams[0].PlayerProfileIds.Count is < 1 or > 4 ||
            playerIds.Distinct().Count() != playerIds.Length || !validRepresentatives ||
            teams.Select(x => x.RepresentativePlayerProfileId).Distinct().Count() != 2 ||
            teams.Any(x => x.Name.Trim().Length is 0 or > 40))
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

        var factions = await LoadFactionsAsync(request.GameEditionId, cancellationToken);
        if (factions.Count < playerIds.Length * 2)
        {
            return CivilizationDraftOperationResult.Failure("InsufficientFactions");
        }

        var state = new DraftState
        {
            Title = title,
            EditionName = edition.Name,
            MapName = map?.Name,
            GameMode = request.GameMode,
            Phase = CivilizationDraftPhase.Lobby,
            Pool = factions,
            Teams = teams.Select(team => new TeamState
            {
                Name = team.Name.Trim(),
                RepresentativePlayerProfileId = team.RepresentativePlayerProfileId,
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

    public async Task<CivilizationDraftView?> GetAsync(
        string shareCode,
        Guid viewerPlayerProfileId,
        CancellationToken cancellationToken = default)
    {
        await ResolveExpiredTurnAsync(shareCode, cancellationToken);
        var row = await GetRowAsync(shareCode, cancellationToken);
        if (row is null)
        {
            return null;
        }

        await CompletePoolAsync(row, cancellationToken);
        return ToView(row);
    }

    public Task<CivilizationDraftOperationResult> SetReadyAsync(
        string shareCode, Guid playerProfileId, bool ready, CancellationToken cancellationToken = default)
        => MutateAsync(shareCode, playerProfileId, (_, state) =>
        {
            if (state.Phase != CivilizationDraftPhase.Lobby)
            {
                return "InvalidPhase";
            }

            var team = FindRepresentedTeam(state, playerProfileId);
            if (team is null)
            {
                return "RepresentativeOnly";
            }

            team.RepresentativeReady = ready;
            return null;
        }, cancellationToken);

    public Task<CivilizationDraftOperationResult> StartAsync(
        string shareCode, Guid organizerPlayerProfileId, CancellationToken cancellationToken = default)
        => MutateAsync(shareCode, organizerPlayerProfileId, (row, state) =>
        {
            if (row.CreatorPlayerProfileId != organizerPlayerProfileId)
            {
                return "OrganizerOnly";
            }

            if (state.Phase != CivilizationDraftPhase.Lobby)
            {
                return "InvalidPhase";
            }

            if (state.Teams.Count != 2 || state.Teams.Any(x => !x.RepresentativeReady))
            {
                return "RepresentativesNotReady";
            }

            var firstTeamIndex = Random.Shared.Next(state.Teams.Count);
            state.Turns = CivilizationDraftTurnPlanner.Build(
                    state.Teams.Select(x => x.RepresentativePlayerProfileId).ToArray(),
                    state.Teams[0].Players.Count,
                    firstTeamIndex)
                .Select(x => new TurnState { RepresentativePlayerProfileId = x.RepresentativePlayerProfileId, Kind = x.Kind })
                .ToList();
            state.Queue.Clear();
            state.ActionIndex = 0;
            SetCurrentTurn(state, DateTimeOffset.UtcNow);
            return null;
        }, cancellationToken);

    public Task<CivilizationDraftOperationResult> SubmitBanAsync(
        string shareCode, Guid playerProfileId, Guid factionId, CancellationToken cancellationToken = default)
        => MutateAsync(shareCode, playerProfileId, (_, state) =>
        {
            if (CurrentAction(state) != CivilizationDraftActionKind.Ban || CurrentRepresentative(state) != playerProfileId)
            {
                return "NotYourTurn";
            }

            if (!IsFactionAvailable(state, factionId))
            {
                return "FactionUnavailable";
            }

            ApplyAction(state, playerProfileId, factionId, CivilizationDraftActionKind.Ban, false, DateTimeOffset.UtcNow);

            return null;
        }, cancellationToken);

    public Task<CivilizationDraftOperationResult> SubmitPickAsync(
        string shareCode,
        Guid playerProfileId,
        Guid factionId,
        CancellationToken cancellationToken = default)
        => MutateAsync(shareCode, playerProfileId, (_, state) =>
        {
            if (CurrentAction(state) != CivilizationDraftActionKind.Pick || CurrentRepresentative(state) != playerProfileId)
            {
                return "NotYourTurn";
            }

            var team = FindRepresentedTeam(state, playerProfileId);
            if (team is null || team.PickedFactionIds.Count >= team.Players.Count)
            {
                return "InvalidPick";
            }

            if (!IsFactionAvailable(state, factionId))
            {
                return "FactionUnavailable";
            }

            ApplyAction(state, playerProfileId, factionId, CivilizationDraftActionKind.Pick, false, DateTimeOffset.UtcNow);

            return null;
        }, cancellationToken);

    public Task<CivilizationDraftOperationResult> CancelAsync(
        string shareCode, Guid organizerPlayerProfileId, CancellationToken cancellationToken = default)
        => MutateAsync(shareCode, organizerPlayerProfileId, (row, state) =>
        {
            if (row.CreatorPlayerProfileId != organizerPlayerProfileId)
            {
                return "OrganizerOnly";
            }

            if (state.Phase is CivilizationDraftPhase.Completed or CivilizationDraftPhase.Cancelled)
            {
                return "InvalidPhase";
            }

            state.Phase = CivilizationDraftPhase.Cancelled;
            state.TurnDeadlineUtc = null;
            return null;
        }, cancellationToken);

    private async Task ResolveExpiredTurnAsync(string shareCode, CancellationToken cancellationToken)
    {
        var row = await GetRowAsync(shareCode, cancellationToken);
        if (row is null)
        {
            return;
        }

        NormalizeLegacyState(row.State);
        if (row.State.TurnDeadlineUtc is null || row.State.TurnDeadlineUtc > DateTimeOffset.UtcNow || CurrentAction(row.State) is null)
        {
            return;
        }

        await MutateAsync(shareCode, Guid.Empty, (_, state) =>
        {
            var now = DateTimeOffset.UtcNow;
            if (state.TurnDeadlineUtc is null || state.TurnDeadlineUtc > now || CurrentAction(state) is not { } action)
            {
                return "TurnStillActive";
            }

            var representativeId = CurrentRepresentative(state);
            var available = state.Pool.Where(x => IsFactionAvailable(state, x.Id)).ToArray();
            if (!representativeId.HasValue || available.Length == 0)
            {
                state.Phase = CivilizationDraftPhase.Completed;
                state.TurnDeadlineUtc = null;
                return null;
            }

            var faction = available[Random.Shared.Next(available.Length)];
            ApplyAction(state, representativeId.Value, faction.Id, action, true, now);
            return null;
        }, cancellationToken);
    }

    private static void ApplyAction(
        DraftState state,
        Guid representativeId,
        Guid factionId,
        CivilizationDraftActionKind action,
        bool automatic,
        DateTimeOffset occurredAtUtc)
    {
        var team = FindRepresentedTeam(state, representativeId)
            ?? throw new InvalidOperationException("A equipe da vez não foi encontrada.");
        var faction = state.Pool.Single(x => x.Id == factionId);

        if (action == CivilizationDraftActionKind.Ban)
        {
            state.BannedFactionIds.Add(factionId);
            state.Bans.Add(new DraftBanState { FactionId = factionId, RepresentativePlayerProfileId = representativeId });
        }
        else
        {
            team.PickedFactionIds.Add(factionId);
        }

        state.LastAction = new LastActionState
        {
            Kind = action,
            RepresentativePlayerProfileId = representativeId,
            FactionId = factionId,
            WasAutomatic = automatic,
            OccurredAtUtc = occurredAtUtc
        };

        state.ActionIndex++;
        if (state.Turns.Count > 0)
        {
            if (state.ActionIndex >= state.Turns.Count)
            {
                state.Phase = CivilizationDraftPhase.Completed;
                state.TurnDeadlineUtc = null;
            }
            else
            {
                SetCurrentTurn(state, occurredAtUtc);
            }

            return;
        }

        // Compatibilidade com salas iniciadas antes do novo fluxo intercalado.
        if (state.ActionIndex >= state.Queue.Count)
        {
            if (state.Phase == CivilizationDraftPhase.Bans)
            {
                state.Phase = CivilizationDraftPhase.Picks;
                state.Queue = BuildLegacyQueue(state.Teams);
                state.ActionIndex = 0;
            }
            else
            {
                state.Phase = CivilizationDraftPhase.Completed;
            }
        }
    }

    private static void SetCurrentTurn(DraftState state, DateTimeOffset now)
    {
        var action = CurrentAction(state);
        state.Phase = action == CivilizationDraftActionKind.Ban
            ? CivilizationDraftPhase.Bans
            : CivilizationDraftPhase.Picks;
        state.TurnDeadlineUtc = now.Add(TurnDuration);
    }

    private static List<Guid> BuildLegacyQueue(IReadOnlyList<TeamState> teams)
    {
        var queue = new List<Guid>(teams[0].Players.Count * 2);
        for (var round = 0; round < teams[0].Players.Count; round++)
        {
            queue.Add(teams[0].RepresentativePlayerProfileId);
            queue.Add(teams[1].RepresentativePlayerProfileId);
        }

        return queue;
    }

    private async Task<CivilizationDraftOperationResult> MutateAsync(
        string shareCode,
        Guid actorId,
        Func<DraftRow, DraftState, string?> mutation,
        CancellationToken cancellationToken)
    {
        shareCode = NormalizeCode(shareCode);
        var gate = Locks.GetOrAdd(shareCode, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            var row = await GetRowAsync(shareCode, cancellationToken);
            if (row is null)
            {
                return CivilizationDraftOperationResult.Failure("NotFound");
            }

            await CompletePoolAsync(row, cancellationToken);
            NormalizeLegacyState(row.State);
            var error = mutation(row, row.State);
            if (error is not null)
            {
                return CivilizationDraftOperationResult.Failure(error);
            }

            var now = DateTimeOffset.UtcNow;
            var affected = await database.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE public.civilization_drafts
                SET status = {row.State.Phase.ToString()}, version = version + 1,
                    state_json = {JsonSerializer.Serialize(row.State, JsonOptions)}::jsonb, updated_at_utc = {now}
                WHERE id = {row.Id} AND version = {row.Version}
                """, cancellationToken);
            if (affected != 1)
            {
                return CivilizationDraftOperationResult.Failure("ConcurrentChange");
            }
        }
        finally
        {
            gate.Release();
        }

        await notifier.NotifyAsync(shareCode);
        return CivilizationDraftOperationResult.Success(shareCode);
    }

    private static Guid? CurrentRepresentative(DraftState state) =>
        state.ActionIndex >= 0 && state.ActionIndex < state.Turns.Count
            ? state.Turns[state.ActionIndex].RepresentativePlayerProfileId
            : (state.Phase is CivilizationDraftPhase.Bans or CivilizationDraftPhase.Picks) && state.ActionIndex < state.Queue.Count
                ? state.Queue[state.ActionIndex]
                : null;

    private static CivilizationDraftActionKind? CurrentAction(DraftState state) =>
        state.ActionIndex >= 0 && state.ActionIndex < state.Turns.Count
            ? state.Turns[state.ActionIndex].Kind
            : state.Phase switch
            {
                CivilizationDraftPhase.Bans => CivilizationDraftActionKind.Ban,
                CivilizationDraftPhase.Picks => CivilizationDraftActionKind.Pick,
                _ => null
            };

    private static TeamState? FindRepresentedTeam(DraftState state, Guid representativeId) =>
        state.Teams.SingleOrDefault(x => x.RepresentativePlayerProfileId == representativeId);

    private static bool IsFactionAvailable(DraftState state, Guid factionId) =>
        state.Pool.Any(x => x.Id == factionId) &&
        !state.BannedFactionIds.Contains(factionId) &&
        state.Teams.All(x => !x.PickedFactionIds.Contains(factionId)) &&
        state.Teams.SelectMany(x => x.Players).All(x => x.PickFactionId != factionId);

    private CivilizationDraftView ToView(DraftRow row)
    {
        var state = row.State;
        NormalizeLegacyState(state);
        var allPlayers = state.Teams.SelectMany(x => x.Players).ToArray();
        var factions = state.Pool.OrderBy(x => x.Name).Select(faction =>
        {
            var pickedPlayer = allPlayers.SingleOrDefault(x => x.PickFactionId == faction.Id);
            var pickedTeam = state.Teams.SingleOrDefault(x => x.PickedFactionIds.Contains(faction.Id))
                ?? (pickedPlayer is null ? null : state.Teams.Single(x => x.Players.Contains(pickedPlayer)));
            var ban = state.Bans.LastOrDefault(x => x.FactionId == faction.Id);
            var banningTeam = ban is null ? null : FindRepresentedTeam(state, ban.RepresentativePlayerProfileId);
            return new CivilizationDraftFactionView(
                faction.Id,
                faction.Name,
                faction.ImageUrl ?? CivilizationImagePath(faction.Slug),
                state.BannedFactionIds.Contains(faction.Id),
                pickedTeam is not null,
                pickedPlayer?.DisplayName,
                pickedTeam?.Name,
                banningTeam?.Name);
        }).ToArray();

        CivilizationDraftParticipantView MapPlayer(ParticipantState player)
        {
            var picked = player.PickFactionId.HasValue ? state.Pool.SingleOrDefault(x => x.Id == player.PickFactionId.Value) : null;
            return new CivilizationDraftParticipantView(
                player.PlayerProfileId,
                player.DisplayName,
                player.AvatarUrl,
                player.PickFactionId,
                picked?.Name,
                picked is null ? null : picked.ImageUrl ?? CivilizationImagePath(picked.Slug));
        }

        var teams = state.Teams.Select(team =>
        {
            var representative = team.Players.Single(x => x.PlayerProfileId == team.RepresentativePlayerProfileId);
            var picks = team.PickedFactionIds
                .Concat(team.Players.Where(x => x.PickFactionId.HasValue).Select(x => x.PickFactionId!.Value))
                .Distinct()
                .Select(id => state.Pool.SingleOrDefault(x => x.Id == id))
                .Where(x => x is not null)
                .Select(x => new CivilizationDraftTeamPickView(
                    x!.Id,
                    x.Name,
                    x.ImageUrl ?? CivilizationImagePath(x.Slug)))
                .ToArray();
            return new CivilizationDraftTeamView(
                team.Name,
                team.RepresentativePlayerProfileId,
                representative.DisplayName,
                team.RepresentativeReady,
                team.Players.Select(MapPlayer).ToArray(),
                picks);
        }).ToArray();
        var actionsPerPhase = state.Teams.Count == 2 ? state.Teams[0].Players.Count * 2 : 0;
        var completedActions = state.Turns.Count > 0
            ? Math.Min(state.ActionIndex, state.Turns.Count)
            : state.Phase switch
        {
            CivilizationDraftPhase.Lobby => state.Teams.Count(x => x.RepresentativeReady),
            CivilizationDraftPhase.Bans => state.ActionIndex,
            CivilizationDraftPhase.Picks => state.BannedFactionIds.Count + state.ActionIndex,
            CivilizationDraftPhase.Completed => actionsPerPhase * 2,
            _ => 0
        };
        var totalActions = state.Phase == CivilizationDraftPhase.Lobby ? 2 : state.Turns.Count > 0 ? state.Turns.Count : actionsPerPhase * 2;
        CivilizationDraftActionView? lastAction = null;
        if (state.LastAction is not null)
        {
            var actionTeam = FindRepresentedTeam(state, state.LastAction.RepresentativePlayerProfileId);
            var actionFaction = state.Pool.SingleOrDefault(x => x.Id == state.LastAction.FactionId);
            if (actionTeam is not null && actionFaction is not null)
            {
                lastAction = new CivilizationDraftActionView(
                    state.LastAction.Kind,
                    actionTeam.Name,
                    actionFaction.Name,
                    state.LastAction.WasAutomatic,
                    state.LastAction.OccurredAtUtc);
            }
        }

        return new CivilizationDraftView(
            row.Id, row.ShareCode, state.Title, state.EditionName, state.MapName, state.GameMode, state.Phase,
            row.CreatorPlayerProfileId, CurrentRepresentative(state), CurrentAction(state), state.TurnDeadlineUtc, lastAction,
            teams, factions, completedActions,
            totalActions, row.Version, row.UpdatedAtUtc);
    }

    private async Task CompletePoolAsync(DraftRow row, CancellationToken cancellationToken)
    {
        var knownIds = row.State.Pool.Select(x => x.Id).ToHashSet();
        var catalog = await LoadFactionsAsync(row.GameEditionId, cancellationToken);
        row.State.Pool.AddRange(catalog.Where(x => !knownIds.Contains(x.Id)));
        row.State.Pool = row.State.Pool.OrderBy(x => x.Name).ToList();
    }

    private async Task<List<FactionState>> LoadFactionsAsync(Guid editionId, CancellationToken cancellationToken) =>
        await database.Factions.AsNoTracking()
            .Where(x => x.GameEditionId == editionId)
            .OrderBy(x => x.Name)
            .Select(x => new FactionState { Id = x.Id, Name = x.Name, Slug = x.Slug, ImageUrl = x.ImageUrl })
            .ToListAsync(cancellationToken);

    private static void NormalizeLegacyState(DraftState state)
    {
        var legacyState = state.Teams.Any(x => x.RepresentativePlayerProfileId == Guid.Empty);
        foreach (var team in state.Teams)
        {
            if (team.RepresentativePlayerProfileId == Guid.Empty && team.Players.Count > 0)
            {
                team.RepresentativePlayerProfileId = team.Players[0].PlayerProfileId;
            }
        }

        if (legacyState && state.Teams.Count == 2 && state.Teams[0].Players.Count == state.Teams[1].Players.Count)
        {
            state.Phase = CivilizationDraftPhase.Lobby;
            state.Queue.Clear();
            state.Turns.Clear();
            state.ActionIndex = 0;
            state.BannedFactionIds.Clear();
            state.Bans.Clear();
            state.LastAction = null;
            state.TurnDeadlineUtc = null;
            foreach (var team in state.Teams)
            {
                team.RepresentativeReady = false;
                team.PickedFactionIds.Clear();
                foreach (var player in team.Players)
                {
                    player.PickFactionId = null;
                }
            }
        }
    }

    private async Task<DraftRow?> GetRowAsync(string shareCode, CancellationToken cancellationToken)
    {
        await using var command = database.Database.GetDbConnection().CreateCommand();
        command.CommandText = """
            SELECT id, share_code, created_by_player_profile_id, game_edition_id, version, state_json::text, updated_at_utc
            FROM public.civilization_drafts WHERE share_code = @code LIMIT 1
            """;
        AddParameter(command, "@code", NormalizeCode(shareCode));
        if (command.Connection!.State != ConnectionState.Open)
        {
            await command.Connection.OpenAsync(cancellationToken);
        }

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new DraftRow(
            reader.GetGuid(0), reader.GetString(1), reader.GetGuid(2), reader.GetGuid(3), reader.GetInt32(4),
            JsonSerializer.Deserialize<DraftState>(reader.GetString(5), JsonOptions)!,
            new DateTimeOffset(DateTime.SpecifyKind(reader.GetDateTime(6), DateTimeKind.Utc)));
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
            if (command.Connection!.State != ConnectionState.Open)
            {
                await command.Connection.OpenAsync(cancellationToken);
            }

            if (!(bool)(await command.ExecuteScalarAsync(cancellationToken))!)
            {
                return code;
            }
        }

        throw new InvalidOperationException("Não foi possível gerar um código único para o draft.");
    }

    private static string CivilizationImagePath(string slug) =>
        $"/images/civilizations/{(string.Equals(slug, "indians", StringComparison.OrdinalIgnoreCase) ? "hindustanis" : slug)}.png";

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private static string NormalizeCode(string code) => code.Trim().ToUpperInvariant();

    private sealed record DraftRow(
        Guid Id, string ShareCode, Guid CreatorPlayerProfileId, Guid GameEditionId, int Version,
        DraftState State, DateTimeOffset UpdatedAtUtc);

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
        public List<TurnState> Turns { get; set; } = [];
        public int ActionIndex { get; set; }
        public List<Guid> BannedFactionIds { get; set; } = [];
        public List<DraftBanState> Bans { get; set; } = [];
        public DateTimeOffset? TurnDeadlineUtc { get; set; }
        public LastActionState? LastAction { get; set; }
    }

    private sealed class TeamState
    {
        public string Name { get; set; } = "";
        public Guid RepresentativePlayerProfileId { get; set; }
        public bool RepresentativeReady { get; set; }
        public List<ParticipantState> Players { get; set; } = [];
        public List<Guid> PickedFactionIds { get; set; } = [];
    }

    private sealed class TurnState
    {
        public Guid RepresentativePlayerProfileId { get; set; }
        public CivilizationDraftActionKind Kind { get; set; }
    }

    private sealed class DraftBanState
    {
        public Guid FactionId { get; set; }
        public Guid RepresentativePlayerProfileId { get; set; }
    }

    private sealed class LastActionState
    {
        public CivilizationDraftActionKind Kind { get; set; }
        public Guid RepresentativePlayerProfileId { get; set; }
        public Guid FactionId { get; set; }
        public bool WasAutomatic { get; set; }
        public DateTimeOffset OccurredAtUtc { get; set; }
    }

    private sealed class ParticipantState
    {
        public Guid PlayerProfileId { get; set; }
        public string DisplayName { get; set; } = "";
        public string? AvatarUrl { get; set; }
        public Guid? PickFactionId { get; set; }
    }

    private sealed class FactionState
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = "";
        public string Slug { get; set; } = "";
        public string? ImageUrl { get; set; }
    }
}
