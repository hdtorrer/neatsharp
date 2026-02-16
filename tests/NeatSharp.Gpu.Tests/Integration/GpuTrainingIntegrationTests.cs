using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NeatSharp.Evaluation;
using NeatSharp.Evolution;
using NeatSharp.Extensions;
using NeatSharp.Genetics;
using NeatSharp.Gpu.Configuration;
using NeatSharp.Gpu.Detection;
using NeatSharp.Gpu.Evaluation;
using NeatSharp.Gpu.Extensions;
using Xunit;

namespace NeatSharp.Gpu.Tests.Integration;

/// <summary>
/// Full end-to-end integration tests running the NeatEvolver training loop
/// with GpuBatchEvaluator on the ILGPU CPU accelerator.
/// Validates pipeline correctness, XOR training progress, and GPU-to-CPU fallback.
/// </summary>
/// <remarks>
/// These tests validate the integration of NeatEvolver with GpuBatchEvaluator.
/// The GPU fp32 evaluation path produces a different fitness landscape than the
/// CPU fp64 path, which changes evolutionary dynamics. Convergence thresholds
/// are set conservatively to avoid flaky tests; specific XOR convergence is
/// validated by the core NeatSharp tests using the fp64 CPU path.
/// </remarks>
public class GpuTrainingIntegrationTests
{
    private sealed class XorFitnessFunction : IGpuFitnessFunction
    {
        private static readonly float[] Inputs = [0f, 0f, 0f, 1f, 1f, 0f, 1f, 1f];
        private static readonly float[] Expected = [0f, 1f, 1f, 0f];

        public int CaseCount => 4;
        public int OutputCount => 1;
        public ReadOnlyMemory<float> InputCases => Inputs;

        public double ComputeFitness(ReadOnlySpan<float> outputs)
        {
            double fitness = 0;
            for (int i = 0; i < CaseCount; i++)
            {
                double error = Math.Abs(Expected[i] - outputs[i]);
                fitness += 1.0 - error;
            }
            return fitness;
        }
    }

    private static (INeatEvolver Evolver, IBatchEvaluator BatchEvaluator, IServiceScope Scope, ServiceProvider Provider)
        CreateGpuEvolver(
            int seed,
            int maxGenerations = 150,
            double fitnessTarget = 3.9,
            int populationSize = 150,
            bool enableMetrics = false,
            Action<GpuOptions>? configureGpu = null)
    {
        var services = new ServiceCollection();
        services.AddNeatSharp(options =>
        {
            options.InputCount = 2;
            options.OutputCount = 1;
            options.PopulationSize = populationSize;
            options.Seed = seed;
            options.EnableMetrics = enableMetrics;
            options.Stopping.MaxGenerations = maxGenerations;
            options.Stopping.FitnessTarget = fitnessTarget;
        });
        services.AddSingleton<IGpuFitnessFunction, XorFitnessFunction>();
        services.AddNeatSharpGpu(configureGpu);
        services.AddLogging();

        var provider = services.BuildServiceProvider();
        var scope = provider.CreateScope();
        var evolver = scope.ServiceProvider.GetRequiredService<INeatEvolver>();
        var batchEvaluator = scope.ServiceProvider.GetRequiredService<IBatchEvaluator>();
        return (evolver, batchEvaluator, scope, provider);
    }

    [Fact]
    public async Task XorTraining_WithGpuEvaluator_ProducesValidFitness()
    {
        var (evolver, batchEvaluator, scope, provider) = CreateGpuEvolver(
            seed: 42, populationSize: 150, maxGenerations: 50);

        try
        {
            var result = await evolver.RunAsync(batchEvaluator);

            // Pipeline validation: training ran, champion has positive fitness,
            // and fitness is better than random (a random network gets ~2.0 on XOR)
            result.Champion.Should().NotBeNull();
            result.Champion.Fitness.Should().BeGreaterThan(2.0,
                "GPU-evaluated XOR fitness should exceed random baseline after 50 generations");
            result.History.TotalGenerations.Should().BeLessThanOrEqualTo(50);
            result.WasCancelled.Should().BeFalse();
        }
        finally
        {
            (batchEvaluator as IDisposable)?.Dispose();
            scope.Dispose();
            provider.Dispose();
        }
    }

    [Fact]
    public async Task XorTraining_ChampionGenomeIsFunctional()
    {
        var (evolver, batchEvaluator, scope, provider) = CreateGpuEvolver(
            seed: 42, populationSize: 150, maxGenerations: 20);

        try
        {
            var result = await evolver.RunAsync(batchEvaluator);

            result.Champion.Should().NotBeNull();
            result.Champion.Fitness.Should().BeGreaterThan(0);
            result.Champion.Genome.Should().NotBeNull();

            // Verify the champion genome is functional (can activate via CPU phenotype)
            var champion = result.Champion.Genome;
            champion.NodeCount.Should().BeGreaterThan(0);
            Span<double> output = stackalloc double[1];
            champion.Activate([0.0, 0.0], output);
            output[0].Should().BeInRange(0.0, 1.0, "output should be in valid sigmoid range");

            champion.Activate([1.0, 1.0], output);
            output[0].Should().BeInRange(0.0, 1.0, "output should be in valid sigmoid range");
        }
        finally
        {
            (batchEvaluator as IDisposable)?.Dispose();
            scope.Dispose();
            provider.Dispose();
        }
    }

