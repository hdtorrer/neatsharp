using FluentAssertions;
using Microsoft.Extensions.Options;
using NeatSharp.Configuration;
using NeatSharp.Evolution.Crossover;
using NeatSharp.Genetics;
using Xunit;

namespace NeatSharp.Tests.Evolution.Crossover;

public class NeatCrossoverTests
{
    private static readonly IReadOnlyList<NodeGene> MinimalNodes =
    [
        new(0, NodeType.Input),
        new(1, NodeType.Output)
    ];

    private static NeatSharpOptions DefaultOptions() => new()
    {
        Crossover =
        {
            DisabledGeneInheritanceProbability = 0.75
        }
    };

    private static NeatCrossover CreateSut(NeatSharpOptions? options = null) =>
        new(Options.Create(options ?? DefaultOptions()));

    // --- Helper: create a genome with specific nodes and connections ---

    private static Genome MakeGenome(IReadOnlyList<NodeGene> nodes, params ConnectionGene[] connections) =>
        new(nodes, connections);

    #region Matching Genes — 50/50 Random Inheritance

    [Fact]
    public void Cross_MatchingGenes_InheritedFromEitherParent()
    {
        // Two parents with the same connection (innovation 0) but different weights
        var parent1 = MakeGenome(MinimalNodes,
            new ConnectionGene(0, 0, 1, 1.0, true));
        var parent2 = MakeGenome(MinimalNodes,
            new ConnectionGene(0, 0, 1, 2.0, true));

        var sut = CreateSut();
        var random = new Random(42);

        var offspring = sut.Cross(parent1, 10.0, parent2, 5.0, random);

        // Offspring should have exactly one connection with innovation 0
        offspring.Connections.Should().HaveCount(1);
        offspring.Connections[0].InnovationNumber.Should().Be(0);
        // Weight should be from one of the two parents
        offspring.Connections[0].Weight.Should().BeOneOf(1.0, 2.0);
    }

    [Fact]
    public void Cross_MatchingGenes_ApproximatelyFiftyFiftyOverManyTrials()
    {
        var parent1 = MakeGenome(MinimalNodes,
            new ConnectionGene(0, 0, 1, 1.0, true));
        var parent2 = MakeGenome(MinimalNodes,
            new ConnectionGene(0, 0, 1, 2.0, true));

        var sut = CreateSut();
        int fromParent1 = 0;
        int totalTrials = 1000;

        for (int seed = 0; seed < totalTrials; seed++)
        {
            var random = new Random(seed);
            var offspring = sut.Cross(parent1, 10.0, parent2, 10.0, random);
            if (offspring.Connections[0].Weight == 1.0)
                fromParent1++;
        }

        // With equal fitness, expect roughly 50/50 — allow wide tolerance
        double ratio = (double)fromParent1 / totalTrials;
        ratio.Should().BeInRange(0.35, 0.65,
            "matching genes should be inherited from either parent roughly 50/50");
    }

    #endregion

    #region Disjoint/Excess Genes — Fitter Parent

    [Fact]
    public void Cross_DisjointGenes_InheritedFromFitterParentOnly()
    {
        // Parent1 (fitter) has innovations 0, 1, 3
        // Parent2 (less fit) has innovations 0, 2
        // Disjoint: innovation 1 (p1 only), 2 (p2 only), 3 is excess from p1
        var nodes = new NodeGene[]
        {
            new(0, NodeType.Input),
            new(1, NodeType.Output),
            new(2, NodeType.Hidden)
        };
        var parent1 = MakeGenome(nodes,
            new ConnectionGene(0, 0, 1, 1.0, true),
            new ConnectionGene(1, 0, 2, 1.5, true),
            new ConnectionGene(3, 2, 1, 2.0, true));
        var parent2 = MakeGenome(nodes,
            new ConnectionGene(0, 0, 1, 3.0, true),
            new ConnectionGene(2, 0, 2, 3.5, true));

        var sut = CreateSut();
        var random = new Random(42);

        // Parent1 is fitter
        var offspring = sut.Cross(parent1, 10.0, parent2, 5.0, random);

        // Should have innovation 0 (matching), 1 (disjoint from fitter), 3 (excess from fitter)
        // Should NOT have innovation 2 (disjoint from less fit parent)
        var innovationNumbers = offspring.Connections.Select(c => c.InnovationNumber).ToList();
        innovationNumbers.Should().Contain(0, "matching gene should be inherited");
        innovationNumbers.Should().Contain(1, "disjoint gene from fitter parent should be inherited");
        innovationNumbers.Should().Contain(3, "excess gene from fitter parent should be inherited");
        innovationNumbers.Should().NotContain(2, "disjoint gene from less fit parent should NOT be inherited");
    }

