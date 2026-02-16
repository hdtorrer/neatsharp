namespace NeatSharp.Exceptions;

/// <summary>
/// Thrown when a checkpoint fails structural integrity validation.
/// Contains all validation errors discovered during the check.
/// </summary>
public class CheckpointCorruptionException : CheckpointException
{
    /// <summary>
    /// Gets the list of all structural integrity failures found during validation.
    /// </summary>
    public IReadOnlyList<string> ValidationErrors { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="CheckpointCorruptionException"/> with
    /// the specified validation errors.
    /// </summary>
    /// <param name="validationErrors">All structural integrity failures found during validation.</param>
    public CheckpointCorruptionException(IReadOnlyList<string> validationErrors)
        : base(FormatMessage(validationErrors))
    {
        ValidationErrors = validationErrors;
    }

    /// <summary>
    /// Initializes a new instance of <see cref="CheckpointCorruptionException"/> with
    /// the specified validation errors and inner exception.
    /// </summary>
    /// <param name="validationErrors">All structural integrity failures found during validation.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public CheckpointCorruptionException(IReadOnlyList<string> validationErrors, Exception innerException)
        : base(FormatMessage(validationErrors), innerException)
    {
        ValidationErrors = validationErrors;
    }

    private static string FormatMessage(IReadOnlyList<string> validationErrors)
    {
        ArgumentNullException.ThrowIfNull(validationErrors);

        return validationErrors.Count switch
        {
            0 => "Checkpoint validation failed with no specific errors.",
            1 => $"Checkpoint is corrupt: {validationErrors[0]}",
            _ => $"Checkpoint is corrupt with {validationErrors.Count} errors: {string.Join("; ", validationErrors)}"
        };
    }
}
