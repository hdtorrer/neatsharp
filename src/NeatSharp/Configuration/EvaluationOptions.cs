namespace NeatSharp.Configuration;

/// <summary>
/// Controls how the evaluation phase handles errors during genome fitness evaluation.
/// </summary>
public class EvaluationOptions
{
    /// <summary>
    /// Determines whether the evolution run stops or continues when
    /// a genome evaluation throws an exception.
    /// Default is <see cref="EvaluationErrorMode.AssignFitness"/>.
    /// </summary>
    public EvaluationErrorMode ErrorMode { get; set; } = EvaluationErrorMode.AssignFitness;

    /// <summary>
    /// The fitness value assigned to a genome whose evaluation throws
    /// an exception, when <see cref="ErrorMode"/> is
    /// <see cref="EvaluationErrorMode.AssignFitness"/>.
    /// Must be finite and non-negative. Default is <c>0.0</c>.
    /// </summary>
    public double ErrorFitnessValue { get; set; }

    /// <summary>
    /// Maximum number of concurrent genome evaluations.
    /// <c>null</c> (default) uses all available processor cores.
    /// <c>1</c> reverts to sequential evaluation.
    /// Must be <c>null</c> or >= 1.
    /// </summary>
    public int? MaxDegreeOfParallelism { get; set; }
}