    [Fact]
    public void Cross_ExcessGenes_InheritedFromFitterParentOnly()
    {
        // Parent1 (fitter) has innovations 0, 1, 2, 3
        // Parent2 (less fit) has innovations 0, 1
        // Excess: innovations 2, 3 from parent1
        var nodes = new NodeGene[]
        {
            new(0, NodeType.Input),
            new(1, NodeType.Output),
            new(2, NodeType.Hidden)
        };
        var parent1 = MakeGenome(nodes,
            new ConnectionGene(0, 0, 1, 1.0, true),
            new ConnectionGene(1, 0, 2, 1.5, true),
            new ConnectionGene(2, 2, 1, 2.0, true),
            new ConnectionGene(3, 0, 1, 2.5, true));
        var parent2 = MakeGenome(
            [new NodeGene(0, NodeType.Input), new NodeGene(1, NodeType.Output), new NodeGene(2, NodeType.Hidden)],
            new ConnectionGene(0, 0, 1, 3.0, true),
            new ConnectionGene(1, 0, 2, 3.5, true));

        var sut = CreateSut();
        var random = new Random(42);

        var offspring = sut.Cross(parent1, 10.0, parent2, 5.0, random);

        var innovationNumbers = offspring.Connections.Select(c => c.InnovationNumber).ToList();
        innovationNumbers.Should().Contain(2, "excess gene from fitter parent");
        innovationNumbers.Should().Contain(3, "excess gene from fitter parent");
    }

    [Fact]
    public void Cross_LessFitParentHasExcess_ExcessNotInherited()
    {
        // Parent1 (fitter) has innovations 0, 1
        // Parent2 (less fit) has innovations 0, 1, 2, 3
        var nodes = new NodeGene[]
        {
            new(0, NodeType.Input),
            new(1, NodeType.Output),
            new(2, NodeType.Hidden)
        };
        var parent1 = MakeGenome(
            [new NodeGene(0, NodeType.Input), new NodeGene(1, NodeType.Output)],
            new ConnectionGene(0, 0, 1, 1.0, true),
            new ConnectionGene(1, 0, 1, 1.5, true));
        var parent2 = MakeGenome(nodes,
            new ConnectionGene(0, 0, 1, 3.0, true),
            new ConnectionGene(1, 0, 2, 3.5, true),
            new ConnectionGene(2, 2, 1, 4.0, true),
            new ConnectionGene(3, 0, 2, 4.5, true));

        var sut = CreateSut();
        var random = new Random(42);

        var offspring = sut.Cross(parent1, 10.0, parent2, 5.0, random);

        var innovationNumbers = offspring.Connections.Select(c => c.InnovationNumber).ToList();
        innovationNumbers.Should().NotContain(2, "excess from less fit parent not inherited");
        innovationNumbers.Should().NotContain(3, "excess from less fit parent not inherited");
    }

    #endregion

    #region Equal Fitness — Disjoint/Excess from Both Parents

    [Fact]
    public void Cross_EqualFitness_DisjointAndExcessFromBothParents()
    {
        // Parent1 has innovations 0, 1, 3
        // Parent2 has innovations 0, 2, 4
        // Matching: 0. Disjoint: 1 (p1), 2 (p2). Excess: 3 (p1), 4 (p2)
        var nodes = new NodeGene[]
        {
            new(0, NodeType.Input),
            new(1, NodeType.Output),
            new(2, NodeType.Hidden),
            new(3, NodeType.Hidden)
        };
        var parent1 = MakeGenome(nodes,
            new ConnectionGene(0, 0, 1, 1.0, true),
            new ConnectionGene(1, 0, 2, 1.5, true),
            new ConnectionGene(3, 2, 1, 2.0, true));
        var parent2 = MakeGenome(nodes,
            new ConnectionGene(0, 0, 1, 3.0, true),
            new ConnectionGene(2, 0, 3, 3.5, true),
            new ConnectionGene(4, 3, 1, 4.0, true));

        var sut = CreateSut();
        var random = new Random(42);

        var offspring = sut.Cross(parent1, 10.0, parent2, 10.0, random);

        var innovationNumbers = offspring.Connections.Select(c => c.InnovationNumber).ToList();
        innovationNumbers.Should().Contain(0, "matching gene");
        innovationNumbers.Should().Contain(1, "disjoint from parent1 included at equal fitness");
        innovationNumbers.Should().Contain(2, "disjoint from parent2 included at equal fitness");
        innovationNumbers.Should().Contain(3, "excess from parent1 included at equal fitness");
        innovationNumbers.Should().Contain(4, "excess from parent2 included at equal fitness");
    }

