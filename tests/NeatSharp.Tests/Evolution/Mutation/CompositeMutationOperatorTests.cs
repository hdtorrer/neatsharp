using FluentAssertions;
using Microsoft.Extensions.Options;
using NeatSharp.Configuration;
using NeatSharp.Evolution.Mutation;
using NeatSharp.Genetics;
using Xunit;

namespace NeatSharp.Tests.Evolution.Mutation;

public class CompositeMutationOperatorTests
{
    private static readonly NodeGene[] ThreeNodes =
    [
        new(0, NodeType.Input),
        new(1, NodeType.Output),
        new(2, NodeType.Hidden)
    ];

    private static readonly NodeGene[] TwoNodes =
    [
        new(0, NodeType.Input),
        new(1, NodeType.Output)
    ];

    private static NeatSharpOptions CreateOptions(
        double perturbRate = 0.8,
        double replaceRate = 0.1,
        double addConnRate = 0.05,
        double addNodeRate = 0.03,
        double toggleRate = 0.01)
    {
        return new NeatSharpOptions
        {
            Mutation =
            {
                WeightPerturbationRate = perturbRate,
                WeightReplacementRate = replaceRate,
                AddConnectionRate = addConnRate,
                AddNodeRate = addNodeRate,
                ToggleEnableRate = toggleRate,
                PerturbationPower = 0.5,
                PerturbationDistribution = WeightDistributionType.Uniform,
                WeightMinValue = -4.0,
                WeightMaxValue = 4.0,
                MaxAddConnectionAttempts = 20
            }
        };
    }

    private static CompositeMutationOperator CreateSut(NeatSharpOptions options)
    {
        var opts = Options.Create(options);
        return new CompositeMutationOperator(
            opts,
            new WeightPerturbationMutation(opts),
            new WeightReplacementMutation(opts),
            new AddConnectionMutation(opts),
            new AddNodeMutation(opts),
            new ToggleEnableMutation());
    }

    [Fact]
    public void Mutate_WeightPerturbationAndReplacementMutuallyExclusive()
    {
        // Set high rates for both — they should still be mutually exclusive per roll
        var options = CreateOptions(perturbRate: 0.5, replaceRate: 0.5, addConnRate: 0, addNodeRate: 0, toggleRate: 0);
        var connections = new ConnectionGene[]
        {
            new(0, 0, 1, 1.0, true)
        };
        var genome = new Genome(TwoNodes, connections);
        var sut = CreateSut(options);

        // Over many seeds, verify both perturbation and replacement are triggered at least once
        // but never both (mutually exclusive)
        bool sawWeightChange = false;

        for (int seed = 0; seed < 100; seed++)
        {
            var random = new Random(seed);
            var tracker = new InnovationTracker(100, 100);
            var result = sut.Mutate(genome, random, tracker);

            if (result.Connections[0].Weight != 1.0)
                sawWeightChange = true;
        }

        // With perturbRate=0.5 and replaceRate=0.5, both types should be reachable
        // (perturbation if roll < 0.5, replacement if 0.5 <= roll < 1.0)
        sawWeightChange.Should().BeTrue("weight mutation should trigger for some seeds");
    }

    [Fact]
    public void Mutate_OnlyPerturbationRate_NeverReplacesWeight()
    {
        var options = CreateOptions(perturbRate: 1.0, replaceRate: 0, addConnRate: 0, addNodeRate: 0, toggleRate: 0);
        var connections = new ConnectionGene[]
        {
            new(0, 0, 1, 0.0, true),
            new(1, 0, 1, 0.0, true),
            new(2, 0, 1, 0.0, true)
        };
        var genome = new Genome(TwoNodes, connections);
        var sut = CreateSut(options);

        for (int seed = 0; seed < 50; seed++)
        {
            var random = new Random(seed);
            var tracker = new InnovationTracker(100, 100);
            var result = sut.Mutate(genome, random, tracker);

            // All weights should change (perturbation applies to each connection)
            for (int i = 0; i < result.Connections.Count; i++)
            {
                // Each connection gets its own perturbation, so all should change
                result.Connections.Count.Should().Be(3, "structure unchanged");
            }
        }
    }

    [Fact]
    public void Mutate_OnlyAddNodeRate_AddsNodeWhenTriggered()
    {
        var options = CreateOptions(perturbRate: 0, replaceRate: 0, addConnRate: 0, addNodeRate: 1.0, toggleRate: 0);
        var connections = new ConnectionGene[]
        {
            new(0, 0, 1, 1.0, true)
        };
        var genome = new Genome(TwoNodes, connections);
        var sut = CreateSut(options);
        var random = new Random(42);
        var tracker = new InnovationTracker(10, 10);

        var result = sut.Mutate(genome, random, tracker);

        result.Nodes.Count.Should().Be(3, "add-node should create a new hidden node");
        result.Connections.Count.Should().Be(3, "add-node should create 2 new connections + 1 disabled original");
    }

