using NeatSharp.Evaluation;
using NeatSharp.Genetics;

namespace NeatSharp.Gpu.Scheduling;

/// <summary>
/// Adapts an <see cref="IEvaluationStrategy"/> to the <see cref="IBatchEvaluator"/> interface.
/// Used by the hybrid scheduler to wrap the CPU evaluation path as an <see cref="IBatchEvaluator"/>.
/// </summary>
internal sealed class EvaluationStrategyBatchAdapter(IEvaluationStrategy evaluationStrategy) : IBatchEvaluator
{
    /// <inheritdoc />
    public Task EvaluateAsync(
        IReadOnlyList<IGenome> genomes,
        Action<int, double> setFitness,
        CancellationToken cancellationToken)
    {
        return evaluationStrategy.EvaluatePopulationAsync(genomes, setFitness, cancellationToken);
    }
}