    #endregion

    #region Disabled Gene Inheritance

    [Fact]
    public void Cross_MatchingGeneDisabledInOneParent_ProbabilisticDisabling()
    {
        // Matching gene: innovation 0 is disabled in parent1, enabled in parent2
        var parent1 = MakeGenome(MinimalNodes,
            new ConnectionGene(0, 0, 1, 1.0, false)); // disabled
        var parent2 = MakeGenome(MinimalNodes,
            new ConnectionGene(0, 0, 1, 2.0, true));  // enabled

        var options = DefaultOptions();
        options.Crossover.DisabledGeneInheritanceProbability = 0.75;
        var sut = CreateSut(options);

        int disabledCount = 0;
        int totalTrials = 1000;

        for (int seed = 0; seed < totalTrials; seed++)
        {
            var random = new Random(seed);
            var offspring = sut.Cross(parent1, 10.0, parent2, 10.0, random);
            if (!offspring.Connections[0].IsEnabled)
                disabledCount++;
        }

        double disabledRatio = (double)disabledCount / totalTrials;
        disabledRatio.Should().BeInRange(0.60, 0.90,
            "~75% of offspring should have gene disabled when disabled in either parent");
    }

    [Fact]
    public void Cross_MatchingGeneBothEnabled_RemainsEnabled()
    {
        // Both parents have the matching gene enabled — should always be enabled
        var parent1 = MakeGenome(MinimalNodes,
            new ConnectionGene(0, 0, 1, 1.0, true));
        var parent2 = MakeGenome(MinimalNodes,
            new ConnectionGene(0, 0, 1, 2.0, true));

        var sut = CreateSut();

        for (int seed = 0; seed < 100; seed++)
        {
            var random = new Random(seed);
            var offspring = sut.Cross(parent1, 10.0, parent2, 10.0, random);
            offspring.Connections[0].IsEnabled.Should().BeTrue(
                $"gene should remain enabled when both parents have it enabled (seed={seed})");
        }
    }

    [Fact]
    public void Cross_MatchingGeneBothDisabled_AlwaysDisabled()
    {
        // Both parents have the matching gene disabled — should always be disabled
        var parent1 = MakeGenome(MinimalNodes,
            new ConnectionGene(0, 0, 1, 1.0, false));
        var parent2 = MakeGenome(MinimalNodes,
            new ConnectionGene(0, 0, 1, 2.0, false));

        var sut = CreateSut();

        for (int seed = 0; seed < 100; seed++)
        {
            var random = new Random(seed);
            var offspring = sut.Cross(parent1, 10.0, parent2, 10.0, random);
            offspring.Connections[0].IsEnabled.Should().BeFalse(
                $"gene should always be disabled when both parents have it disabled (seed={seed})");
        }
    }

    #endregion

    #region Node Inheritance