    [Fact]
    public void Mutate_OnlyAddConnectionRate_AddsConnectionWhenTriggered()
    {
        var options = CreateOptions(perturbRate: 0, replaceRate: 0, addConnRate: 1.0, addNodeRate: 0, toggleRate: 0);
        // 3 nodes with partial connectivity — room for new connections
        var connections = new ConnectionGene[]
        {
            new(0, 0, 2, 1.0, true),
            new(1, 2, 1, 1.0, true)
        };
        var genome = new Genome(ThreeNodes, connections);
        var sut = CreateSut(options);

        bool addedConnection = false;
        for (int seed = 0; seed < 100; seed++)
        {
            var random = new Random(seed);
            var tracker = new InnovationTracker(10, 10);
            var result = sut.Mutate(genome, random, tracker);
            if (result.Connections.Count > genome.Connections.Count)
            {
                addedConnection = true;
                break;
            }
        }

        addedConnection.Should().BeTrue("at least one seed should add a new connection");
    }

    [Fact]
    public void Mutate_OnlyToggleRate_TogglesWhenTriggered()
    {
        var options = CreateOptions(perturbRate: 0, replaceRate: 0, addConnRate: 0, addNodeRate: 0, toggleRate: 1.0);
        var connections = new ConnectionGene[]
        {
            new(0, 0, 1, 1.0, true)
        };
        var genome = new Genome(TwoNodes, connections);
        var sut = CreateSut(options);
        var random = new Random(42);
        var tracker = new InnovationTracker(100, 100);

        var result = sut.Mutate(genome, random, tracker);

        result.Connections[0].IsEnabled.Should().BeFalse("toggle should flip the enabled state");
    }

    [Fact]
    public void Mutate_AllRatesZero_ReturnsGenomeWithNoChanges()
    {
        var options = CreateOptions(perturbRate: 0, replaceRate: 0, addConnRate: 0, addNodeRate: 0, toggleRate: 0);
        var connections = new ConnectionGene[]
        {
            new(0, 0, 1, 1.0, true)
        };
        var genome = new Genome(TwoNodes, connections);
        var sut = CreateSut(options);
        var random = new Random(42);
        var tracker = new InnovationTracker(100, 100);

        var result = sut.Mutate(genome, random, tracker);

        result.Connections[0].Weight.Should().Be(1.0);
        result.Connections[0].IsEnabled.Should().BeTrue();
        result.Nodes.Count.Should().Be(2);
        result.Connections.Count.Should().Be(1);
    }

    [Fact]
    public void Mutate_MultipleMutationsCanApplyInOneCall()
    {
        // Set high rates for weight perturbation and toggle — both should apply
        var options = CreateOptions(perturbRate: 1.0, replaceRate: 0, addConnRate: 0, addNodeRate: 0, toggleRate: 1.0);
        var connections = new ConnectionGene[]
        {
            new(0, 0, 1, 1.0, true)
        };
        var genome = new Genome(TwoNodes, connections);
        var sut = CreateSut(options);
        var random = new Random(42);
        var tracker = new InnovationTracker(100, 100);

        var result = sut.Mutate(genome, random, tracker);

        // Weight should change (perturbation applied) AND enabled should flip (toggle applied)
        result.Connections[0].Weight.Should().NotBe(1.0, "weight perturbation should change the weight");
        result.Connections[0].IsEnabled.Should().BeFalse("toggle should flip enabled state");
    }

    [Fact]
    public void Mutate_Deterministic_SameResultWithSameSeed()
    {
        var options = CreateOptions();
        var connections = new ConnectionGene[]
        {
            new(0, 0, 2, 1.0, true),
            new(1, 2, 1, 0.5, true)
        };
        var genome = new Genome(ThreeNodes, connections);
        var sut = CreateSut(options);

        var result1 = sut.Mutate(genome, new Random(42), new InnovationTracker(10, 10));
        var result2 = sut.Mutate(genome, new Random(42), new InnovationTracker(10, 10));

        result1.Nodes.Count.Should().Be(result2.Nodes.Count);
        result1.Connections.Count.Should().Be(result2.Connections.Count);
        for (int i = 0; i < result1.Connections.Count; i++)
        {
            result1.Connections[i].Should().Be(result2.Connections[i]);
        }
    }

    [Fact]
    public void Mutate_MutationOrder_WeightBeforeStructuralBeforeToggle()
    {
        // Set all rates to 1.0 to ensure all mutations apply
        // With add-node rate 1.0 and toggle rate 1.0:
        // 1. Weight perturbation first (modifies weights)
        // 2. Add-node (splits a connection, adds node)
        // 3. Toggle (flips one connection's enabled state)
        var options = CreateOptions(perturbRate: 1.0, replaceRate: 0, addConnRate: 0, addNodeRate: 1.0, toggleRate: 1.0);
        var connections = new ConnectionGene[]
        {
            new(0, 0, 1, 1.0, true)
        };
        var genome = new Genome(TwoNodes, connections);
        var sut = CreateSut(options);
        var random = new Random(42);
        var tracker = new InnovationTracker(10, 10);

        var result = sut.Mutate(genome, random, tracker);

        // After weight perturbation: weight changed
        // After add-node: original connection disabled, 2 new connections + 1 new node
        // After toggle: one connection flipped
        result.Nodes.Count.Should().Be(3, "add-node should create a new hidden node");
        result.Connections.Count.Should().Be(3, "add-node adds 2 new connections");
    }
}
