using FluentAssertions;
using NeatSharp.Configuration;
using NeatSharp.Evaluation;
using NeatSharp.Genetics;
using NeatSharp.Tests.TestDoubles;
using Xunit;

namespace NeatSharp.Tests.Evaluation;

public class ParallelSyncFunctionAdapterTests
{
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

        Func<IGenome, double> fitnessFunc = g => g.NodeCount * 10.0;
        var options = new EvaluationOptions { MaxDegreeOfParallelism = null };

        var sequentialScores = new double[genomes.Count];
        var sequentialStrategy = EvaluationStrategy.FromFunction(fitnessFunc);
        await sequentialStrategy.EvaluatePopulationAsync(
            genomes,
            (index, fitness) => sequentialScores[index] = fitness,
            CancellationToken.None);

        var parallelScores = new double[genomes.Count];
        var parallelStrategy = EvaluationStrategy.FromFunction(fitnessFunc, options);
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

        var failingIndices = new HashSet<int> { 1, 3 };
        Func<IGenome, double> fitnessFunc = g =>
        {
            var stub = (StubGenome)g;
            if (stub.NodeCount == 4 || stub.NodeCount == 1)
            {
                throw new InvalidOperationException($"Failed for node count {stub.NodeCount}");
            }

            return stub.NodeCount * 10.0;
        };

        var options = new EvaluationOptions
        {
            MaxDegreeOfParallelism = null,
            ErrorMode = EvaluationErrorMode.StopRun,
        };

        var scores = new double[genomes.Count];
        var strategy = EvaluationStrategy.FromFunction(fitnessFunc, options);

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

        Func<IGenome, double> fitnessFunc = g =>
        {
            var stub = (StubGenome)g;
            if (stub.NodeCount == 4)
            {
                throw new InvalidOperationException("Evaluation failed");
            }

            return stub.NodeCount * 10.0;
        };

        var options = new EvaluationOptions
        {
            MaxDegreeOfParallelism = null,
            ErrorMode = EvaluationErrorMode.AssignFitness,
            ErrorFitnessValue = -1.0,
        };

