// Contract definition — not compilable source code.
// Defines the public API for checkpoint structural validation (FR-023).

namespace NeatSharp.Serialization;

/// <summary>
/// Validates the structural integrity of a deserialized training checkpoint.
/// Checks internal reference consistency, counter integrity, and champion existence.
/// </summary>
/// <remarks>
/// Called internally by <see cref="ICheckpointSerializer.LoadAsync"/> after deserialization.
/// Also available for direct use by consumers who construct checkpoints programmatically.
/// Registered as a singleton by <c>AddNeatSharp()</c>.
/// </remarks>
public interface ICheckpointValidator
{
    /// <summary>
    /// Validates the structural integrity of a training checkpoint.
    /// </summary>
    /// <param name="checkpoint">The checkpoint to validate.</param>
    /// <returns>
    /// A validation result containing any errors found. An empty error list
    /// indicates a valid checkpoint.
    /// </returns>
    CheckpointValidationResult Validate(TrainingCheckpoint checkpoint);
}

/// <summary>
/// Result of checkpoint structural validation.
/// </summary>
/// <param name="Errors">
/// List of validation errors. Empty if the checkpoint is structurally valid.
/// Each error is a human-readable description of the integrity violation.
/// </param>
public record CheckpointValidationResult(IReadOnlyList<string> Errors)
{
    /// <summary>
    /// Returns <c>true</c> if the checkpoint passed all validation checks.
    /// </summary>
    public bool IsValid => Errors.Count == 0;
}