    [Fact]
    public void Cross_OffspringContainsAllNodesReferencedByInheritedConnections()
    {
        var nodes1 = new NodeGene[]
        {
            new(0, NodeType.Input),
            new(1, NodeType.Output),
            new(2, NodeType.Hidden)
        };
        var nodes2 = new NodeGene[]
        {
            new(0, NodeType.Input),
            new(1, NodeType.Output),
            new(3, NodeType.Hidden)
        };
        // Parent1 (fitter) uses hidden node 2; Parent2 uses hidden node 3
        var parent1 = MakeGenome(nodes1,
            new ConnectionGene(0, 0, 1, 1.0, true),
            new ConnectionGene(1, 0, 2, 1.5, true),
            new ConnectionGene(2, 2, 1, 2.0, true));
        var parent2 = MakeGenome(nodes2,
            new ConnectionGene(0, 0, 1, 3.0, true));

        var sut = CreateSut();
        var random = new Random(42);

        // Parent1 is fitter, so disjoint/excess (innovations 1, 2) come from parent1
        var offspring = sut.Cross(parent1, 10.0, parent2, 5.0, random);

        var nodeIds = offspring.Nodes.Select(n => n.Id).ToHashSet();
        // Must have nodes 0, 1 (input/output) and 2 (hidden, referenced by connections from parent1)
        nodeIds.Should().Contain(0);
        nodeIds.Should().Contain(1);
        nodeIds.Should().Contain(2, "node 2 is referenced by inherited connections from fitter parent");
    }

    [Fact]
    public void Cross_OffspringIncludesAllInputOutputBiasFromFitterParent()
    {
        var nodes1 = new NodeGene[]
        {
            new(0, NodeType.Input),
            new(1, NodeType.Input),
            new(2, NodeType.Bias),
            new(3, NodeType.Output),
            new(4, NodeType.Output)
        };
        var nodes2 = new NodeGene[]
        {
            new(0, NodeType.Input),
            new(1, NodeType.Input),
            new(2, NodeType.Bias),
            new(3, NodeType.Output),
            new(4, NodeType.Output)
        };
        var parent1 = MakeGenome(nodes1,
            new ConnectionGene(0, 0, 3, 1.0, true));
        var parent2 = MakeGenome(nodes2,
            new ConnectionGene(0, 0, 3, 2.0, true));

        var sut = CreateSut();
        var random = new Random(42);

        // Parent1 fitter
        var offspring = sut.Cross(parent1, 10.0, parent2, 5.0, random);

        var nodeIds = offspring.Nodes.Select(n => n.Id).ToHashSet();
        nodeIds.Should().Contain(0, "input node from fitter parent");
        nodeIds.Should().Contain(1, "input node from fitter parent");
        nodeIds.Should().Contain(2, "bias node from fitter parent");
        nodeIds.Should().Contain(3, "output node from fitter parent");
        nodeIds.Should().Contain(4, "output node from fitter parent");
    }

    [Fact]
    public void Cross_EqualFitness_InputOutputBiasFromParent1()
    {
        // Per contract: "prefer parent1 if equal fitness" for input/output/bias
        var nodes = new NodeGene[]
        {
            new(0, NodeType.Input),
            new(1, NodeType.Output)
        };
        var parent1 = MakeGenome(nodes,
            new ConnectionGene(0, 0, 1, 1.0, true));
        var parent2 = MakeGenome(nodes,
            new ConnectionGene(0, 0, 1, 2.0, true));

        var sut = CreateSut();
        var random = new Random(42);

        var offspring = sut.Cross(parent1, 10.0, parent2, 10.0, random);

        offspring.Nodes.Should().Contain(n => n.Id == 0 && n.Type == NodeType.Input);
        offspring.Nodes.Should().Contain(n => n.Id == 1 && n.Type == NodeType.Output);
    }

    #endregion

    #region Offspring Immutability

    [Fact]
    public void Cross_OffspringIsImmutableGenome()
    {
        var parent1 = MakeGenome(MinimalNodes,
            new ConnectionGene(0, 0, 1, 1.0, true));
        var parent2 = MakeGenome(MinimalNodes,
            new ConnectionGene(0, 0, 1, 2.0, true));

        var sut = CreateSut();
        var random = new Random(42);

        var offspring = sut.Cross(parent1, 10.0, parent2, 5.0, random);

        // Genome is a sealed class with IReadOnlyList properties — verify it was constructed properly
        offspring.Should().NotBeNull();
        offspring.Should().BeOfType<Genome>();
        offspring.Nodes.Should().NotBeEmpty();
        offspring.Connections.Should().NotBeEmpty();
    }

    #endregion

    #region Determinism

