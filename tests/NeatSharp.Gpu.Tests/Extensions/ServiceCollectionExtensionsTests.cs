using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NeatSharp.Evaluation;
using NeatSharp.Extensions;
using NeatSharp.Genetics;
using NeatSharp.Gpu.Configuration;
using NeatSharp.Gpu.Detection;
using NeatSharp.Gpu.Evaluation;
using NeatSharp.Gpu.Extensions;
using Xunit;

namespace NeatSharp.Gpu.Tests.Extensions;

public class ServiceCollectionExtensionsTests
{
    private static ServiceProvider BuildProvider(Action<GpuOptions>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.Debug));
        services.AddNeatSharp(opts =>
        {
            opts.InputCount = 2;
            opts.OutputCount = 1;
            opts.PopulationSize = 10;
            opts.Stopping.MaxGenerations = 1;
        });
        services.AddSingleton<IGpuFitnessFunction, TestFitnessFunction>();
        services.AddNeatSharpGpu(configure);
        return services.BuildServiceProvider();
    }

    [Fact]
    public void AddNeatSharpGpu_RegistersIGpuDeviceDetector()
    {
        using var provider = BuildProvider();

        var detector = provider.GetService<IGpuDeviceDetector>();

        detector.Should().NotBeNull();
    }

    [Fact]
    public void AddNeatSharpGpu_RegistersIBatchEvaluator()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();

        var evaluator = scope.ServiceProvider.GetService<IBatchEvaluator>();

        evaluator.Should().NotBeNull();
        evaluator.Should().BeOfType<GpuBatchEvaluator>();
    }

    [Fact]
    public void AddNeatSharpGpu_ReplacesINetworkBuilderWithGpuNetworkBuilder()
    {
        using var provider = BuildProvider();

        var builder = provider.GetService<INetworkBuilder>();

        builder.Should().NotBeNull();
        builder.Should().BeOfType<GpuNetworkBuilder>();
    }

    [Fact]
    public void AddNeatSharpGpu_GpuNetworkBuilder_ProducesGpuFeedForwardNetwork()
    {
        using var provider = BuildProvider();
        var builder = provider.GetRequiredService<INetworkBuilder>();

        var nodes = new NodeGene[]
        {
            new(0, NodeType.Input),
            new(1, NodeType.Input),
            new(2, NodeType.Output),
        };
        var connections = new ConnectionGene[]
        {
            new(1, 0, 2, 1.0, true),
            new(2, 1, 2, 1.0, true),
        };
        var genome = new Genome(nodes, connections);
        var network = builder.Build(genome);

        network.Should().BeOfType<GpuFeedForwardNetwork>();
    }

    [Fact]
    public void AddNeatSharpGpu_ConfiguresGpuOptions()
    {
        using var provider = BuildProvider(gpu =>
        {
            gpu.EnableGpu = false;
            gpu.MinComputeCapability = 60;
        });

        var options = provider.GetRequiredService<IOptions<GpuOptions>>().Value;

        options.EnableGpu.Should().BeFalse();
        options.MinComputeCapability.Should().Be(60);
    }

    [Fact]
    public void AddNeatSharpGpu_EnableGpuFalse_EvaluatorStillResolvable()
    {
        using var provider = BuildProvider(gpu => gpu.EnableGpu = false);
        using var scope = provider.CreateScope();

        var evaluator = scope.ServiceProvider.GetService<IBatchEvaluator>();

        evaluator.Should().NotBeNull();
    }

    private class TestFitnessFunction : IGpuFitnessFunction
    {
        public int CaseCount => 1;
        public int OutputCount => 1;
        public ReadOnlyMemory<float> InputCases => new float[] { 0f, 0f };
        public double ComputeFitness(ReadOnlySpan<float> outputs) => 1.0;
    }
}
