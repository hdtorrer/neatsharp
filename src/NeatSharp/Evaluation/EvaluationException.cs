namespace NeatSharp.Evaluation;

/// <summary>
/// Aggregates per-genome evaluation failures. Thrown by sequential
/// evaluation adapters after all genomes have been processed, so
/// that a single genome failure does not prevent evaluation of
/// the remaining population.
/// </summary>
public sealed class EvaluationException : Exception
{
    /// <summary>
    /// The individual genome failures, each with the genome index
    /// and the exception that was thrown.
    /// </summary>
    public IReadOnlyList<(int Index, Exception Error)> Errors { get; }

    public EvaluationException(List<(int Index, Exception Error)> errors)
        : base($"Evaluation failed for {errors.Count} genome(s)", errors[0].Error)
    {
        Errors = errors;
    }
}
