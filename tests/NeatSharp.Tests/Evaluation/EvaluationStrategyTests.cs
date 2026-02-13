using FluentAssertions;
using NeatSharp.Evaluation;
using NeatSharp.Evolution;
using NeatSharp.Genetics;
using NeatSharp.Tests.TestDoubles;
using Xunit;

namespace NeatSharp.Tests.Evaluation;

public class EvaluationStrategyTests
{
    [Fact]
    public void FromFunction_Sync_NullFunction_ThrowsArgumentNullException()
    {
        var act = () => EvaluationStrategy.FromFunction((Func<IGenome, double>)null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void FromFunction_Sync_ReturnsNonNullStrategy()
    {
        var strategy = EvaluationStrategy.FromFunction(g => 1.0);

        strategy.Should().NotBeNull();
        strategy.Should().BeAssignableTo<IEvaluationStrategy>();
    }

    [Fact]
    public async Task FromFunction_Sync_EvaluatesGenomesViaCallback()
    {
        var genome1 = new StubGenome(3, 5);
        var genome2 = new StubGenome(4, 6);
        var genomes = new List<IGenome> { genome1, genome2 };
        var scores = new double[2];

        var strategy = EvaluationStrategy.FromFunction(g =>
        {
            var stub = (StubGenome)g;
            return stub.NodeCount * 10.0;
        });

        await strategy.EvaluatePopulationAsync(
            genomes,
            (index, fitness) => scores[index] = fitness,
            CancellationToken.None);

        scores[0].Should().Be(30.0);
        scores[1].Should().Be(40.0);
    }

    [Fact]
    public async Task FromFunction_Sync_RespectsCancel()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var genomes = new List<IGenome> { new StubGenome(1, 1) };
        var strategy = EvaluationStrategy.FromFunction(g => 1.0);

        var act = async () => await strategy.EvaluatePopulationAsync(
            genomes,
            (_, _) => { },
            cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task FromFunction_Sync_EmptyPopulation_CompletesSuccessfully()
    {
        var genomes = new List<IGenome>();
        var strategy = EvaluationStrategy.FromFunction(g => 1.0);

        await strategy.EvaluatePopulationAsync(
            genomes,
            (_, _) => { },
            CancellationToken.None);
    }

    // --- T032: FromFunction(async) tests ---

    [Fact]
    public void FromFunction_Async_NullFunction_ThrowsArgumentNullException()
    {
        var act = () => EvaluationStrategy.FromFunction((Func<IGenome, CancellationToken, Task<double>>)null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void FromFunction_Async_ReturnsNonNullStrategy()
    {
        var strategy = EvaluationStrategy.FromFunction(
            (g, ct) => Task.FromResult(1.0));

        strategy.Should().NotBeNull();
        strategy.Should().BeAssignableTo<IEvaluationStrategy>();
    }

    [Fact]
    public async Task FromFunction_Async_EvaluatesGenomesViaCallback()
    {
        var genome1 = new StubGenome(3, 5);
        var genome2 = new StubGenome(4, 6);
        var genomes = new List<IGenome> { genome1, genome2 };
        var scores = new double[2];

        var strategy = EvaluationStrategy.FromFunction(
            (g, ct) =>
            {
                var stub = (StubGenome)g;
                return Task.FromResult(stub.NodeCount * 10.0);
            });

        await strategy.EvaluatePopulationAsync(
            genomes,
            (index, fitness) => scores[index] = fitness,
            CancellationToken.None);

        scores[0].Should().Be(30.0);
        scores[1].Should().Be(40.0);
    }

    [Fact]
    public async Task FromFunction_Async_RespectsCancel()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var genomes = new List<IGenome> { new StubGenome(1, 1) };
        var strategy = EvaluationStrategy.FromFunction(
            (g, ct) =>
            {
                ct.ThrowIfCancellationRequested();
                return Task.FromResult(1.0);
            });

        var act = async () => await strategy.EvaluatePopulationAsync(
            genomes,
            (_, _) => { },
            cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    // --- T032: FromEnvironment tests ---

    [Fact]
    public void FromEnvironment_NullEvaluator_ThrowsArgumentNullException()
    {
        var act = () => EvaluationStrategy.FromEnvironment(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void FromEnvironment_ReturnsNonNullStrategy()
    {
        var strategy = EvaluationStrategy.FromEnvironment(new StubEnvironmentEvaluator());

        strategy.Should().NotBeNull();
        strategy.Should().BeAssignableTo<IEvaluationStrategy>();
    }

    [Fact]
    public async Task FromEnvironment_EvaluatesGenomesViaEnvironment()
    {
        var genome1 = new StubGenome(3, 5);
        var genome2 = new StubGenome(4, 6);
        var genomes = new List<IGenome> { genome1, genome2 };
        var scores = new double[2];

        var strategy = EvaluationStrategy.FromEnvironment(new StubEnvironmentEvaluator());

        await strategy.EvaluatePopulationAsync(
            genomes,
            (index, fitness) => scores[index] = fitness,
            CancellationToken.None);

        scores[0].Should().Be(30.0); // 3 * 10.0
        scores[1].Should().Be(40.0); // 4 * 10.0
    }

    // --- T032: FromBatch tests ---

    [Fact]
    public void FromBatch_NullEvaluator_ThrowsArgumentNullException()
    {
        var act = () => EvaluationStrategy.FromBatch(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void FromBatch_ReturnsNonNullStrategy()
    {
        var strategy = EvaluationStrategy.FromBatch(new StubBatchEvaluator());

        strategy.Should().NotBeNull();
        strategy.Should().BeAssignableTo<IEvaluationStrategy>();
    }

    [Fact]
    public async Task FromBatch_DelegatesToBatchEvaluator()
    {
        var genome1 = new StubGenome(3, 5);
        var genome2 = new StubGenome(4, 6);
        var genomes = new List<IGenome> { genome1, genome2 };
        var scores = new double[2];

        var strategy = EvaluationStrategy.FromBatch(new StubBatchEvaluator());

        await strategy.EvaluatePopulationAsync(
            genomes,
            (index, fitness) => scores[index] = fitness,
            CancellationToken.None);

        scores[0].Should().Be(30.0); // 3 * 10.0
        scores[1].Should().Be(40.0); // 4 * 10.0
    }

    // --- T033: NeatEvolverExtensions overload tests ---

    [Fact]
    public async Task RunAsync_SyncFunction_DelegatesToEvolver()
    {
        var capturedStrategy = default(IEvaluationStrategy);
        var evolver = new StubEvolver(strategy => capturedStrategy = strategy);

        await evolver.RunAsync(
            g => 1.0,
            CancellationToken.None);

        capturedStrategy.Should().NotBeNull();
        capturedStrategy.Should().BeAssignableTo<IEvaluationStrategy>();
    }

    [Fact]
    public async Task RunAsync_AsyncFunction_DelegatesToEvolver()
    {
        var capturedStrategy = default(IEvaluationStrategy);
        var evolver = new StubEvolver(strategy => capturedStrategy = strategy);

        await evolver.RunAsync(
            (g, ct) => Task.FromResult(1.0),
            CancellationToken.None);

        capturedStrategy.Should().NotBeNull();
        capturedStrategy.Should().BeAssignableTo<IEvaluationStrategy>();
    }

    [Fact]
    public async Task RunAsync_EnvironmentEvaluator_DelegatesToEvolver()
    {
        var capturedStrategy = default(IEvaluationStrategy);
        var evolver = new StubEvolver(strategy => capturedStrategy = strategy);

        await evolver.RunAsync(
            new StubEnvironmentEvaluator(),
            CancellationToken.None);

        capturedStrategy.Should().NotBeNull();
        capturedStrategy.Should().BeAssignableTo<IEvaluationStrategy>();
    }

    [Fact]
    public async Task RunAsync_BatchEvaluator_DelegatesToEvolver()
    {
        var capturedStrategy = default(IEvaluationStrategy);
        var evolver = new StubEvolver(strategy => capturedStrategy = strategy);

        await evolver.RunAsync(
            new StubBatchEvaluator(),
            CancellationToken.None);

        capturedStrategy.Should().NotBeNull();
        capturedStrategy.Should().BeAssignableTo<IEvaluationStrategy>();
    }

    // --- Test doubles ---

    private sealed class StubEnvironmentEvaluator : IEnvironmentEvaluator
    {
        public Task<double> EvaluateAsync(IGenome genome, CancellationToken cancellationToken)
        {
            return Task.FromResult(genome.NodeCount * 10.0);
        }
    }

    private sealed class StubBatchEvaluator : IBatchEvaluator
    {
        public Task EvaluateAsync(
            IReadOnlyList<IGenome> genomes,
            Action<int, double> setFitness,
            CancellationToken cancellationToken)
        {
            for (int i = 0; i < genomes.Count; i++)
            {
                setFitness(i, genomes[i].NodeCount * 10.0);
            }

            return Task.CompletedTask;
        }
    }

    private sealed class StubEvolver(Action<IEvaluationStrategy> onRunAsync) : INeatEvolver
    {
        public Task<EvolutionResult> RunAsync(
            IEvaluationStrategy evaluator,
            CancellationToken cancellationToken = default)
        {
            onRunAsync(evaluator);
            return Task.FromResult<EvolutionResult>(null!);
        }
    }
}
