// Contract definition — not compilable source code.
// Defines the public API for diagnostics bundle creation (FR-016, FR-017).

namespace NeatSharp.Serialization;

/// <summary>
/// Creates a single-call diagnostics bundle containing a checkpoint, configuration,
/// environment metadata, and run history. Designed for reproducible bug reports.
/// </summary>
/// <remarks>
/// Registered as a singleton by <c>AddNeatSharp()</c>.
/// </remarks>
public interface IDiagnosticsBundleCreator
{
    /// <summary>
    /// Creates a diagnostics bundle from a training checkpoint and writes it to a stream.
    /// The bundle is a single JSON document containing all information needed to
    /// reproduce the training run.
    /// </summary>
    /// <param name="stream">The output stream. Must be writable.</param>
    /// <param name="checkpoint">
    /// The training checkpoint to include. Contains the population state,
    /// configuration, and run history.
    /// </param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="stream"/> or <paramref name="checkpoint"/> is null.
    /// </exception>
    Task CreateAsync(Stream stream, TrainingCheckpoint checkpoint, CancellationToken cancellationToken = default);
}
