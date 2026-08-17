namespace Ironbell.Domain.Training;

/// <summary>
/// A unit of work inside a session.
/// </summary>
/// <remarks>
/// A closed hierarchy of nine specific types rather than a generic sets-and-reps abstraction. That
/// is the central modelling decision of the app: an EMOM and a ladder are not the same shape with
/// different numbers, and flattening them would push the difference into the UI where it cannot be
/// tested.
/// </remarks>
public abstract record TrainingBlock(string Name);

/// <summary>A movement performed for a number of reps, as part of a larger block.</summary>
public sealed record Effort(string Exercise, int Reps, BellWeight Weight);

// --- rep-driven: the athlete's pace decides how long these take ---------------------------------

/// <summary>Fixed sets of fixed reps, with rest between them.</summary>
public sealed record StraightBlock(
    string Name,
    string Exercise,
    int Sets,
    int Reps,
    BellWeight Weight,
    TimeSpan Rest) : TrainingBlock(Name);

/// <summary>Ordered stations, repeated for rounds, with rest between rounds.</summary>
public sealed record CircuitBlock(
    string Name,
    IReadOnlyList<Effort> Stations,
    int Rounds,
    TimeSpan RestBetweenRounds) : TrainingBlock(Name);

/// <summary>
/// An ascending or descending rep scheme, e.g. 1-2-3 repeated for rounds.
/// </summary>
public sealed record LadderBlock(
    string Name,
    string Exercise,
    BellWeight Weight,
    IReadOnlyList<int> Rungs,
    int Rounds,
    TimeSpan RestBetweenRounds) : TrainingBlock(Name);

/// <summary>
/// Several movements on one bell, all reps of each before moving on, without setting it down.
/// </summary>
/// <remarks>
/// The distinction from <see cref="ChainBlock"/> is not cosmetic and is why both exist: a complex
/// finishes every rep of a movement before the next, a chain cycles through the movements. They
/// expand into different step lists from the same numbers.
/// </remarks>
public sealed record ComplexBlock(
    string Name,
    IReadOnlyList<Effort> Movements,
    int Sets,
    TimeSpan Rest) : TrainingBlock(Name);

/// <summary>
/// Several movements cycled through as one continuous set, repeated for sets.
/// </summary>
public sealed record ChainBlock(
    string Name,
    IReadOnlyList<Effort> Links,
    int Sets,
    TimeSpan Rest) : TrainingBlock(Name);

// --- clock-driven: these have known offsets before the session starts ---------------------------

/// <summary>
/// Every minute on the minute. The rotation cycles across the window, so alternating work is one
/// block rather than several.
/// </summary>
public sealed record EmomBlock(
    string Name,
    int Rounds,
    TimeSpan Interval,
    IReadOnlyList<Effort> Rotation) : TrainingBlock(Name);

/// <summary>As many rounds as possible of a fixed sequence, inside a fixed window.</summary>
public sealed record AmrapBlock(
    string Name,
    TimeSpan Window,
    IReadOnlyList<Effort> Round) : TrainingBlock(Name);

/// <summary>Timed work alternating with timed rest, for a number of rounds.</summary>
public sealed record IntervalBlock(
    string Name,
    string Exercise,
    BellWeight Weight,
    TimeSpan Work,
    TimeSpan Rest,
    int Rounds) : TrainingBlock(Name);

/// <summary>
/// Fixed work, raced against the clock, optionally capped.
/// </summary>
/// <remarks>
/// Clock-driven only in the sense that the clock is running and observed. The work itself still
/// advances when the athlete logs it, so its steps carry no duration; the cap does.
/// </remarks>
public sealed record ForTimeBlock(
    string Name,
    IReadOnlyList<Effort> Tasks,
    TimeSpan? Cap) : TrainingBlock(Name);
