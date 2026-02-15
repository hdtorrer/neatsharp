using NeatSharp.Genetics;

namespace NeatSharp.Evaluation;

/// <summary>
/// Evaluates multiple genomes in a single call, enabling bulk
/// processing optimizations such as GPU-accelerated evaluation
/// or shared environment simulation.
/// </summary>
/// <remarks>
/// Implement this interface when evaluating genomes individually
/// would be inefficient — for example, when using GPU acceleration
/// to evaluate many networks in parallel, or when genomes compete
/// against each other in a shared simulation.
/// </remarks>
public interface IBatchEvaluator
{
    /// <summary>
    /// Evaluates all genomes and reports their fitness scores via the callback.
    /// </summary>
    /// <param name="genomes">The genomes to evaluate.</param>
    /// <param name="setFitness">
    /// Callback to report a fitness score. Call with the genome's index
    /// in <paramref name="genomes"/> and its computed fitness.
    /// Must be called exactly once per genome.
    /// </param>
    /// <param name="cancellationToken">Token to observe for cancellation.</param>
    /// <returns>A task that completes when all genomes have been evaluated.</returns>
    /// <remarks>
    /// <para>
    /// <strong>Error handling contract:</strong> If evaluation fails for a subset of genomes,
    /// call <paramref name="setFitness"/> for all successfully evaluated genomes <em>before</em>
    /// throwing. When <c>EvaluationErrorMode.AssignFitness</c> is configured, the training runner
    /// assigns a default fitness to any genome whose score was not set via the callback.
    /// If the method throws without calling <paramref name="setFitness"/> for any genome,
    /// all genomes receive the default error fitness.
    /// </para>
    /// </remarks>
    Task EvaluateAsync(
        IReadOnlyList<IGenome> genomes,
        Action<int, double> setFitness,
        CancellationToken cancellationToken);
}
