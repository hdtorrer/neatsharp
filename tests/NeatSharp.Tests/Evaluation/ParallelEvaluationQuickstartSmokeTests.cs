using FluentAssertions;
using NeatSharp.Configuration;
using NeatSharp.Evaluation;
using NeatSharp.Genetics;
using NeatSharp.Tests.TestDoubles;
using Xunit;

namespace NeatSharp.Tests.Evaluation;

/// <summary>
/// Smoke tests that exercise the quickstart.md code examples against the implemented API.
/// Verifies factory overloads, option configuration, and parallel evaluation signatures
/// compile and run correctly.
/// </summary>
public class ParallelEvaluationQuickstartSmokeTests
{
    private static double StubFitnessFunction(IGenome genome) => genome.NodeCount * 1.0;

    private static Task<double> StubAsyncFitnessFunction(IGenome genome, CancellationToken ct)
        => Task.FromResult(genome.NodeCount * 1.0);

    private static IReadOnlyList<IGenome> CreatePopulation()
        => new List<IGenome>
        {
            new StubGenome(3, 5),
            new StubGenome(4, 6),
            new StubGenome(2, 3),
        };

    [Fact]
    public async Task DefaultParallel_AllCores_EvaluatesSuccessfully()
    {
        // Default parallel (all cores)
        var strategy = EvaluationStrategy.FromFunction(
            genome => StubFitnessFunction(genome),
            new EvaluationOptions());

        strategy.Should().NotBeNull();

        var genomes = CreatePopulation();
        var scores = new double[genomes.Count];

        await strategy.EvaluatePopulationAsync(
            genomes,
            (index, fitness) => scores[index] = fitness,
            CancellationToken.None);

        scores[0].Should().Be(3.0);
        scores[1].Should().Be(4.0);
        scores[2].Should().Be(2.0);
    }

    [Fact]
    public async Task SpecificCoreCount_EvaluatesSuccessfully()
    {
        // Specific core count
        var options = new EvaluationOptions { MaxDegreeOfParallelism = 4 };
        var strategy = EvaluationStrategy.FromFunction(
            genome => StubFitnessFunction(genome), options);

        strategy.Should().NotBeNull();

        var genomes = CreatePopulation();
        var scores = new double[genomes.Count];

        await strategy.EvaluatePopulationAsync(
            genomes,
            (index, fitness) => scores[index] = fitness,
            CancellationToken.None);

        scores[0].Should().Be(3.0);
        scores[1].Should().Be(4.0);
        scores[2].Should().Be(2.0);
    }

    [Fact]
    public async Task SequentialOptOut_EvaluatesSuccessfully()
    {
        // Sequential opt-out
        var options = new EvaluationOptions { MaxDegreeOfParallelism = 1 };
        var strategy = EvaluationStrategy.FromFunction(
            genome => StubFitnessFunction(genome), options);

        strategy.Should().NotBeNull();

        var genomes = CreatePopulation();
        var scores = new double[genomes.Count];

        await strategy.EvaluatePopulationAsync(
            genomes,
            (index, fitness) => scores[index] = fitness,
            CancellationToken.None);

        scores[0].Should().Be(3.0);
        scores[1].Should().Be(4.0);
        scores[2].Should().Be(2.0);
    }

    [Fact]
    public async Task AsyncParallel_EvaluatesSuccessfully()
    {
        // Async
        var options = new EvaluationOptions { MaxDegreeOfParallelism = 8 };
        var strategy = EvaluationStrategy.FromFunction(
            async (genome, ct) => await StubAsyncFitnessFunction(genome, ct), options);

        strategy.Should().NotBeNull();

        var genomes = CreatePopulation();
        var scores = new double[genomes.Count];

        await strategy.EvaluatePopulationAsync(
            genomes,
            (index, fitness) => scores[index] = fitness,
            CancellationToken.None);

        scores[0].Should().Be(3.0);
        scores[1].Should().Be(4.0);
        scores[2].Should().Be(2.0);
    }

    [Fact]
    public async Task EnvironmentParallel_AllCores_EvaluatesSuccessfully()
    {
        // Environment
        var options = new EvaluationOptions { MaxDegreeOfParallelism = null };
        var strategy = EvaluationStrategy.FromEnvironment(
            new StubEnvironmentEvaluator(), options);

        strategy.Should().NotBeNull();

        var genomes = CreatePopulation();
        var scores = new double[genomes.Count];

        await strategy.EvaluatePopulationAsync(
            genomes,
            (index, fitness) => scores[index] = fitness,
            CancellationToken.None);

        scores[0].Should().Be(30.0);
        scores[1].Should().Be(40.0);
        scores[2].Should().Be(20.0);
    }

    [Fact]
    public void ErrorHandlingOptions_Compile()
    {
        // Error handling configuration compiles and produces a valid strategy
        var options = new EvaluationOptions
        {
            ErrorMode = EvaluationErrorMode.AssignFitness,
            ErrorFitnessValue = 0.0,
            MaxDegreeOfParallelism = null
        };

        var strategy = EvaluationStrategy.FromFunction(
            genome => StubFitnessFunction(genome), options);

        strategy.Should().NotBeNull();
        strategy.Should().BeAssignableTo<IEvaluationStrategy>();
    }

    private sealed class StubEnvironmentEvaluator : IEnvironmentEvaluator
    {
        public Task<double> EvaluateAsync(IGenome genome, CancellationToken cancellationToken)
        {
            return Task.FromResult(genome.NodeCount * 10.0);
        }
    }
}