    [Fact]
    public void Cross_Deterministic_SameResultWithSameSeed()
    {
        var nodes = new NodeGene[]
        {
            new(0, NodeType.Input),
            new(1, NodeType.Output),
            new(2, NodeType.Hidden)
        };
        var parent1 = MakeGenome(nodes,
            new ConnectionGene(0, 0, 1, 1.0, true),
            new ConnectionGene(1, 0, 2, 1.5, true),
            new ConnectionGene(3, 2, 1, 2.0, true));
        var parent2 = MakeGenome(nodes,
            new ConnectionGene(0, 0, 1, 3.0, true),
            new ConnectionGene(2, 0, 2, 3.5, true));

        var sut = CreateSut();

        var offspring1 = sut.Cross(parent1, 10.0, parent2, 5.0, new Random(42));
        var offspring2 = sut.Cross(parent1, 10.0, parent2, 5.0, new Random(42));

        offspring1.Connections.Should().HaveCount(offspring2.Connections.Count);
        for (int i = 0; i < offspring1.Connections.Count; i++)
        {
            offspring1.Connections[i].InnovationNumber.Should().Be(offspring2.Connections[i].InnovationNumber);
            offspring1.Connections[i].Weight.Should().Be(offspring2.Connections[i].Weight);
            offspring1.Connections[i].IsEnabled.Should().Be(offspring2.Connections[i].IsEnabled);
            offspring1.Connections[i].SourceNodeId.Should().Be(offspring2.Connections[i].SourceNodeId);
            offspring1.Connections[i].TargetNodeId.Should().Be(offspring2.Connections[i].TargetNodeId);
        }

        offspring1.Nodes.Should().HaveCount(offspring2.Nodes.Count);
        for (int i = 0; i < offspring1.Nodes.Count; i++)
        {
            offspring1.Nodes[i].Id.Should().Be(offspring2.Nodes[i].Id);
            offspring1.Nodes[i].Type.Should().Be(offspring2.Nodes[i].Type);
            offspring1.Nodes[i].ActivationFunction.Should().Be(offspring2.Nodes[i].ActivationFunction);
        }
    }

    #endregion

    #region Self-Crossover

    [Fact]
    public void Cross_SelfCrossover_ProducesClone()
    {
        var nodes = new NodeGene[]
        {
            new(0, NodeType.Input),
            new(1, NodeType.Output),
            new(2, NodeType.Hidden)
        };
        var genome = MakeGenome(nodes,
            new ConnectionGene(0, 0, 1, 1.0, true),
            new ConnectionGene(1, 0, 2, 1.5, true),
            new ConnectionGene(2, 2, 1, 2.0, true));

        var sut = CreateSut();
        var random = new Random(42);

        var offspring = sut.Cross(genome, 10.0, genome, 10.0, random);

        // Self-crossover: all genes are matching, inherited from same genome = clone
        offspring.Connections.Should().HaveCount(genome.Connections.Count);
        for (int i = 0; i < offspring.Connections.Count; i++)
        {
            offspring.Connections[i].InnovationNumber.Should().Be(genome.Connections[i].InnovationNumber);
            offspring.Connections[i].SourceNodeId.Should().Be(genome.Connections[i].SourceNodeId);
            offspring.Connections[i].TargetNodeId.Should().Be(genome.Connections[i].TargetNodeId);
            offspring.Connections[i].Weight.Should().Be(genome.Connections[i].Weight);
            // IsEnabled stays same since both parents have it enabled
            offspring.Connections[i].IsEnabled.Should().Be(genome.Connections[i].IsEnabled);
        }

        offspring.Nodes.Should().HaveCount(genome.Nodes.Count);
    }

    #endregion

    #region Original Parents Not Modified

