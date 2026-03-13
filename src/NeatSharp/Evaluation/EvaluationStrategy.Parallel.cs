using System.Collections.Concurrent;
using NeatSharp.Genetics;

namespace NeatSharp.Evaluation;

/// <summary>
/// Parallel evaluation adapters and shared helpers for <see cref="EvaluationStrategy"/>.
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
    /// with a synchronous fitness function. Thread-safety is ensured via a lock-wrapped
    /// <c>setFitness</c> callback. Errors are accumulated in a <see cref="ConcurrentBag{T}"/>
    /// and thrown as an <see cref="EvaluationException"/> after all evaluations complete.
    /// When <see cref="Configuration.EvaluationErrorMode.AssignFitness"/> is configured,
    /// failed genomes receive the configured <see cref="Configuration.EvaluationOptions.ErrorFitnessValue"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>Thread-safety requirement (FR-013):</strong> The user-provided fitness function
    /// passed to this adapter will be invoked concurrently from multiple threads. Callers must
    /// ensure their fitness function is thread-safe — it must not mutate shared state without
    /// proper synchronization.
    /// </para>
    /// <para>
    /// The <c>setFitness</c> callback provided to
    /// <see cref="IEvaluationStrategy.EvaluatePopulationAsync"/> is automatically synchronized
    /// by this adapter via a lock wrapper; callers do not need to synchronize it themselves.
    /// </para>
    /// </remarks>
    private sealed class ParallelSyncFunctionAdapter : IEvaluationStrategy
    {
        private readonly Func<IGenome, double> _fitnessFunction;
        private readonly Configuration.EvaluationOptions _options;
        private readonly int _resolvedMaxDegreeOfParallelism;

        public ParallelSyncFunctionAdapter(
            Func<IGenome, double> fitnessFunction,
            Configuration.EvaluationOptions options)
        {
            _fitnessFunction = fitnessFunction;
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

            await Parallel.ForEachAsync(indexedGenomes, parallelOptions, (item, ct) =>
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    var fitness = _fitnessFunction(item.Genome);
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

                return ValueTask.CompletedTask;
            });

            var exception = ToEvaluationException(errors);
            if (exception is not null)
            {
                throw exception;
            }
        }
    }

    /// <summary>
    /// Evaluates genomes in parallel using <see cref="SemaphoreSlim"/> to bound concurrency
    /// and <see cref="Task.WhenAll(Task[])"/> to collect results from an asynchronous fitness function.
    /// Thread-safety is ensured via a lock-wrapped <c>setFitness</c> callback.
    /// Errors are accumulated in a <see cref="ConcurrentBag{T}"/>
    /// and thrown as an <see cref="EvaluationException"/> after all evaluations complete.
    /// When <see cref="Configuration.EvaluationErrorMode.AssignFitness"/> is configured,
    /// failed genomes receive the configured <see cref="Configuration.EvaluationOptions.ErrorFitnessValue"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>Thread-safety requirement (FR-013):</strong> The user-provided async fitness function
    /// passed to this adapter will be invoked concurrently from multiple threads. Callers must
    /// ensure their fitness function is thread-safe — it must not mutate shared state without
    /// proper synchronization.
    /// </para>
    /// <para>
    /// The <c>setFitness</c> callback provided to
    /// <see cref="IEvaluationStrategy.EvaluatePopulationAsync"/> is automatically synchronized
    /// by this adapter via a lock wrapper; callers do not need to synchronize it themselves.
    /// </para>
    /// </remarks>
    private sealed class ParallelAsyncFunctionAdapter : IEvaluationStrategy
    {
        private readonly Func<IGenome, CancellationToken, Task<double>> _fitnessFunction;
        private readonly Configuration.EvaluationOptions _options;
        private readonly int _resolvedMaxDegreeOfParallelism;

        public ParallelAsyncFunctionAdapter(
            Func<IGenome, CancellationToken, Task<double>> fitnessFunction,
            Configuration.EvaluationOptions options)
        {
            _fitnessFunction = fitnessFunction;
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
            using var semaphore = new SemaphoreSlim(_resolvedMaxDegreeOfParallelism);

            var tasks = new Task[genomes.Count];
            for (int i = 0; i < genomes.Count; i++)
            {
                int index = i;
                tasks[i] = EvaluateGenomeAsync(
                    genomes[index], index, semaphore, threadSafeSetFitness, errors, cancellationToken);
            }

            await Task.WhenAll(tasks);

            var exception = ToEvaluationException(errors);
            if (exception is not null)
            {
                throw exception;
            }
        }

        private async Task EvaluateGenomeAsync(
            IGenome genome,
            int index,
            SemaphoreSlim semaphore,
            Action<int, double> setFitness,
            ConcurrentBag<(int, Exception)> errors,
            CancellationToken ct)
        {
            await semaphore.WaitAsync(ct);
            try
            {
                var fitness = await _fitnessFunction(genome, ct);
                setFitness(index, fitness);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                if (_options.ErrorMode == Configuration.EvaluationErrorMode.AssignFitness)
                {
                    setFitness(index, _options.ErrorFitnessValue);
                }

                errors.Add((index, ex));
            }
            finally
            {
                semaphore.Release();
            }
        }
    }

    /// <summary>
    /// Evaluates genomes in parallel using <see cref="Parallel.ForEachAsync{TSource}(IEnumerable{TSource}, ParallelOptions, Func{TSource, CancellationToken, ValueTask})"/>
    /// with an <see cref="IEnvironmentEvaluator"/>. Thread-safety is ensured via a lock-wrapped
    /// <c>setFitness</c> callback. Errors are accumulated in a <see cref="ConcurrentBag{T}"/>
    /// and thrown as an <see cref="EvaluationException"/> after all evaluations complete.
    /// When <see cref="Configuration.EvaluationErrorMode.AssignFitness"/> is configured,
    /// failed genomes receive the configured <see cref="Configuration.EvaluationOptions.ErrorFitnessValue"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>Thread-safety requirement (FR-013):</strong> The user-provided
    /// <see cref="IEnvironmentEvaluator"/> passed to this adapter will have its
    /// <see cref="IEnvironmentEvaluator.EvaluateAsync"/> method invoked concurrently from
    /// multiple threads. Callers must ensure their evaluator implementation is thread-safe —
    /// it must not mutate shared state without proper synchronization. Typically each call
    /// should create its own environment instance or use thread-local state.
    /// </para>
    /// <para>
    /// The <c>setFitness</c> callback provided to
    /// <see cref="IEvaluationStrategy.EvaluatePopulationAsync"/> is automatically synchronized
    /// by this adapter via a lock wrapper; callers do not need to synchronize it themselves.
    /// </para>
    /// </remarks>
    private sealed class ParallelEnvironmentAdapter : IEvaluationStrategy
    {
        private readonly IEnvironmentEvaluator _evaluator;
        private readonly Configuration.EvaluationOptions _options;
        private readonly int _resolvedMaxDegreeOfParallelism;

        public ParallelEnvironmentAdapter(
            IEnvironmentEvaluator evaluator,
            Configuration.EvaluationOptions options)
        {
            _evaluator = evaluator;
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
                ct.ThrowIfCancellationRequested();
                try
                {
                    var fitness = await _evaluator.EvaluateAsync(item.Genome, ct);
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
