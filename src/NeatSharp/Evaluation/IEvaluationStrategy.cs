using NeatSharp.Genetics;

namespace NeatSharp.Evaluation;

/// <summary>
/// Internal abstraction for evaluating a population of genomes.
/// The evolution engine calls this once per generation to assign
/// fitness scores to all genomes.
/// </summary>
/// <remarks>
/// Users do not implement this interface directly. Instead, they use
/// the <see cref="EvaluationStrategy"/> factory methods.
/// </remarks>
public interface IEvaluationStrategy
{
    /// <summary>
    /// Evaluates all genomes in a population and reports their fitness scores.
    /// </summary>
    /// <param name="genomes">The genomes to evaluate.</param>
    /// <param name="setFitness">
    /// Callback to report a fitness score for a genome at the given index.
    /// Must be called exactly once for each index in <paramref name="genomes"/>.
    /// </param>
    /// <param name="cancellationToken">Token to observe for cancellation.</param>
    /// <returns>A task that completes when all genomes have been evaluated.</returns>
    Task EvaluatePopulationAsync(
        IReadOnlyList<IGenome> genomes,
        Action<int, double> setFitness,
        CancellationToken cancellationToken);
}
