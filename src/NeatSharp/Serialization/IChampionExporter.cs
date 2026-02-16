using NeatSharp.Evolution;

namespace NeatSharp.Serialization;

/// <summary>
/// Exports the champion genome as a self-describing JSON graph suitable for
/// interoperability with external tools. The output is parseable by any standard
/// JSON reader without requiring the NEATSharp library.
/// </summary>
/// <remarks>
/// Registered as a singleton by <c>AddNeatSharp()</c>.
/// </remarks>
public interface IChampionExporter
{
    /// <summary>
    /// Exports the champion from an evolution result to a JSON stream.
    /// </summary>
    /// <param name="stream">The output stream. Must be writable.</param>
    /// <param name="result">The evolution result containing the champion to export.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="stream"/> or <paramref name="result"/> is null.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when <paramref name="result"/> does not contain champion genotype information.
    /// Use the <see cref="ExportAsync(Stream, TrainingCheckpoint, CancellationToken)"/> overload
    /// with a <see cref="TrainingCheckpoint"/> instead.
    /// </exception>
    Task ExportAsync(Stream stream, EvolutionResult result, CancellationToken cancellationToken = default);

    /// <summary>
    /// Exports a champion from a training checkpoint to a JSON stream.
    /// </summary>
    /// <param name="stream">The output stream. Must be writable.</param>
    /// <param name="checkpoint">The checkpoint containing the champion to export.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="stream"/> or <paramref name="checkpoint"/> is null.
    /// </exception>
    Task ExportAsync(Stream stream, TrainingCheckpoint checkpoint, CancellationToken cancellationToken = default);
}
