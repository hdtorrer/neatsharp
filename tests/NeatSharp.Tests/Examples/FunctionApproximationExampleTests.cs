using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NeatSharp.Evolution;
using NeatSharp.Extensions;
using NeatSharp.Genetics;
using Xunit;

namespace NeatSharp.Tests.Examples;

public class FunctionApproximationExampleTests
{
    private const int SampleCount = 20;

    private static readonly double[] SampleInputs;
    private static readonly double[] SampleExpected;

    static FunctionApproximationExampleTests()
    {
        SampleInputs = new double[SampleCount];
        SampleExpected = new double[SampleCount];
        for (int i = 0; i < SampleCount; i++)
        {
            SampleInputs[i] = i * 2.0 * Math.PI / (SampleCount - 1);
            SampleExpected[i] = (Math.Sin(SampleInputs[i]) + 1.0) / 2.0; // Normalize to [0, 1]
        }
    }

    private static double SineFitness(IGenome genome)
    {
        double mse = 0;
        Span<double> output = stackalloc double[1];
        for (int i = 0; i < SampleCount; i++)
        {
            genome.Activate([SampleInputs[i] / (2.0 * Math.PI)], output); // Normalize input to [0, 1]
            double error = SampleExpected[i] - output[0];
            mse += error * error;
        }
        mse /= SampleCount;
        return 1.0 / (1.0 + mse); // Fitness in (0, 1]
    }

    private static (INeatEvolver Evolver, IServiceScope Scope, ServiceProvider Provider) CreateSineEvolver(
        int seed, int maxGenerations = 500, double fitnessTarget = 0.95, bool enableMetrics = false)
    {
        var services = new ServiceCollection();
        services.AddNeatSharp(options =>
        {
            options.InputCount = 1;
            options.OutputCount = 1;
            options.PopulationSize = 150;
            options.Seed = seed;
            options.EnableMetrics = enableMetrics;
            options.Stopping.MaxGenerations = maxGenerations;
            options.Stopping.FitnessTarget = fitnessTarget;
        });
        services.AddLogging();

        var provider = services.BuildServiceProvider();
        var scope = provider.CreateScope();
        var evolver = scope.ServiceProvider.GetRequiredService<INeatEvolver>();
        return (evolver, scope, provider);
    }

    [Fact]
    public async Task ChampionFitnessMeetsThresholdWithin500Generations()
    {
        // Arrange
        var (evolver, scope, provider) = CreateSineEvolver(seed: 123);

        try
        {
            // Act
            var result = await evolver.RunAsync(SineFitness);

            // Assert (SC-002, FR-024, FR-025): champion fitness meets threshold within 500 generations
            result.Champion.Fitness.Should().BeGreaterThanOrEqualTo(0.95,
                "champion should approximate sin(x) with fitness >= 0.95");
            result.History.TotalGenerations.Should().BeLessThanOrEqualTo(500);
        }
        finally
        {
            scope.Dispose();
            provider.Dispose();
        }
    }

    [Fact]
    public async Task MetricsShowProgressiveFitnessImprovement()
    {
        // Arrange
        var (evolver, scope, provider) = CreateSineEvolver(seed: 123, enableMetrics: true);

        try
        {
            // Act
            var result = await evolver.RunAsync(SineFitness);

            // Assert (SC-004): metrics recorded each generation with progressive improvement
            result.History.Generations.Should().NotBeEmpty(
                "metrics should be recorded when EnableMetrics=true");

            // Verify generation numbers are sequential starting from 0
            for (int i = 0; i < result.History.Generations.Count; i++)
            {
                result.History.Generations[i].Generation.Should().Be(i);
            }

            // Verify progressive improvement: the best fitness in the final generation
            // should be higher than the best fitness in the first generation
            var firstGen = result.History.Generations[0];
            var lastGen = result.History.Generations[^1];
            lastGen.BestFitness.Should().BeGreaterThanOrEqualTo(firstGen.BestFitness,
                "fitness should improve over generations");

            // Additionally verify that running max best fitness is non-decreasing
            // across the run (best fitness found so far never decreases)
            double runningBest = 0;
            foreach (var gen in result.History.Generations)
            {
                if (gen.BestFitness > runningBest)
                {
                    runningBest = gen.BestFitness;
                }
            }
            runningBest.Should().BeGreaterThanOrEqualTo(0.95,
                "running best fitness should reach the target threshold");
        }
        finally
        {
            scope.Dispose();
            provider.Dispose();
        }
    }

    [Fact]
    public async Task TwoRunsWithSameSeed_ProduceIdenticalResults()
    {
        // Arrange
        var (evolver1, scope1, provider1) = CreateSineEvolver(seed: 123);
        var (evolver2, scope2, provider2) = CreateSineEvolver(seed: 123);

        try
        {
            // Act
            var result1 = await evolver1.RunAsync(SineFitness);
            var result2 = await evolver2.RunAsync(SineFitness);

            // Assert: deterministic — same seed produces identical results
            result1.Seed.Should().Be(result2.Seed);
            result1.Champion.Fitness.Should().Be(result2.Champion.Fitness);
            result1.Champion.Generation.Should().Be(result2.Champion.Generation);
            result1.History.TotalGenerations.Should().Be(result2.History.TotalGenerations);
        }
        finally
        {
            scope1.Dispose();
            provider1.Dispose();
            scope2.Dispose();
            provider2.Dispose();
        }
    }
}
