using NeatSharp.Serialization;

namespace NeatSharp.Evolution;

/// <summary>
/// Options for controlling an evolution run, including checkpoint resume and callbacks.
/// </summary>
public class EvolutionRunOptions
{
    /// <summary>
    /// Gets or sets a checkpoint to resume training from.
    /// When not null, the evolution run resumes from this checkpoint state
    /// instead of starting from scratch.
    /// </summary>
    public TrainingCheckpoint? ResumeFrom { get; set; }

    /// <summary>
    /// Gets or sets a callback invoked at each generation boundary after speciation.
    /// The callback receives a snapshot of the current training state as a
    /// <see cref="TrainingCheckpoint"/> and a <see cref="CancellationToken"/>.
    /// </summary>
    public Func<TrainingCheckpoint, CancellationToken, Task>? OnCheckpoint { get; set; }
}
