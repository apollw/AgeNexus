namespace AgeNexus.Domain.Matches;

public enum MatchType
{
    PlayerVersusPlayer,
    HumansVersusAi,
    Mixed,
    FreeForAll
}

public enum MatchStatus
{
    Draft,
    Submitted,
    AwaitingConfirmation,
    Confirmed,
    Disputed,
    Validated,
    Voided
}

public enum MatchNature
{
    Casual,
    Ranked,
    Tournament,
    Series
}

public enum ParticipantType
{
    Human,
    ArtificialIntelligence
}

public enum FactionSelection
{
    Unknown,
    Manual,
    Random
}

public enum TeamResult
{
    Undecided,
    Victory,
    Draw,
    Defeat
}
