namespace NeatSharp.Serialization;

/// <summary>
/// Serializes and deserializes <see cref="TrainingCheckpoint"/> instances
/// to and from streams using JSON format with schema version management.
/// </summary>
public interface ICheckpointSerializer
{
    /// <summary>
    /// Serializes a training checkpoint to the specified stream.
    /// </summary>
    /// <param name="stream">The stream to write to.</param>
    /// <param name="checkpoint">The checkpoint to serialize.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    public Task SaveAsync(Stream stream, TrainingCheckpoint checkpoint, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deserializes a training checkpoint from the specified stream.
    /// Performs schema version checking, optional migration, and structural validation.
    /// </summary>
    /// <param name="stream">The stream to read from.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A validated <see cref="TrainingCheckpoint"/> instance.</returns>
    /// <exception cref="Exceptions.SchemaVersionException">
    /// Thrown when the schema version is incompatible and no migration path is available.
    /// </exception>
    /// <exception cref="Exceptions.CheckpointCorruptionException">
    /// Thrown when the loaded checkpoint fails structural validation.
    /// </exception>
    public Task<TrainingCheckpoint> LoadAsync(Stream stream, CancellationToken cancellationToken = default);
}