    [Fact]
    public async Task XorTraining_WithGpuDisabled_FallsBackToCpuAndCompletes()
    {
        var (evolver, batchEvaluator, scope, provider) = CreateGpuEvolver(
            seed: 42, populationSize: 150, maxGenerations: 20,
            configureGpu: gpu => gpu.EnableGpu = false);

        try
        {
            var result = await evolver.RunAsync(batchEvaluator);

            // CPU fallback should still produce valid training results
            result.Champion.Should().NotBeNull();
            result.Champion.Fitness.Should().BeGreaterThan(0);
            result.History.TotalGenerations.Should().BeLessThanOrEqualTo(20);
            result.WasCancelled.Should().BeFalse();
        }
        finally
        {
            (batchEvaluator as IDisposable)?.Dispose();
            scope.Dispose();
            provider.Dispose();
        }
    }

    [Fact]
    public async Task XorTraining_WithCancellation_ReturnsBestResultSoFar()
    {
        var (evolver, batchEvaluator, scope, provider) = CreateGpuEvolver(
            seed: 42,
            maxGenerations: 1000,
            fitnessTarget: 100.0);

        try
        {
            using var cts = new CancellationTokenSource();
            cts.CancelAfter(TimeSpan.FromMilliseconds(500));

            var result = await evolver.RunAsync(batchEvaluator, cts.Token);

            result.WasCancelled.Should().BeTrue();
            result.Champion.Should().NotBeNull();
            result.Champion.Fitness.Should().BeGreaterThanOrEqualTo(0,
                "champion fitness should be returned even on cancellation");
        }
        finally
        {
            (batchEvaluator as IDisposable)?.Dispose();
            scope.Dispose();
            provider.Dispose();
        }
    }

    [Fact]
    public async Task XorTraining_StopsAtMaxGenerations_WhenFitnessTargetNotReached()
    {
        var (evolver, batchEvaluator, scope, provider) = CreateGpuEvolver(
            seed: 42,
            maxGenerations: 5,
            fitnessTarget: 100.0);

        try
        {
            var result = await evolver.RunAsync(batchEvaluator);

            result.History.TotalGenerations.Should().Be(5);
            result.Champion.Should().NotBeNull();
            result.Champion.Fitness.Should().BeGreaterThan(0);
            result.WasCancelled.Should().BeFalse();
        }
        finally
        {
            (batchEvaluator as IDisposable)?.Dispose();
            scope.Dispose();
            provider.Dispose();
        }
    }

    [Fact]
    public async Task XorTraining_MultipleGenerations_FitnessImprovesOverTime()
    {
        var (evolver, batchEvaluator, scope, provider) = CreateGpuEvolver(
            seed: 300, populationSize: 150, maxGenerations: 50,
            enableMetrics: true);

        try
        {
            var result = await evolver.RunAsync(batchEvaluator);

            // With metrics enabled, generations should be recorded
            result.History.Generations.Should().NotBeEmpty();

            // Best fitness at the end should be >= first generation's best
            var firstGen = result.History.Generations[0];
            var lastGen = result.History.Generations[^1];
            lastGen.BestFitness.Should().BeGreaterThanOrEqualTo(firstGen.BestFitness,
                "fitness should improve or stay the same over generations");
        }
        finally
        {
            (batchEvaluator as IDisposable)?.Dispose();
            scope.Dispose();
            provider.Dispose();
        }
    }

    [Fact]
    public void DiRegistration_ResolvesAllGpuServices()
    {
        var services = new ServiceCollection();
        services.AddNeatSharp(options =>
        {
            options.InputCount = 2;
            options.OutputCount = 1;
            options.PopulationSize = 150;
            options.Seed = 42;
            options.Stopping.MaxGenerations = 10;
        });
        services.AddSingleton<IGpuFitnessFunction, XorFitnessFunction>();
        services.AddNeatSharpGpu();
        services.AddLogging();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var evolver = scope.ServiceProvider.GetRequiredService<INeatEvolver>();
        var batchEvaluator = scope.ServiceProvider.GetRequiredService<IBatchEvaluator>();
        var networkBuilder = scope.ServiceProvider.GetRequiredService<INetworkBuilder>();
        var deviceDetector = scope.ServiceProvider.GetRequiredService<IGpuDeviceDetector>();

        evolver.Should().NotBeNull();
        batchEvaluator.Should().BeOfType<GpuBatchEvaluator>();
        networkBuilder.Should().BeOfType<GpuNetworkBuilder>();
        deviceDetector.Should().NotBeNull();

        (batchEvaluator as IDisposable)?.Dispose();
    }
}
