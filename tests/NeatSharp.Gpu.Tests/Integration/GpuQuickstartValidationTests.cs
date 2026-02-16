using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NeatSharp.Evaluation;
using NeatSharp.Evolution;
using NeatSharp.Extensions;
using NeatSharp.Gpu.Evaluation;
using NeatSharp.Gpu.Extensions;
using Xunit;

namespace NeatSharp.Gpu.Tests.Integration;

/// <summary>
/// Validates that all code samples in specs/006-cuda-evaluator/quickstart.md
/// compile and run correctly against the implemented API.
/// </summary>
public class GpuQuickstartValidationTests
{
    /// <summary>
    /// Validates the "Basic Usage: XOR with GPU Evaluation" quickstart sample.
    /// This is the primary end-to-end scenario from quickstart.md.
    /// </summary>
    [Fact]
    public async Task BasicUsage_XorWithGpuEvaluation_CompletesSuccessfully()
    {
        // 1. Define your fitness function for GPU evaluation
        var xorFitness = new XorFitnessFunction();

        // 2. Configure services
        var services = new ServiceCollection();
        services.AddNeatSharp(options =>
        {
            options.InputCount = 2;
            options.OutputCount = 1;
            options.PopulationSize = 500;
            options.Seed = 42;
            options.Stopping.MaxGenerations = 200;
            options.Stopping.FitnessTarget = 3.9;
        });
        services.AddSingleton<IGpuFitnessFunction>(xorFitness);
        services.AddNeatSharpGpu();
        services.AddLogging();

        // 3. Build and run
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var evolver = scope.ServiceProvider.GetRequiredService<INeatEvolver>();
        var batchEvaluator = scope.ServiceProvider.GetRequiredService<IBatchEvaluator>();

        try
        {
            var result = await evolver.RunAsync(batchEvaluator);

            result.Champion.Fitness.Should().BeGreaterThan(0,
                "quickstart XOR sample should produce a valid champion");
            result.WasCancelled.Should().BeFalse();
        }
        finally
        {
            (batchEvaluator as IDisposable)?.Dispose();
        }
    }

    /// <summary>
    /// Validates the "GPU Configuration Options" quickstart sample.
    /// </summary>
    [Fact]
    public async Task GpuConfigurationOptions_AllOptionsApplied_Compiles()
    {
        var services = new ServiceCollection();
        services.AddNeatSharp(options =>
        {
            options.InputCount = 2;
            options.OutputCount = 1;
            options.PopulationSize = 150;
            options.Seed = 42;
            options.Stopping.MaxGenerations = 5;
            options.Stopping.FitnessTarget = 100.0;
        });
        services.AddSingleton<IGpuFitnessFunction, XorFitnessFunction>();
        services.AddNeatSharpGpu(gpu =>
        {
            gpu.EnableGpu = true;
            gpu.MinComputeCapability = 50;
            gpu.BestEffortDeterministic = false;
            gpu.MaxPopulationSize = 2000;
        });
        services.AddLogging();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var evolver = scope.ServiceProvider.GetRequiredService<INeatEvolver>();
        var batchEvaluator = scope.ServiceProvider.GetRequiredService<IBatchEvaluator>();

        try
        {
            var result = await evolver.RunAsync(batchEvaluator);

            result.Should().NotBeNull();
            result.Champion.Should().NotBeNull();
        }
        finally
        {
            (batchEvaluator as IDisposable)?.Dispose();
        }
    }

    /// <summary>
    /// Validates the "Force CPU-Only (Disable GPU)" quickstart sample.
    /// Verifies the API compiles and the training pipeline completes without errors
    /// when GPU is explicitly disabled.
    /// </summary>
    [Fact]
    public async Task ForceCpuOnly_DisableGpu_CompletesSuccessfully()
    {
        var services = new ServiceCollection();
        services.AddNeatSharp(options =>
        {
            options.InputCount = 2;
            options.OutputCount = 1;
            options.PopulationSize = 150;
            options.Seed = 42;
            options.Stopping.MaxGenerations = 10;
            options.Stopping.FitnessTarget = 100.0;
        });
        services.AddSingleton<IGpuFitnessFunction, XorFitnessFunction>();
        services.AddNeatSharpGpu(gpu =>
        {
            gpu.EnableGpu = false;
        });
        services.AddLogging();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var evolver = scope.ServiceProvider.GetRequiredService<INeatEvolver>();
        var batchEvaluator = scope.ServiceProvider.GetRequiredService<IBatchEvaluator>();

        try
        {
            var result = await evolver.RunAsync(batchEvaluator);

            result.Champion.Should().NotBeNull();
            result.Champion.Fitness.Should().BeGreaterThan(0,
                "CPU-only mode should produce valid fitness scores");
            result.History.TotalGenerations.Should().Be(10);
            result.WasCancelled.Should().BeFalse();
        }
        finally
        {
            (batchEvaluator as IDisposable)?.Dispose();
        }
    }

    /// <summary>
    /// Validates the XorFitnessFunction implementation from quickstart.md.
    /// </summary>
    [Fact]
    public void XorFitnessFunction_MatchesQuickstartContract()
    {
        var fitness = new XorFitnessFunction();

        fitness.CaseCount.Should().Be(4, "XOR has 4 test cases");
        fitness.InputCases.Length.Should().Be(8, "4 cases * 2 inputs = 8 floats");

        // Perfect XOR outputs: [0, 1, 1, 0]
        float[] perfectOutputs = [0f, 1f, 1f, 0f];
        double perfectFitness = fitness.ComputeFitness(perfectOutputs);
        perfectFitness.Should().Be(4.0, "perfect XOR should yield max fitness of 4.0");

        // All-zero outputs
        float[] zeroOutputs = [0f, 0f, 0f, 0f];
        double zeroFitness = fitness.ComputeFitness(zeroOutputs);
        zeroFitness.Should().Be(2.0, "all-zero outputs yield fitness 2.0 (two correct for XOR)");
    }

    /// <summary>
    /// XOR fitness function matching the quickstart.md code sample exactly.
    /// </summary>
    private sealed class XorFitnessFunction : IGpuFitnessFunction
    {
        private static readonly float[] Inputs =
        [
            0f, 0f,
            0f, 1f,
            1f, 0f,
            1f, 1f
        ];

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
}
