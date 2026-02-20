namespace NeatSharp.Serialization;

/// <summary>
/// Validates the structural integrity of a <see cref="TrainingCheckpoint"/>
/// by checking cross-references between population, species, and counters.
/// </summary>
public interface ICheckpointValidator
{
    /// <summary>
    /// Validates the specified checkpoint for structural integrity.
    /// </summary>
    /// <param name="checkpoint">The checkpoint to validate.</param>
    /// <returns>A validation result containing any errors found.</returns>
    public CheckpointValidationResult Validate(TrainingCheckpoint checkpoint);
}

/// <summary>
/// Result of checkpoint validation containing all discovered errors.
/// </summary>
/// <param name="Errors">The list of validation error messages. Empty if the checkpoint is valid.</param>
public record CheckpointValidationResult(IReadOnlyList<string> Errors)
{
    /// <summary>
    /// Gets a value indicating whether the checkpoint passed all validation checks.
    /// </summary>
    public bool IsValid => Errors.Count == 0;
}