        var scores = new double[genomes.Count];
        var strategy = EvaluationStrategy.FromFunction(fitnessFunc, options);

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
    public async Task EvaluatePopulationAsync_CancellationRequested_AlreadyCompletedScoresPreserved()
    {
        const int genomeCount = 20;
        var genomes = Enumerable.Range(0, genomeCount)
            .Select(i => (IGenome)new StubGenome(i + 1, 1))
            .ToList();

        using var cts = new CancellationTokenSource();
        var evaluatedCount = 0;

        Func<IGenome, double> fitnessFunc = g =>
        {
            var count = Interlocked.Increment(ref evaluatedCount);
            if (count >= 5)
            {
                cts.Cancel();
            }

            cts.Token.ThrowIfCancellationRequested();
            return g.NodeCount * 10.0;
        };

        var options = new EvaluationOptions { MaxDegreeOfParallelism = 2 };
        var scores = new double[genomeCount];
        var strategy = EvaluationStrategy.FromFunction(fitnessFunc, options);

        var act = async () => await strategy.EvaluatePopulationAsync(
            genomes,
            (index, fitness) => scores[index] = fitness,
            cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();

        scores.Count(s => s > 0).Should().BeGreaterThan(0, "some genomes should have been evaluated before cancellation");
    }

    [Fact]
    public async Task EvaluatePopulationAsync_PopulationSmallerThanMaxDegreeOfParallelism_CompletesCorrectly()
    {
        var genomes = new List<IGenome>
        {
            new StubGenome(3, 5),
            new StubGenome(4, 6),
        };

        Func<IGenome, double> fitnessFunc = g => g.NodeCount * 10.0;
        var options = new EvaluationOptions { MaxDegreeOfParallelism = 100 };

        var scores = new double[genomes.Count];
        var strategy = EvaluationStrategy.FromFunction(fitnessFunc, options);

        await strategy.EvaluatePopulationAsync(
            genomes,
            (index, fitness) => scores[index] = fitness,
            CancellationToken.None);

        scores[0].Should().Be(30.0);
        scores[1].Should().Be(40.0);
    }

    [Fact]
    public async Task EvaluatePopulationAsync_MaxDegreeOfParallelism2_LimitsConcurrentEvaluationsTo2()
    {
        const int genomeCount = 10;
        var genomes = Enumerable.Range(0, genomeCount)
            .Select(i => (IGenome)new StubGenome(i + 1, 1))
            .ToList();

        var currentConcurrency = 0;
        var maxObservedConcurrency = 0;

        Func<IGenome, double> fitnessFunc = g =>
        {
            var current = Interlocked.Increment(ref currentConcurrency);

            // Track max concurrency via CompareExchange loop
            int observed;
            do
            {
                observed = Volatile.Read(ref maxObservedConcurrency);
                if (current <= observed)
                {
                    break;
                }
            }
            while (Interlocked.CompareExchange(ref maxObservedConcurrency, current, observed) != observed);

            // Short delay to ensure overlap between concurrent evaluations
            Thread.Sleep(50);

            Interlocked.Decrement(ref currentConcurrency);
            return g.NodeCount * 10.0;
        };

        var options = new EvaluationOptions { MaxDegreeOfParallelism = 2 };
        var scores = new double[genomeCount];
        var strategy = EvaluationStrategy.FromFunction(fitnessFunc, options);

        await strategy.EvaluatePopulationAsync(
            genomes,
            (index, fitness) => scores[index] = fitness,
            CancellationToken.None);

        maxObservedConcurrency.Should().BeGreaterThan(0);
        maxObservedConcurrency.Should().BeLessOrEqualTo(2);

        // Verify all scores are correct
        for (int i = 0; i < genomeCount; i++)
        {
            scores[i].Should().Be((i + 1) * 10.0);
        }
    }

    [Fact]
    public async Task EvaluatePopulationAsync_MaxDegreeOfParallelismNull_UsesAllCoresAndCompletesCorrectly()
    {
        var genomes = new List<IGenome>
        {
            new StubGenome(3, 5),
            new StubGenome(4, 6),
            new StubGenome(7, 2),
            new StubGenome(1, 9),
        };

        Func<IGenome, double> fitnessFunc = g => g.NodeCount * 10.0;
        var options = new EvaluationOptions { MaxDegreeOfParallelism = null };

        var scores = new double[genomes.Count];
        var strategy = EvaluationStrategy.FromFunction(fitnessFunc, options);

        await strategy.EvaluatePopulationAsync(
            genomes,
            (index, fitness) => scores[index] = fitness,
            CancellationToken.None);

        scores[0].Should().Be(30.0);
        scores[1].Should().Be(40.0);
        scores[2].Should().Be(70.0);
        scores[3].Should().Be(10.0);
    }

    [Fact]
    public void FromFunction_MaxDegreeOfParallelism1_ReturnsSequentialAdapter()
    {
        Func<IGenome, double> fitnessFunc = g => g.NodeCount * 10.0;
        var options = new EvaluationOptions { MaxDegreeOfParallelism = 1 };

        var strategy = EvaluationStrategy.FromFunction(fitnessFunc, options);

        // MaxDegreeOfParallelism = 1 should return the sequential SyncFunctionAdapter,
        // not the ParallelSyncFunctionAdapter. We verify by checking the type name.
        strategy.Should().NotBeNull();
        strategy.GetType().Name.Should().NotContain("Parallel");
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

        Func<IGenome, double> fitnessFunc = g =>
            throw new InvalidOperationException($"Failed for node count {g.NodeCount}");

        var options = new EvaluationOptions
        {
            MaxDegreeOfParallelism = null,
            ErrorMode = EvaluationErrorMode.AssignFitness,
            ErrorFitnessValue = -5.0,
        };

        var scores = new double[genomes.Count];
        var strategy = EvaluationStrategy.FromFunction(fitnessFunc, options);

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
