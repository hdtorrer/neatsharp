namespace NeatSharp.Serialization;

/// <summary>
/// Creates a diagnostics bundle containing the full checkpoint, configuration,
/// environment metadata, and run history in a single JSON document suitable for
/// bug reports and troubleshooting.
/// </summary>
/// <remarks>
/// Registered as a singleton by <c>AddNeatSharp()</c>.
/// </remarks>
public interface IDiagnosticsBundleCreator
{
    /// <summary>
    /// Creates a diagnostics bundle from a training checkpoint and writes it to the specified stream.
    /// </summary>
    /// <param name="stream">The output stream. Must be writable.</param>
    /// <param name="checkpoint">The training checkpoint to include in the bundle.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="stream"/> or <paramref name="checkpoint"/> is null.
    /// </exception>
    Task CreateAsync(Stream stream, TrainingCheckpoint checkpoint, CancellationToken cancellationToken = default);
}
