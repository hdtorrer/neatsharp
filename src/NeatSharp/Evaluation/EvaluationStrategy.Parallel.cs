using System.Collections.Concurrent;
using NeatSharp.Genetics;

namespace NeatSharp.Evaluation;

/// <summary>
/// Parallel evaluation adapter and shared helpers for <see cref="EvaluationStrategy"/>.
/// </summary>
public static partial class EvaluationStrategy
{
    /// <summary>
    /// Creates a thread-safe wrapper around a <paramref name="setFitness"/> callback
    /// that synchronizes access via the provided <paramref name="syncLock"/>.
    /// </summary>
    /// <param name="setFitness">The original callback to wrap.</param>
    /// <param name="syncLock">The lock object used to synchronize access.</param>
    /// <returns>A thread-safe wrapper that delegates to <paramref name="setFitness"/> under lock.</returns>
    private static Action<int, double> CreateThreadSafeSetFitness(Action<int, double> setFitness, object syncLock)
    {
        return (index, fitness) =>
        {
            lock (syncLock)
            {
                setFitness(index, fitness);
            }
        };
    }

    /// <summary>
    /// Converts accumulated errors from a <see cref="ConcurrentBag{T}"/> to an
    /// <see cref="EvaluationException"/> if any errors are present.
    /// </summary>
    /// <param name="errors">The thread-safe bag of accumulated errors.</param>
    /// <returns>
    /// An <see cref="EvaluationException"/> containing all accumulated errors,
    /// or <c>null</c> if no errors were collected.
    /// </returns>
    private static EvaluationException? ToEvaluationException(ConcurrentBag<(int Index, Exception Error)> errors)
    {
        if (errors.IsEmpty)
        {
            return null;
        }

        return new EvaluationException(errors.ToList());
    }

    /// <summary>
    /// Evaluates genomes in parallel using <see cref="Parallel.ForEachAsync{TSource}(IEnumerable{TSource}, ParallelOptions, Func{TSource, CancellationToken, ValueTask})"/>
    /// with a caller-supplied evaluation delegate. Thread-safety is ensured via a lock-wrapped
    /// <c>setFitness</c> callback. Errors are accumulated in a <see cref="ConcurrentBag{T}"/>
    /// and thrown as an <see cref="EvaluationException"/> after all evaluations complete.
    /// When <see cref="Configuration.EvaluationErrorMode.AssignFitness"/> is configured,
    /// failed genomes receive the configured <see cref="Configuration.EvaluationOptions.ErrorFitnessValue"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>Thread-safety requirement (FR-013):</strong> The user-provided evaluation logic
    /// will be invoked concurrently from multiple threads. Callers must ensure their evaluation
    /// function or evaluator is thread-safe — it must not mutate shared state without proper
    /// synchronization.
    /// </para>
    /// <para>
    /// The <c>setFitness</c> callback provided to
    /// <see cref="IEvaluationStrategy.EvaluatePopulationAsync"/> is automatically synchronized
    /// by this adapter via a lock wrapper; callers do not need to synchronize it themselves.
    /// </para>
    /// </remarks>
    private sealed class ParallelAdapter : IEvaluationStrategy
    {
        private readonly Func<IGenome, CancellationToken, ValueTask<double>> _evaluateFunc;
        private readonly Configuration.EvaluationOptions _options;
        private readonly int _resolvedMaxDegreeOfParallelism;

        public ParallelAdapter(
            Func<IGenome, CancellationToken, ValueTask<double>> evaluateFunc,
            Configuration.EvaluationOptions options)
        {
            _evaluateFunc = evaluateFunc;
            _options = options;
            _resolvedMaxDegreeOfParallelism = options.MaxDegreeOfParallelism ?? Environment.ProcessorCount;
        }

        public async Task EvaluatePopulationAsync(
            IReadOnlyList<IGenome> genomes,
            Action<int, double> setFitness,
            CancellationToken cancellationToken)
        {
            var syncLock = new object();
            var threadSafeSetFitness = CreateThreadSafeSetFitness(setFitness, syncLock);
            var errors = new ConcurrentBag<(int Index, Exception Error)>();

            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = _resolvedMaxDegreeOfParallelism,
                CancellationToken = cancellationToken,
            };

            var indexedGenomes = genomes.Select((genome, index) => (Genome: genome, Index: index));

            await Parallel.ForEachAsync(indexedGenomes, parallelOptions, async (item, ct) =>
            {
                try
                {
                    var fitness = await _evaluateFunc(item.Genome, ct);
                    threadSafeSetFitness(item.Index, fitness);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    if (_options.ErrorMode == Configuration.EvaluationErrorMode.AssignFitness)
                    {
                        threadSafeSetFitness(item.Index, _options.ErrorFitnessValue);
                    }

                    errors.Add((item.Index, ex));
                }
            });

            var exception = ToEvaluationException(errors);
            if (exception is not null)
            {
                throw exception;
            }
        }
    }
}
