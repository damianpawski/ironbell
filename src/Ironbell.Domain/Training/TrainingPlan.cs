namespace Ironbell.Domain.Training;

/// <summary>A programme: ordered weeks of sessions.</summary>
/// <remarks>
/// Plain data with no identity or persistence concerns. A session freezes the plan it was trained
/// against into <c>plan_snapshot</c>, so editing a plan can never retroactively rewrite history —
/// which only works if this shape stays serialisable and free of references to anything live.
/// </remarks>
public sealed record TrainingPlan(string Name, IReadOnlyList<TrainingWeek> Weeks);

/// <param name="Ordinal">Week number within the plan, from one.</param>
/// <param name="Sessions">Sessions in the order they are trained.</param>
public sealed record TrainingWeek(int Ordinal, IReadOnlyList<TrainingSession> Sessions);

public sealed record TrainingSession(string Name, IReadOnlyList<TrainingBlock> Blocks);
