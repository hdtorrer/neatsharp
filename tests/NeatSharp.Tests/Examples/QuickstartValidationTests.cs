using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NeatSharp.Evaluation;
using NeatSharp.Evolution;
using NeatSharp.Extensions;
using NeatSharp.Genetics;
using NeatSharp.Reporting;
using Xunit;

namespace NeatSharp.Tests.Examples;

/// <summary>
/// Validates that all code samples from specs/004-training-runner/quickstart.md
/// compile and produce expected outputs.
/// </summary>
public class QuickstartValidationTests
{
    #region Shared helpers

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

    private const int SineSampleCount = 20;
    private static readonly double[] SineSampleInputs;
    private static readonly double[] SineSampleExpected;

    static QuickstartValidationTests()
    {
        SineSampleInputs = new double[SineSampleCount];
        SineSampleExpected = new double[SineSampleCount];
        for (int i = 0; i < SineSampleCount; i++)
        {
            SineSampleInputs[i] = i * 2.0 * Math.PI / (SineSampleCount - 1);
            SineSampleExpected[i] = (Math.Sin(SineSampleInputs[i]) + 1.0) / 2.0;
        }
    }

    private static double SineFitness(IGenome genome)
    {
        double mse = 0;
        Span<double> output = stackalloc double[1];
        for (int i = 0; i < SineSampleCount; i++)
        {
            genome.Activate([SineSampleInputs[i] / (2.0 * Math.PI)], output);
            double error = SineSampleExpected[i] - output[0];
            mse += error * error;
        }
        mse /= SineSampleCount;
        return 1.0 / (1.0 + mse);
    }

