using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NeatSharp.Evolution;
using NeatSharp.Extensions;
using NeatSharp.Genetics;
using Xunit;

namespace NeatSharp.Tests.Examples;

public class XorExampleTests
{
    private static readonly double[][] XorInputs = [[0, 0], [0, 1], [1, 0], [1, 1]];
    private static readonly double[] XorExpected = [0, 1, 1, 0];

    private static double XorFitness(IGenome genome)
    {
        double fitness = 0;
        Span<double> output = stackalloc double[1];
        for (int i = 0; i < 4; i++)
        {
            genome.Activate(XorInputs[i], output);
            double error = Math.Abs(XorExpected[i] - output[0]);
            fitness += 1.0 - error;
        }
        return fitness;
    }

    private static (INeatEvolver Evolver, IServiceScope Scope, ServiceProvider Provider) CreateXorEvolver(
        int seed, int maxGenerations = 150, double fitnessTarget = 3.9)
    {
        var services = new ServiceCollection();
        services.AddNeatSharp(options =>
        {
            options.InputCount = 2;
            options.OutputCount = 1;
            options.PopulationSize = 150;
            options.Seed = seed;
            options.EnableMetrics = false;
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
    public async Task XorSolvedWithin150Generations()
    {
        // Arrange
        var (evolver, scope, provider) = CreateXorEvolver(seed: 300);

        try
        {
            // Act
            var result = await evolver.RunAsync(XorFitness);

            // Assert (SC-001): XOR solved within 150 generations
            result.Champion.Fitness.Should().BeGreaterThanOrEqualTo(3.9,
                "champion should solve XOR with fitness >= 3.9");
            result.History.TotalGenerations.Should().BeLessThanOrEqualTo(150);
        }
        finally
        {
            scope.Dispose();
            provider.Dispose();
        }
    }

    [Fact]
    public async Task ChampionProducesCorrectXorOutputs()
    {
        // Arrange
        var (evolver, scope, provider) = CreateXorEvolver(seed: 300);

        try
        {
            // Act
            var result = await evolver.RunAsync(XorFitness);

            // Assert: champion should produce correct outputs for all four XOR cases
            var champion = result.Champion.Genome;
            Span<double> output = stackalloc double[1];
            double tolerance = 0.3; // Allow some tolerance for analog outputs

            for (int i = 0; i < 4; i++)
            {
                champion.Activate(XorInputs[i], output);
                output[0].Should().BeApproximately(XorExpected[i], tolerance,
                    $"XOR({XorInputs[i][0]}, {XorInputs[i][1]}) should be {XorExpected[i]}");
            }
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
        var (evolver1, scope1, provider1) = CreateXorEvolver(seed: 300);
        var (evolver2, scope2, provider2) = CreateXorEvolver(seed: 300);

        try
        {
            // Act
            var result1 = await evolver1.RunAsync(XorFitness);
            var result2 = await evolver2.RunAsync(XorFitness);

            // Assert (SC-003): identical seed produces identical results
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

    [Fact]
    public async Task StopsAtMaxGenerations_ReturnsBestGenomeFound()
    {
        // Arrange: use impossible fitness target so it runs to MaxGenerations
        var (evolver, scope, provider) = CreateXorEvolver(
            seed: 42, maxGenerations: 5, fitnessTarget: 100.0);

        try
        {
            // Act
            var result = await evolver.RunAsync(XorFitness);

            // Assert: should reach MaxGenerations and return the best genome found
            result.History.TotalGenerations.Should().Be(5);
            result.Champion.Should().NotBeNull();
            result.Champion.Fitness.Should().BeGreaterThan(0);
            result.WasCancelled.Should().BeFalse();
        }
        finally
        {
            scope.Dispose();
            provider.Dispose();
        }
    }
}
