using FluentAssertions;
using Microsoft.Extensions.Options;
using NeatSharp.Configuration;
using NeatSharp.Evolution.Mutation;
using NeatSharp.Genetics;
using Xunit;

namespace NeatSharp.Tests.Evolution.Mutation;

public class WeightPerturbationMutationTests
{
    private static readonly IReadOnlyList<NodeGene> MinimalNodes =
    [
        new(0, NodeType.Input),
        new(1, NodeType.Output)
    ];

    private static NeatSharpOptions DefaultOptions() => new()
    {
        Mutation =
        {
            PerturbationPower = 0.5,
            PerturbationDistribution = WeightDistributionType.Uniform,
            WeightMinValue = -4.0,
            WeightMaxValue = 4.0
        }
    };

    private static WeightPerturbationMutation CreateSut(NeatSharpOptions? options = null) =>
        new(Options.Create(options ?? DefaultOptions()));

    [Fact]
    public void Mutate_UniformDistribution_PerturbsEachConnectionWeight()
    {
        var connections = new ConnectionGene[]
        {
            new(0, 0, 1, 1.0, true),
            new(1, 0, 1, -1.0, true)
        };
        var genome = new Genome(MinimalNodes, connections);
        var sut = CreateSut();
        var random = new Random(42);
        var tracker = new InnovationTracker(100, 100);

        var result = sut.Mutate(genome, random, tracker);

        // Each weight should be changed (extremely unlikely to get exact 0.0 delta with fixed seed)
        for (int i = 0; i < result.Connections.Count; i++)
        {
            result.Connections[i].Weight.Should().NotBe(connections[i].Weight);
        }
    }

    [Fact]
    public void Mutate_UniformDistribution_DeltaWithinPowerRange()
    {
        var options = DefaultOptions();
        options.Mutation.PerturbationPower = 0.5;
        var connection = new ConnectionGene(0, 0, 1, 0.0, true);
        var genome = new Genome(MinimalNodes, [connection]);
        var sut = CreateSut(options);

        // Run many times and verify all deltas are within [-0.5, +0.5]
        for (int seed = 0; seed < 100; seed++)
        {
            var random = new Random(seed);
            var tracker = new InnovationTracker(100, 100);
            var result = sut.Mutate(genome, random, tracker);
            double delta = result.Connections[0].Weight - 0.0;
            delta.Should().BeInRange(-0.5, 0.5,
                $"uniform delta should be in [-power, +power] (seed={seed})");
        }
    }

    [Fact]
    public void Mutate_GaussianDistribution_PerturbsWeights()
    {
        var options = DefaultOptions();
        options.Mutation.PerturbationDistribution = WeightDistributionType.Gaussian;
        options.Mutation.PerturbationPower = 0.5;
        var connection = new ConnectionGene(0, 0, 1, 0.0, true);
        var genome = new Genome(MinimalNodes, [connection]);
        var sut = CreateSut(options);
        var random = new Random(42);
        var tracker = new InnovationTracker(100, 100);

        var result = sut.Mutate(genome, random, tracker);

        // Gaussian can produce values outside [-power, power], just verify it changed
        result.Connections[0].Weight.Should().NotBe(0.0);
    }

    [Fact]
    public void Mutate_WeightExceedsMax_ClampedToMax()
    {
        var options = DefaultOptions();
        options.Mutation.PerturbationPower = 2.0;
        options.Mutation.WeightMaxValue = 4.0;
        // Start at 3.5 — with power 2.0, uniform delta up to +2.0 could push above 4.0
        var connection = new ConnectionGene(0, 0, 1, 3.5, true);
        var genome = new Genome(MinimalNodes, [connection]);
        var sut = CreateSut(options);

        // Run many seeds to find one that would exceed max
        bool foundClampedCase = false;
        for (int seed = 0; seed < 1000; seed++)
        {
            var random = new Random(seed);
            var tracker = new InnovationTracker(100, 100);
            var result = sut.Mutate(genome, random, tracker);
            result.Connections[0].Weight.Should().BeLessThanOrEqualTo(4.0,
                $"weight should be clamped to max (seed={seed})");
            if (result.Connections[0].Weight == 4.0)
            {
                foundClampedCase = true;
            }
        }

        foundClampedCase.Should().BeTrue("at least one seed should produce a clamped-to-max result");
    }

