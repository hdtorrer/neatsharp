namespace NeatSharp.Configuration;

/// <summary>
/// Determines how the evolution engine handles exceptions thrown
/// during individual genome evaluation.
/// </summary>
public enum EvaluationErrorMode
{
    /// <summary>
    /// Assign a default fitness value to the failing genome and continue
    /// evaluating remaining genomes. The error is logged via
    /// <c>TrainingLog.EvaluationFailed</c>. This is the default behavior.
    /// </summary>
    AssignFitness,

    /// <summary>
    /// Stop the entire evolution run immediately when any genome
    /// evaluation throws an exception. The exception propagates
    /// to the caller of <c>RunAsync</c>.
    /// </summary>
    StopRun
}