    [Fact]
    public void Cross_OriginalParentsNotModified()
    {
        var parent1 = MakeGenome(MinimalNodes,
            new ConnectionGene(0, 0, 1, 1.0, true));
        var parent2 = MakeGenome(MinimalNodes,
            new ConnectionGene(0, 0, 1, 2.0, true),
            new ConnectionGene(1, 0, 1, 3.0, true));

        var sut = CreateSut();
        var random = new Random(42);

        _ = sut.Cross(parent1, 10.0, parent2, 5.0, random);

        // Parents should be unchanged
        parent1.Connections.Should().HaveCount(1);
        parent1.Connections[0].Weight.Should().Be(1.0);
        parent2.Connections.Should().HaveCount(2);
        parent2.Connections[0].Weight.Should().Be(2.0);
        parent2.Connections[1].Weight.Should().Be(3.0);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void Cross_NoConnections_ReturnsGenomeWithNoConnections()
    {
        var parent1 = MakeGenome(MinimalNodes);
        var parent2 = MakeGenome(MinimalNodes);

        var sut = CreateSut();
        var random = new Random(42);

        var offspring = sut.Cross(parent1, 10.0, parent2, 5.0, random);

        offspring.Connections.Should().BeEmpty();
        offspring.Nodes.Should().NotBeEmpty();
    }

    [Fact]
    public void Cross_OneParentNoConnections_InheritsFromFitterParent()
    {
        var parent1 = MakeGenome(MinimalNodes,
            new ConnectionGene(0, 0, 1, 1.0, true));
        var parent2 = MakeGenome(MinimalNodes);

        var sut = CreateSut();
        var random = new Random(42);

        // Parent1 is fitter and has connections
        var offspring = sut.Cross(parent1, 10.0, parent2, 5.0, random);

        offspring.Connections.Should().HaveCount(1);
        offspring.Connections[0].InnovationNumber.Should().Be(0);
    }

    [Fact]
    public void Cross_OneParentNoConnections_LessFitParentHasAll_NoConnectionsInherited()
    {
        var parent1 = MakeGenome(MinimalNodes);
        var parent2 = MakeGenome(MinimalNodes,
            new ConnectionGene(0, 0, 1, 2.0, true));

        var sut = CreateSut();
        var random = new Random(42);

        // Parent1 is fitter but has no connections; parent2 has excess genes
        var offspring = sut.Cross(parent1, 10.0, parent2, 5.0, random);

        // Excess from less fit parent should not be inherited
        offspring.Connections.Should().BeEmpty();
    }

    [Fact]
    public void Cross_NoOverlappingInnovations_FitterParentGenesOnly()
    {
        var nodes = new NodeGene[]
        {
            new(0, NodeType.Input),
            new(1, NodeType.Output),
            new(2, NodeType.Hidden)
        };
        var parent1 = MakeGenome(nodes,
            new ConnectionGene(0, 0, 1, 1.0, true),
            new ConnectionGene(1, 0, 2, 1.5, true));
        var parent2 = MakeGenome(nodes,
            new ConnectionGene(2, 2, 1, 3.0, true),
            new ConnectionGene(3, 0, 2, 3.5, true));

        var sut = CreateSut();
        var random = new Random(42);

        // Parent1 fitter — only its genes (innovations 0,1) inherited; parent2's are all disjoint/excess
        var offspring = sut.Cross(parent1, 10.0, parent2, 5.0, random);

        var innovationNumbers = offspring.Connections.Select(c => c.InnovationNumber).ToList();
        innovationNumbers.Should().Contain(0);
        innovationNumbers.Should().Contain(1);
        innovationNumbers.Should().NotContain(2);
        innovationNumbers.Should().NotContain(3);
    }

    [Fact]
    public void Cross_ConnectionsPreservedInInnovationOrder()
    {
        var nodes = new NodeGene[]
        {
            new(0, NodeType.Input),
            new(1, NodeType.Output),
            new(2, NodeType.Hidden)
        };
        var parent1 = MakeGenome(nodes,
            new ConnectionGene(0, 0, 1, 1.0, true),
            new ConnectionGene(1, 0, 2, 1.5, true),
            new ConnectionGene(3, 2, 1, 2.0, true));
        var parent2 = MakeGenome(nodes,
            new ConnectionGene(0, 0, 1, 3.0, true),
            new ConnectionGene(2, 0, 2, 3.5, true));

        var sut = CreateSut();
        var random = new Random(42);

        // Equal fitness — all genes from both
        var offspring = sut.Cross(parent1, 10.0, parent2, 10.0, random);

        // Connections should be in innovation number order
        for (int i = 1; i < offspring.Connections.Count; i++)
        {
            offspring.Connections[i].InnovationNumber.Should().BeGreaterThan(
                offspring.Connections[i - 1].InnovationNumber,
                "connections should be ordered by innovation number");
        }
    }

    #endregion

    #region Offspring Validity

    [Fact]
    public void Cross_OffspringIsValidGenome_AllConnectionsReferenceExistingNodes()
    {
        var nodes1 = new NodeGene[]
        {
            new(0, NodeType.Input),
            new(1, NodeType.Output),
            new(2, NodeType.Hidden)
        };
        var nodes2 = new NodeGene[]
        {
            new(0, NodeType.Input),
            new(1, NodeType.Output),
            new(3, NodeType.Hidden)
        };
        var parent1 = MakeGenome(nodes1,
            new ConnectionGene(0, 0, 1, 1.0, true),
            new ConnectionGene(1, 0, 2, 1.5, true),
            new ConnectionGene(2, 2, 1, 2.0, true));
        var parent2 = MakeGenome(nodes2,
            new ConnectionGene(0, 0, 1, 3.0, true),
            new ConnectionGene(3, 0, 3, 3.5, true),
            new ConnectionGene(4, 3, 1, 4.0, true));

        var sut = CreateSut();
        var random = new Random(42);

        // Equal fitness — genes from both parents
        var offspring = sut.Cross(parent1, 10.0, parent2, 10.0, random);

        // Verify all connections reference existing nodes
        var nodeIds = offspring.Nodes.Select(n => n.Id).ToHashSet();
        foreach (var conn in offspring.Connections)
        {
            nodeIds.Should().Contain(conn.SourceNodeId,
                $"connection {conn.InnovationNumber} source node {conn.SourceNodeId} must exist in offspring");
            nodeIds.Should().Contain(conn.TargetNodeId,
                $"connection {conn.InnovationNumber} target node {conn.TargetNodeId} must exist in offspring");
        }
    }

    #endregion

    #region Fitness Determines Fitter Parent Direction

    [Fact]
    public void Cross_Parent2Fitter_DisjointExcessFromParent2()
    {
        // When parent2 is fitter, disjoint/excess should come from parent2
        var nodes = new NodeGene[]
        {
            new(0, NodeType.Input),
            new(1, NodeType.Output),
            new(2, NodeType.Hidden)
        };
        var parent1 = MakeGenome(
            [new NodeGene(0, NodeType.Input), new NodeGene(1, NodeType.Output)],
            new ConnectionGene(0, 0, 1, 1.0, true),
            new ConnectionGene(1, 0, 1, 1.5, true));
        var parent2 = MakeGenome(nodes,
            new ConnectionGene(0, 0, 1, 3.0, true),
            new ConnectionGene(2, 0, 2, 3.5, true),
            new ConnectionGene(3, 2, 1, 4.0, true));

        var sut = CreateSut();
        var random = new Random(42);

        // Parent2 fitter
        var offspring = sut.Cross(parent1, 5.0, parent2, 10.0, random);

        var innovationNumbers = offspring.Connections.Select(c => c.InnovationNumber).ToList();
        innovationNumbers.Should().Contain(0, "matching gene");
        innovationNumbers.Should().NotContain(1, "disjoint from less fit parent1 NOT inherited");
        innovationNumbers.Should().Contain(2, "disjoint/excess from fitter parent2 inherited");
        innovationNumbers.Should().Contain(3, "excess from fitter parent2 inherited");
    }

    #endregion

    #region No New Innovation Numbers Created

    [Fact]
    public void Cross_NoNewInnovationNumbersCreated()
    {
        var nodes = new NodeGene[]
        {
            new(0, NodeType.Input),
            new(1, NodeType.Output),
            new(2, NodeType.Hidden)
        };
        var parent1 = MakeGenome(nodes,
            new ConnectionGene(0, 0, 1, 1.0, true),
            new ConnectionGene(1, 0, 2, 1.5, true));
        var parent2 = MakeGenome(nodes,
            new ConnectionGene(0, 0, 1, 3.0, true),
            new ConnectionGene(2, 2, 1, 3.5, true));

        var unionInnovations = parent1.Connections.Select(c => c.InnovationNumber)
            .Union(parent2.Connections.Select(c => c.InnovationNumber))
            .ToHashSet();

        var sut = CreateSut();
        var random = new Random(42);

        var offspring = sut.Cross(parent1, 10.0, parent2, 10.0, random);

        foreach (var conn in offspring.Connections)
        {
            unionInnovations.Should().Contain(conn.InnovationNumber,
                "offspring should only contain innovation numbers from parent union");
        }
    }

    #endregion
}