    [Fact]
    public void Mutate_WeightBelowMin_ClampedToMin()
    {
        var options = DefaultOptions();
        options.Mutation.PerturbationPower = 2.0;
        options.Mutation.WeightMinValue = -4.0;
        // Start at -3.5 — with power 2.0, uniform delta down to -2.0 could push below -4.0
        var connection = new ConnectionGene(0, 0, 1, -3.5, true);
        var genome = new Genome(MinimalNodes, [connection]);
        var sut = CreateSut(options);

        bool foundClampedCase = false;
        for (int seed = 0; seed < 1000; seed++)
        {
            var random = new Random(seed);
            var tracker = new InnovationTracker(100, 100);
            var result = sut.Mutate(genome, random, tracker);
            result.Connections[0].Weight.Should().BeGreaterThanOrEqualTo(-4.0,
                $"weight should be clamped to min (seed={seed})");
            if (result.Connections[0].Weight == -4.0)
            {
                foundClampedCase = true;
            }
        }

        foundClampedCase.Should().BeTrue("at least one seed should produce a clamped-to-min result");
    }

    [Fact]
    public void Mutate_GenomeStructureUnchanged()
    {
        var connections = new ConnectionGene[]
        {
            new(0, 0, 1, 1.0, true),
            new(1, 0, 1, -1.0, true)
        };
        var genome = new Genome(MinimalNodes, connections);
        var sut = CreateSut();
        var random = new Random(42);
        var tracker = new InnovationTracker(100, 100);

        var result = sut.Mutate(genome, random, tracker);

        result.Nodes.Should().HaveCount(genome.Nodes.Count);
        result.Connections.Should().HaveCount(genome.Connections.Count);
        for (int i = 0; i < result.Connections.Count; i++)
        {
            result.Connections[i].InnovationNumber.Should().Be(connections[i].InnovationNumber);
            result.Connections[i].SourceNodeId.Should().Be(connections[i].SourceNodeId);
            result.Connections[i].TargetNodeId.Should().Be(connections[i].TargetNodeId);
            result.Connections[i].IsEnabled.Should().Be(connections[i].IsEnabled);
        }
    }

    [Fact]
    public void Mutate_OriginalGenomeNotModified()
    {
        var connections = new ConnectionGene[]
        {
            new(0, 0, 1, 1.0, true),
            new(1, 0, 1, -1.0, true)
        };
        var genome = new Genome(MinimalNodes, connections);
        var sut = CreateSut();
        var random = new Random(42);
        var tracker = new InnovationTracker(100, 100);

        _ = sut.Mutate(genome, random, tracker);

        genome.Connections[0].Weight.Should().Be(1.0);
        genome.Connections[1].Weight.Should().Be(-1.0);
    }

    [Fact]
    public void Mutate_Deterministic_SameResultWithSameSeed()
    {
        var connections = new ConnectionGene[]
        {
            new(0, 0, 1, 1.0, true),
            new(1, 0, 1, -1.0, true)
        };
        var genome = new Genome(MinimalNodes, connections);
        var sut = CreateSut();

        var result1 = sut.Mutate(genome, new Random(42), new InnovationTracker(100, 100));
        var result2 = sut.Mutate(genome, new Random(42), new InnovationTracker(100, 100));

        for (int i = 0; i < result1.Connections.Count; i++)
        {
            result1.Connections[i].Weight.Should().Be(result2.Connections[i].Weight);
        }
    }

    [Fact]
    public void Mutate_ZeroConnections_ReturnsGenomeUnchanged()
    {
        var genome = new Genome(MinimalNodes, Array.Empty<ConnectionGene>());
        var sut = CreateSut();
        var random = new Random(42);
        var tracker = new InnovationTracker(100, 100);

        var result = sut.Mutate(genome, random, tracker);

        result.Connections.Should().BeEmpty();
    }
}
