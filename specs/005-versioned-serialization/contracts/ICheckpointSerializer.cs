// Contract definition — not compilable source code.
// Defines the public API for checkpoint serialization (FR-001, FR-002, FR-003, FR-018, FR-019).

namespace NeatSharp.Serialization;

/// <summary>
/// Serializes and deserializes training checkpoints to and from streams.
/// All operations use System.Text.Json and are stream-based with no filesystem dependency.
/// </summary>
/// <remarks>
/// Registered as a singleton by <c>AddNeatSharp()</c>.
/// Thread-safe: serialization is stateless.
/// </remarks>
public interface ICheckpointSerializer
{
    /// <summary>
    /// Serializes a training checkpoint to the specified stream as JSON.
    /// </summary>
    /// <param name="stream">The output stream. Must be writable. Not closed by this method.</param>
    /// <param name="checkpoint">The checkpoint to serialize.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="stream"/> or <paramref name="checkpoint"/> is null.
    /// </exception>
    Task SaveAsync(Stream stream, TrainingCheckpoint checkpoint, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deserializes a training checkpoint from the specified stream.
    /// Performs full structural validation (FR-023) before returning.
    /// </summary>
    /// <param name="stream">The input stream containing JSON. Must be readable.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A validated training checkpoint ready for resume.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="stream"/> is null.
    /// </exception>
    /// <exception cref="SchemaVersionException">
    /// Thrown when the artifact's schema version is incompatible with the current version.
    /// Includes the artifact version, expected version, and migration availability.
    /// </exception>
    /// <exception cref="CheckpointCorruptionException">
    /// Thrown when structural validation fails. Includes all validation errors.
    /// </exception>
    /// <exception cref="CheckpointException">
    /// Thrown when the JSON is malformed or missing required fields.
    /// </exception>
    Task<TrainingCheckpoint> LoadAsync(Stream stream, CancellationToken cancellationToken = default);
}