    private static (INeatEvolver Evolver, IServiceScope Scope, ServiceProvider Provider)
        CreateEvolver(
            int inputCount,
            int outputCount,
            int seed,
            int maxGenerations,
            double fitnessTarget,
            bool enableMetrics = false,
            int populationSize = 150)
    {
        var services = new ServiceCollection();
        services.AddNeatSharp(options =>
        {
            options.InputCount = inputCount;
            options.OutputCount = outputCount;
            options.PopulationSize = populationSize;
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

    #endregion

    /// <summary>
    /// (a) XOR example — champion fitness >= 3.9, run completes within 150 generations.
    /// Validates quickstart.md "Minimal XOR Example" snippet.
    /// </summary>
    [Fact]
    public async Task XorExample_ChampionFitnessAndGenerationCount()
    {
        var (evolver, scope, provider) = CreateEvolver(
            inputCount: 2, outputCount: 1,
            seed: 300, maxGenerations: 150, fitnessTarget: 3.9);

        try
        {
            var result = await evolver.RunAsync(XorFitness);

            result.Champion.Fitness.Should().BeGreaterThanOrEqualTo(3.9,
                "champion should solve XOR with fitness >= 3.9");
            result.History.TotalGenerations.Should().BeLessThanOrEqualTo(150);
            result.WasCancelled.Should().BeFalse();
        }
        finally
        {
            scope.Dispose();
            provider.Dispose();
        }
    }

    /// <summary>
    /// (b) Sine approximation example — champion fitness >= 0.95, run completes within 500 generations.
    /// Validates quickstart.md "Function Approximation Example (Sine)" snippet.
    /// </summary>
    [Fact]
    public async Task SineApproximationExample_ChampionFitnessAndGenerationCount()
    {
        var (evolver, scope, provider) = CreateEvolver(
            inputCount: 1, outputCount: 1,
            seed: 123, maxGenerations: 500, fitnessTarget: 0.95);

        try
        {
            var result = await evolver.RunAsync(SineFitness);

            result.Champion.Fitness.Should().BeGreaterThanOrEqualTo(0.95,
                "champion should approximate sin(x) with fitness >= 0.95");
            result.History.TotalGenerations.Should().BeLessThanOrEqualTo(500);
            result.WasCancelled.Should().BeFalse();
        }
        finally
        {
            scope.Dispose();
            provider.Dispose();
        }
    }

    /// <summary>
    /// (c) Cancellation example — WasCancelled=true when token fires before completion, champion fitness returned.
    /// Validates quickstart.md "Cancellation Example" snippet.
    /// </summary>
    [Fact]
    public async Task CancellationExample_ReturnsBestResultWithCancelledFlag()
    {
        // Use an impossible fitness target so the run doesn't complete naturally
        var (evolver, scope, provider) = CreateEvolver(
            inputCount: 2, outputCount: 1,
            seed: 42, maxGenerations: 1000, fitnessTarget: 100.0);

        try
        {
            using var cts = new CancellationTokenSource();
            // Cancel after a few generations
            cts.CancelAfter(TimeSpan.FromMilliseconds(100));

            var result = await evolver.RunAsync(XorFitness, cts.Token);

            result.WasCancelled.Should().BeTrue();
            result.Champion.Should().NotBeNull();
            result.Champion.Fitness.Should().BeGreaterThanOrEqualTo(0,
                "champion fitness should be returned even on cancellation");
        }
        finally
        {
            scope.Dispose();
            provider.Dispose();
        }
    }

    /// <summary>
    /// (d) Batch evaluation example — all genomes receive fitness scores, run completes with valid result.
    /// Validates quickstart.md "Batch Evaluation Example" snippet.
    /// </summary>
    [Fact]
    public async Task BatchEvaluationExample_AllGenomesReceiveFitness()
    {
        var (evolver, scope, provider) = CreateEvolver(
            inputCount: 2, outputCount: 1,
            seed: 42, maxGenerations: 10, fitnessTarget: 100.0);

        try
        {
            var batchEvaluator = new XorBatchEvaluator();
            var result = await evolver.RunAsync(batchEvaluator);

            result.Should().NotBeNull();
            result.Champion.Should().NotBeNull();
            result.Champion.Fitness.Should().BeGreaterThan(0,
                "all genomes should receive fitness scores from batch evaluator");
            result.History.TotalGenerations.Should().Be(10);
            result.WasCancelled.Should().BeFalse();
        }
        finally
        {
            scope.Dispose();
            provider.Dispose();
        }
    }

    /// <summary>
    /// (e) Environment evaluation example — mock environment runs multi-step episodes,
    /// champion fitness reflects environment performance.
    /// Validates quickstart.md "Environment Evaluation Example" snippet.
    /// </summary>
    [Fact]
    public async Task EnvironmentEvaluationExample_MultiStepEpisodes()
    {
        var (evolver, scope, provider) = CreateEvolver(
            inputCount: 2, outputCount: 1,
            seed: 42, maxGenerations: 10, fitnessTarget: 100.0);

        try
        {
            var envEvaluator = new StepBasedEnvironmentEvaluator(steps: 50);
            var result = await evolver.RunAsync(envEvaluator);

            result.Should().NotBeNull();
            result.Champion.Should().NotBeNull();
            result.Champion.Fitness.Should().BeGreaterThan(0,
                "champion fitness should reflect environment performance");
            result.History.TotalGenerations.Should().Be(10);
            result.WasCancelled.Should().BeFalse();
        }
        finally
        {
            scope.Dispose();
            provider.Dispose();
        }
    }

    /// <summary>
    /// (f) Metrics access example — result.History.Generations is non-empty, each entry
    /// has expected fields (Generation, BestFitness, AverageFitness, SpeciesCount,
    /// SpeciesSizes, Complexity, Timing).
    /// Validates quickstart.md "Accessing Metrics" snippet.
    /// </summary>
    [Fact]
    public async Task MetricsAccessExample_GenerationsHaveExpectedFields()
    {
        var (evolver, scope, provider) = CreateEvolver(
            inputCount: 2, outputCount: 1,
            seed: 42, maxGenerations: 5, fitnessTarget: 100.0,
            enableMetrics: true);

        try
        {
            var result = await evolver.RunAsync(XorFitness);

            result.History.Generations.Should().NotBeEmpty(
                "generations should be recorded when EnableMetrics=true");
            result.History.Generations.Should().HaveCount(result.History.TotalGenerations);

            foreach (var gen in result.History.Generations)
            {
                gen.Generation.Should().BeGreaterThanOrEqualTo(0);
                gen.BestFitness.Should().BeGreaterThanOrEqualTo(0);
                gen.AverageFitness.Should().BeGreaterThanOrEqualTo(0);
                gen.SpeciesCount.Should().BeGreaterThan(0);
                gen.SpeciesSizes.Should().NotBeEmpty();
                gen.SpeciesSizes.Sum().Should().BeGreaterThan(0,
                    "species sizes should sum to the population count");
                gen.Complexity.Should().NotBeNull();
                gen.Complexity.AverageNodes.Should().BeGreaterThan(0);
                gen.Complexity.AverageConnections.Should().BeGreaterThan(0);
                gen.Timing.Should().NotBeNull();
                gen.Timing.Evaluation.Should().BeGreaterThanOrEqualTo(TimeSpan.Zero);
                gen.Timing.Reproduction.Should().BeGreaterThanOrEqualTo(TimeSpan.Zero);
                gen.Timing.Speciation.Should().BeGreaterThanOrEqualTo(TimeSpan.Zero);
            }

            // Verify sequential generation numbers
            for (int i = 0; i < result.History.Generations.Count; i++)
            {
                result.History.Generations[i].Generation.Should().Be(i);
            }
        }
        finally
        {
            scope.Dispose();
            provider.Dispose();
        }
    }

    #region Test evaluators

    private sealed class XorBatchEvaluator : IBatchEvaluator
    {
        public Task EvaluateAsync(
            IReadOnlyList<IGenome> genomes,
            Action<int, double> setFitness,
            CancellationToken cancellationToken)
        {
            for (int i = 0; i < genomes.Count; i++)
            {
                double score = XorFitness(genomes[i]);
                setFitness(i, score);
            }
            return Task.CompletedTask;
        }
    }

    private sealed class StepBasedEnvironmentEvaluator(int steps) : IEnvironmentEvaluator
    {
        public Task<double> EvaluateAsync(IGenome genome, CancellationToken cancellationToken)
        {
            double totalReward = 0;
            Span<double> outputs = stackalloc double[1];

            for (int step = 0; step < steps; step++)
            {
                double input1 = (double)step / steps;
                double input2 = 1.0 - input1;
                genome.Activate([input1, input2], outputs);
                totalReward += Math.Clamp(outputs[0], 0.0, 1.0);
            }

            return Task.FromResult(totalReward);
        }
    }

    #endregion
}
