namespace Ironbell.Domain.Training;

/// <summary>
/// A block after expansion: its steps, plus any limit that governs the block as a whole.
/// </summary>
/// <param name="Name">The block's name, carried through for display.</param>
/// <param name="Limit">
/// A ceiling on the whole block, or <see langword="null"/> when there is none.
/// </param>
/// <param name="Steps">The steps in the order they are performed.</param>
/// <remarks>
/// This type exists because some timing belongs to a block rather than to any step in it. An AMRAP
/// window and a ForTime cap both bound a group of steps without prescribing how long any single one
/// takes. Flattening those into step durations would have meant inventing lengths for work that has
/// none — the exact fiction <see cref="TimelineStep.Duration"/> is nullable to avoid.
/// </remarks>
public sealed record ResolvedBlock(string Name, TimeSpan? Limit, IReadOnlyList<TimelineStep> Steps);

/// <summary>A session expanded into the blocks and steps the training screen walks through.</summary>
public sealed record ResolvedSession(string Name, IReadOnlyList<ResolvedBlock> Blocks)
{
    /// <summary>Every step in session order, ignoring block boundaries.</summary>
    public IEnumerable<TimelineStep> Steps => Blocks.SelectMany(block => block.Steps);

    /// <summary>
    /// Total weight the session prescribes. Only counts work with a known rep count, so an AMRAP
    /// contributes nothing until it has actually been performed.
    /// </summary>
    public decimal PrescribedTonnage => Steps.Sum(step => step.PrescribedTonnage);
}
