using FluentAssertions;
using NeatSharp.Configuration;
using NeatSharp.Evaluation;
using NeatSharp.Genetics;
using NeatSharp.Tests.TestDoubles;
using Xunit;

namespace NeatSharp.Tests.Evaluation;

public class ParallelEnvironmentAdapterTests
{
    private sealed class StubEnvironmentEvaluator : IEnvironmentEvaluator
    {
        public Task<double> EvaluateAsync(IGenome genome, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(genome.NodeCount * 10.0);
        }
    }

    private sealed class FailingEnvironmentEvaluator(HashSet<int> failingNodeCounts) : IEnvironmentEvaluator
    {
        public Task<double> EvaluateAsync(IGenome genome, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (failingNodeCounts.Contains(genome.NodeCount))
            {
                throw new InvalidOperationException($"Failed for node count {genome.NodeCount}");
            }

            return Task.FromResult(genome.NodeCount * 10.0);
        }
    }

    private sealed class AllFailingEnvironmentEvaluator : IEnvironmentEvaluator
    {
        public Task<double> EvaluateAsync(IGenome genome, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw new InvalidOperationException($"Failed for node count {genome.NodeCount}");
        }
    }

    private sealed class ConcurrencyState
    {
        public int CurrentConcurrency;
        public int MaxObservedConcurrency;
    }

    private sealed class ConcurrencyTrackingEvaluator(ConcurrencyState state) : IEnvironmentEvaluator
    {
        public async Task<double> EvaluateAsync(IGenome genome, CancellationToken cancellationToken)
        {
            var current = Interlocked.Increment(ref state.CurrentConcurrency);

            int observed;
            do
            {
                observed = Volatile.Read(ref state.MaxObservedConcurrency);
                if (current <= observed)
                {
                    break;
                }
            }
            while (Interlocked.CompareExchange(ref state.MaxObservedConcurrency, current, observed) != observed);

            await Task.Delay(50, cancellationToken);

            Interlocked.Decrement(ref state.CurrentConcurrency);
            return genome.NodeCount * 10.0;
        }
    }

    private sealed class CancellationState
    {
        public int EvaluatedCount;
    }

    private sealed class CancellingEvaluator(
        CancellationTokenSource cts,
        CancellationState state,
        int cancelAfter) : IEnvironmentEvaluator
    {
        public Task<double> EvaluateAsync(IGenome genome, CancellationToken cancellationToken)
        {
            var count = Interlocked.Increment(ref state.EvaluatedCount);
            if (count >= cancelAfter)
            {
                cts.Cancel();
            }

            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(genome.NodeCount * 10.0);
        }
    }

    [Fact]
    public async Task EvaluatePopulationAsync_AllGenomes_ReceiveCorrectFitnessMatchingSequential()
    {
        var genomes = new List<IGenome>
        {
            new StubGenome(3, 5),
            new StubGenome(4, 6),
            new StubGenome(7, 2),
            new StubGenome(1, 9),
        };

        var evaluator = new StubEnvironmentEvaluator();
        var options = new EvaluationOptions { MaxDegreeOfParallelism = null };

        var sequentialScores = new double[genomes.Count];
        var sequentialStrategy = EvaluationStrategy.FromEnvironment(evaluator);
        await sequentialStrategy.EvaluatePopulationAsync(
            genomes,
            (index, fitness) => sequentialScores[index] = fitness,
            CancellationToken.None);

        var parallelScores = new double[genomes.Count];
        var parallelStrategy = EvaluationStrategy.FromEnvironment(evaluator, options);
        await parallelStrategy.EvaluatePopulationAsync(
            genomes,
            (index, fitness) => parallelScores[index] = fitness,
            CancellationToken.None);

        parallelScores.Should().BeEquivalentTo(sequentialScores);
        parallelScores[0].Should().Be(30.0);
        parallelScores[1].Should().Be(40.0);
        parallelScores[2].Should().Be(70.0);
        parallelScores[3].Should().Be(10.0);
    }

    [Fact]
    public async Task EvaluatePopulationAsync_SomeGenomesThrow_RemainingGetCorrectScoresAndExceptionContainsAllFailures()
    {
        var genomes = new List<IGenome>
        {
            new StubGenome(3, 5),
            new StubGenome(4, 6),
            new StubGenome(7, 2),
            new StubGenome(1, 9),
        };

        var evaluator = new FailingEnvironmentEvaluator(new HashSet<int> { 4, 1 });
        var options = new EvaluationOptions
        {
            MaxDegreeOfParallelism = null,
            ErrorMode = EvaluationErrorMode.StopRun,
        };

        var scores = new double[genomes.Count];
        var strategy = EvaluationStrategy.FromEnvironment(evaluator, options);

        var act = async () => await strategy.EvaluatePopulationAsync(
            genomes,
            (index, fitness) => scores[index] = fitness,
            CancellationToken.None);

        var exception = await act.Should().ThrowAsync<EvaluationException>();
        exception.Which.Errors.Should().HaveCount(2);

        scores[0].Should().Be(30.0);
        scores[2].Should().Be(70.0);
    }

