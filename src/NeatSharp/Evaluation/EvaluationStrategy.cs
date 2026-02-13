using NeatSharp.Genetics;

namespace NeatSharp.Evaluation;

/// <summary>
/// Factory methods for creating <see cref="IEvaluationStrategy"/> instances
/// from user-facing evaluation patterns.
/// </summary>
public static class EvaluationStrategy
{
    /// <summary>
    /// Creates a strategy from a synchronous fitness function.
    /// Each genome is evaluated individually by calling the function.
    /// </summary>
    /// <param name="fitnessFunction">
    /// A function that takes a genome and returns its fitness score.
    /// </param>
    /// <returns>An evaluation strategy that applies the function to each genome.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="fitnessFunction"/> is null.
    /// </exception>
    public static IEvaluationStrategy FromFunction(Func<IGenome, double> fitnessFunction)
    {
        ArgumentNullException.ThrowIfNull(fitnessFunction);
        return new SyncFunctionAdapter(fitnessFunction);
    }

    /// <summary>
    /// Creates a strategy from an asynchronous fitness function.
    /// Each genome is evaluated individually by calling the function.
    /// </summary>
    /// <param name="fitnessFunction">
    /// An async function that takes a genome and cancellation token
    /// and returns its fitness score.
    /// </param>
    /// <returns>An evaluation strategy that applies the function to each genome.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="fitnessFunction"/> is null.
    /// </exception>
    public static IEvaluationStrategy FromFunction(Func<IGenome, CancellationToken, Task<double>> fitnessFunction)
    {
        ArgumentNullException.ThrowIfNull(fitnessFunction);
        return new AsyncFunctionAdapter(fitnessFunction);
    }

    /// <summary>
    /// Creates a strategy from an <see cref="IEnvironmentEvaluator"/>.
    /// Each genome is evaluated by running it through the environment.
    /// </summary>
    /// <param name="evaluator">The environment evaluator.</param>
    /// <returns>An evaluation strategy that delegates to the environment evaluator.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="evaluator"/> is null.
    /// </exception>
    public static IEvaluationStrategy FromEnvironment(IEnvironmentEvaluator evaluator)
    {
        ArgumentNullException.ThrowIfNull(evaluator);
        return new EnvironmentAdapter(evaluator);
    }

    /// <summary>
    /// Creates a strategy from an <see cref="IBatchEvaluator"/>.
    /// The entire population is passed to the evaluator in a single call.
    /// </summary>
    /// <param name="evaluator">The batch evaluator.</param>
    /// <returns>An evaluation strategy that delegates to the batch evaluator.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="evaluator"/> is null.
    /// </exception>
    public static IEvaluationStrategy FromBatch(IBatchEvaluator evaluator)
    {
        ArgumentNullException.ThrowIfNull(evaluator);
        return new BatchAdapter(evaluator);
    }

    private sealed class SyncFunctionAdapter(Func<IGenome, double> fitnessFunction) : IEvaluationStrategy
    {
        public Task EvaluatePopulationAsync(
            IReadOnlyList<IGenome> genomes,
            Action<int, double> setFitness,
            CancellationToken cancellationToken)
        {
            for (int i = 0; i < genomes.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var fitness = fitnessFunction(genomes[i]);
                setFitness(i, fitness);
            }

            return Task.CompletedTask;
        }
    }

    private sealed class AsyncFunctionAdapter(Func<IGenome, CancellationToken, Task<double>> fitnessFunction) : IEvaluationStrategy
    {
        public async Task EvaluatePopulationAsync(
            IReadOnlyList<IGenome> genomes,
            Action<int, double> setFitness,
            CancellationToken cancellationToken)
        {
            for (int i = 0; i < genomes.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var fitness = await fitnessFunction(genomes[i], cancellationToken);
                setFitness(i, fitness);
            }
        }
    }

    private sealed class EnvironmentAdapter(IEnvironmentEvaluator evaluator) : IEvaluationStrategy
    {
        public async Task EvaluatePopulationAsync(
            IReadOnlyList<IGenome> genomes,
            Action<int, double> setFitness,
            CancellationToken cancellationToken)
        {
            for (int i = 0; i < genomes.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var fitness = await evaluator.EvaluateAsync(genomes[i], cancellationToken);
                setFitness(i, fitness);
            }
        }
    }

    private sealed class BatchAdapter(IBatchEvaluator evaluator) : IEvaluationStrategy
    {
        public Task EvaluatePopulationAsync(
            IReadOnlyList<IGenome> genomes,
            Action<int, double> setFitness,
            CancellationToken cancellationToken)
        {
            return evaluator.EvaluateAsync(genomes, setFitness, cancellationToken);
        }
    }
}
