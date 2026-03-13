using NeatSharp.Configuration;
using NeatSharp.Genetics;

namespace NeatSharp.Evaluation;

/// <summary>
/// Factory methods for creating <see cref="IEvaluationStrategy"/> instances
/// from user-facing evaluation patterns.
/// </summary>
public static partial class EvaluationStrategy
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

    /// <summary>
    /// Creates a strategy from a synchronous fitness function with evaluation options.
    /// When <see cref="EvaluationOptions.MaxDegreeOfParallelism"/> is <c>1</c>, returns
    /// a sequential adapter; when <c>null</c> or greater than <c>1</c>, returns a parallel adapter.
    /// </summary>
    /// <param name="fitnessFunction">
    /// A function that takes a genome and returns its fitness score.
    /// Must be thread-safe when <paramref name="options"/> specifies parallel evaluation.
    /// </param>
    /// <param name="options">Evaluation options controlling parallelism and error handling.</param>
    /// <returns>An evaluation strategy that applies the function to each genome.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="fitnessFunction"/> or <paramref name="options"/> is null.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <see cref="EvaluationOptions.MaxDegreeOfParallelism"/> is less than or equal to zero.
    /// </exception>
    public static IEvaluationStrategy FromFunction(Func<IGenome, double> fitnessFunction, EvaluationOptions options)
    {
        ArgumentNullException.ThrowIfNull(fitnessFunction);
        ArgumentNullException.ThrowIfNull(options);
        ValidateMaxDegreeOfParallelism(options.MaxDegreeOfParallelism);

        if (options.MaxDegreeOfParallelism == 1)
        {
            return new SyncFunctionAdapter(fitnessFunction);
        }

        return new ParallelSyncFunctionAdapter(fitnessFunction, options);
    }

    /// <summary>
    /// Creates a strategy from an asynchronous fitness function with evaluation options.
    /// When <see cref="EvaluationOptions.MaxDegreeOfParallelism"/> is <c>1</c>, returns
    /// a sequential adapter; when <c>null</c> or greater than <c>1</c>, returns a parallel adapter.
    /// </summary>
    /// <param name="fitnessFunction">
    /// An async function that takes a genome and cancellation token and returns its fitness score.
    /// Must be thread-safe when <paramref name="options"/> specifies parallel evaluation.
    /// </param>
    /// <param name="options">Evaluation options controlling parallelism and error handling.</param>
    /// <returns>An evaluation strategy that applies the function to each genome.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="fitnessFunction"/> or <paramref name="options"/> is null.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <see cref="EvaluationOptions.MaxDegreeOfParallelism"/> is less than or equal to zero.
    /// </exception>
    public static IEvaluationStrategy FromFunction(
        Func<IGenome, CancellationToken, Task<double>> fitnessFunction,
        EvaluationOptions options)
    {
        ArgumentNullException.ThrowIfNull(fitnessFunction);
        ArgumentNullException.ThrowIfNull(options);
        ValidateMaxDegreeOfParallelism(options.MaxDegreeOfParallelism);

        if (options.MaxDegreeOfParallelism == 1)
        {
            return new AsyncFunctionAdapter(fitnessFunction);
        }

        return new ParallelAsyncFunctionAdapter(fitnessFunction, options);
    }

    /// <summary>
    /// Creates a strategy from an <see cref="IEnvironmentEvaluator"/> with evaluation options.
    /// When <see cref="EvaluationOptions.MaxDegreeOfParallelism"/> is <c>1</c>, returns
    /// a sequential adapter; when <c>null</c> or greater than <c>1</c>, returns a parallel adapter.
    /// </summary>
    /// <param name="evaluator">The environment evaluator.
    /// Must be thread-safe when <paramref name="options"/> specifies parallel evaluation.
    /// </param>
    /// <param name="options">Evaluation options controlling parallelism and error handling.</param>
    /// <returns>An evaluation strategy that delegates to the environment evaluator.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="evaluator"/> or <paramref name="options"/> is null.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <see cref="EvaluationOptions.MaxDegreeOfParallelism"/> is less than or equal to zero.
    /// </exception>
    public static IEvaluationStrategy FromEnvironment(IEnvironmentEvaluator evaluator, EvaluationOptions options)
    {
        ArgumentNullException.ThrowIfNull(evaluator);
        ArgumentNullException.ThrowIfNull(options);
        ValidateMaxDegreeOfParallelism(options.MaxDegreeOfParallelism);

        if (options.MaxDegreeOfParallelism == 1)
        {
            return new EnvironmentAdapter(evaluator);
        }

        return new ParallelEnvironmentAdapter(evaluator, options);
    }

    /// <summary>
    /// Validates that <paramref name="maxDegreeOfParallelism"/> is <c>null</c> or at least <c>1</c>.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="maxDegreeOfParallelism"/> is less than or equal to zero.
    /// </exception>
    private static void ValidateMaxDegreeOfParallelism(int? maxDegreeOfParallelism)
    {
        if (maxDegreeOfParallelism is <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxDegreeOfParallelism),
                maxDegreeOfParallelism,
                "MaxDegreeOfParallelism must be null or >= 1.");
        }
    }

    private sealed class SyncFunctionAdapter(Func<IGenome, double> fitnessFunction) : IEvaluationStrategy
    {
        public Task EvaluatePopulationAsync(
            IReadOnlyList<IGenome> genomes,
            Action<int, double> setFitness,
            CancellationToken cancellationToken)
        {
            List<(int Index, Exception Error)>? errors = null;

            for (int i = 0; i < genomes.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    var fitness = fitnessFunction(genomes[i]);
                    setFitness(i, fitness);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    (errors ??= []).Add((i, ex));
                }
            }

            if (errors is { Count: > 0 })
            {
                throw new EvaluationException(errors);
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
            List<(int Index, Exception Error)>? errors = null;

            for (int i = 0; i < genomes.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    var fitness = await fitnessFunction(genomes[i], cancellationToken);
                    setFitness(i, fitness);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    (errors ??= []).Add((i, ex));
                }
            }

            if (errors is { Count: > 0 })
            {
                throw new EvaluationException(errors);
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
            List<(int Index, Exception Error)>? errors = null;

            for (int i = 0; i < genomes.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    var fitness = await evaluator.EvaluateAsync(genomes[i], cancellationToken);
                    setFitness(i, fitness);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    (errors ??= []).Add((i, ex));
                }
            }

            if (errors is { Count: > 0 })
            {
                throw new EvaluationException(errors);
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