    [Fact]
    public async Task EvaluatePopulationAsync_ErrorModeAssignFitness_AssignsDefaultFitnessToFailedGenomes()
    {
        var genomes = new List<IGenome>
        {
            new StubGenome(3, 5),
            new StubGenome(4, 6),
            new StubGenome(7, 2),
        };

        var evaluator = new FailingEnvironmentEvaluator(new HashSet<int> { 4 });
        var options = new EvaluationOptions
        {
            MaxDegreeOfParallelism = null,
            ErrorMode = EvaluationErrorMode.AssignFitness,
            ErrorFitnessValue = -1.0,
        };

        var scores = new double[genomes.Count];
        var strategy = EvaluationStrategy.FromEnvironment(evaluator, options);

        var act = async () => await strategy.EvaluatePopulationAsync(
            genomes,
            (index, fitness) => scores[index] = fitness,
            CancellationToken.None);

        var exception = await act.Should().ThrowAsync<EvaluationException>();
        exception.Which.Errors.Should().HaveCount(1);

        scores[0].Should().Be(30.0);
        scores[1].Should().Be(-1.0);
        scores[2].Should().Be(70.0);
    }

    [Fact]
    public async Task EvaluatePopulationAsync_MaxDegreeOfParallelism2_LimitsConcurrentEvaluationsTo2()
    {
        const int genomeCount = 10;
        var genomes = Enumerable.Range(0, genomeCount)
            .Select(i => (IGenome)new StubGenome(i + 1, 1))
            .ToList();

        var state = new ConcurrencyState();
        var evaluator = new ConcurrencyTrackingEvaluator(state);

        var options = new EvaluationOptions { MaxDegreeOfParallelism = 2 };
        var scores = new double[genomeCount];
        var strategy = EvaluationStrategy.FromEnvironment(evaluator, options);

        await strategy.EvaluatePopulationAsync(
            genomes,
            (index, fitness) => scores[index] = fitness,
            CancellationToken.None);

        state.MaxObservedConcurrency.Should().BeGreaterThan(0);
        state.MaxObservedConcurrency.Should().BeLessOrEqualTo(2);

        for (int i = 0; i < genomeCount; i++)
        {
            scores[i].Should().Be((i + 1) * 10.0);
        }
    }

    [Fact]
    public async Task EvaluatePopulationAsync_CancellationRequested_AlreadyCompletedScoresPreserved()
    {
        const int genomeCount = 20;
        var genomes = Enumerable.Range(0, genomeCount)
            .Select(i => (IGenome)new StubGenome(i + 1, 1))
            .ToList();

        using var cts = new CancellationTokenSource();
        var state = new CancellationState();
        var evaluator = new CancellingEvaluator(cts, state, cancelAfter: 5);

        var options = new EvaluationOptions { MaxDegreeOfParallelism = 2 };
        var scores = new double[genomeCount];
        var strategy = EvaluationStrategy.FromEnvironment(evaluator, options);

        var act = async () => await strategy.EvaluatePopulationAsync(
            genomes,
            (index, fitness) => scores[index] = fitness,
            cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();

        scores.Count(s => s > 0).Should().BeGreaterThan(0,
            "some genomes should have been evaluated before cancellation");
    }

    [Fact]
    public async Task EvaluatePopulationAsync_PopulationSmallerThanMaxDegreeOfParallelism_CompletesCorrectly()
    {
        var genomes = new List<IGenome>
        {
            new StubGenome(3, 5),
            new StubGenome(4, 6),
        };

        var evaluator = new StubEnvironmentEvaluator();
        var options = new EvaluationOptions { MaxDegreeOfParallelism = 100 };

        var scores = new double[genomes.Count];
        var strategy = EvaluationStrategy.FromEnvironment(evaluator, options);

        await strategy.EvaluatePopulationAsync(
            genomes,
            (index, fitness) => scores[index] = fitness,
            CancellationToken.None);

        scores[0].Should().Be(30.0);
        scores[1].Should().Be(40.0);
    }

    [Fact]
    public async Task EvaluatePopulationAsync_AllGenomesThrow_AggregatedErrorContainsAllFailuresAndDefaultFitnessAssigned()
    {
        var genomes = new List<IGenome>
        {
            new StubGenome(1, 1),
            new StubGenome(2, 2),
            new StubGenome(3, 3),
        };

        var evaluator = new AllFailingEnvironmentEvaluator();
        var options = new EvaluationOptions
        {
            MaxDegreeOfParallelism = null,
            ErrorMode = EvaluationErrorMode.AssignFitness,
            ErrorFitnessValue = -5.0,
        };

        var scores = new double[genomes.Count];
        var strategy = EvaluationStrategy.FromEnvironment(evaluator, options);

        var act = async () => await strategy.EvaluatePopulationAsync(
            genomes,
            (index, fitness) => scores[index] = fitness,
            CancellationToken.None);

        var exception = await act.Should().ThrowAsync<EvaluationException>();
        exception.Which.Errors.Should().HaveCount(3);

        scores[0].Should().Be(-5.0);
        scores[1].Should().Be(-5.0);
        scores[2].Should().Be(-5.0);
    }
}
