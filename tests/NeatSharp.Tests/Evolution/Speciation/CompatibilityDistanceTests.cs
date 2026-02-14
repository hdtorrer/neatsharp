using FluentAssertions;
using Microsoft.Extensions.Options;
using NeatSharp.Configuration;
using NeatSharp.Evolution.Speciation;
using NeatSharp.Genetics;
using Xunit;

namespace NeatSharp.Tests.Evolution.Speciation;

public class CompatibilityDistanceTests
{
    private static readonly IReadOnlyList<NodeGene> MinimalNodes =
    [
        new(0, NodeType.Input),
        new(1, NodeType.Output)
    ];

    private static NeatSharpOptions DefaultOptions() => new()
    {
        Speciation =
        {
            ExcessCoefficient = 1.0,
            DisjointCoefficient = 1.0,
            WeightDifferenceCoefficient = 0.4
        }
    };

    private static CompatibilityDistance CreateSut(NeatSharpOptions? options = null) =>
        new(Options.Create(options ?? DefaultOptions()));

    private static Genome MakeGenome(IReadOnlyList<NodeGene> nodes, params ConnectionGene[] connections) =>
        new(nodes, connections);

    #region Identical Genomes Return 0.0

    [Fact]
    public void Compute_IdenticalGenomes_ReturnsZero()
    {
        var genome = MakeGenome(MinimalNodes,
            new ConnectionGene(0, 0, 1, 1.0, true),
            new ConnectionGene(1, 0, 1, 2.0, true));

        var sut = CreateSut();

        var distance = sut.Compute(genome, genome);

        distance.Should().Be(0.0, "identical genomes have no excess, disjoint, or weight difference");
    }

    [Fact]
    public void Compute_IdenticalStructureSameWeights_ReturnsZero()
    {
        var genome1 = MakeGenome(MinimalNodes,
            new ConnectionGene(0, 0, 1, 1.5, true),
            new ConnectionGene(1, 0, 1, -0.5, true));
        var genome2 = MakeGenome(MinimalNodes,
            new ConnectionGene(0, 0, 1, 1.5, true),
            new ConnectionGene(1, 0, 1, -0.5, true));

        var sut = CreateSut();

        var distance = sut.Compute(genome1, genome2);

        distance.Should().Be(0.0);
    }

    [Fact]
    public void Compute_BothGenomesNoConnections_ReturnsZero()
    {
        var genome1 = MakeGenome(MinimalNodes);
        var genome2 = MakeGenome(MinimalNodes);

        var sut = CreateSut();

        var distance = sut.Compute(genome1, genome2);

        distance.Should().Be(0.0);
    }

    #endregion

    #region Disjoint Genes Only

    [Fact]
    public void Compute_OnlyDisjointGenes_CountsCorrectly()
    {
        // Genome1 has innovations 0, 2; Genome2 has innovations 1, 3
        // Within the range [0,3], innovation 0 in g1 but not g2 = disjoint,
        // innovation 1 in g2 but not g1 = disjoint,
        // innovation 2 in g1 but not g2 = disjoint,
        // innovation 3 in g2 but not g1 = excess (beyond g1 max=2)
        // Actually let's design this more carefully:
        // Genome1: innovations [0, 2], max=2
        // Genome2: innovations [1], max=1
        // Range of both: 0..2
        // g1[0] vs g2[1]: g1 innov 0 < g2 innov 1 → disjoint (g1)
        // g1[2] vs g2[1]: g2 innov 1 < g1 innov 2 → disjoint (g2), advance g2
        // g2 exhausted, g1 has innov 2 remaining → excess
        // D=2, E=1, W=0, N=max(2,1)=2
        // d = (1*1/2) + (1*2/2) + (0.4*0) = 0.5 + 1.0 = 1.5

        var nodes = new NodeGene[]
        {
            new(0, NodeType.Input),
            new(1, NodeType.Output),
            new(2, NodeType.Hidden)
        };
        var genome1 = MakeGenome(nodes,
            new ConnectionGene(0, 0, 1, 1.0, true),
            new ConnectionGene(2, 0, 2, 1.0, true));
        var genome2 = MakeGenome(nodes,
            new ConnectionGene(1, 0, 2, 1.0, true));

        var sut = CreateSut();

        var distance = sut.Compute(genome1, genome2);

        // D=2, E=1, W=0, N=2
        // d = (1*1/2) + (1*2/2) + (0.4*0) = 0.5 + 1.0 = 1.5
        distance.Should().Be(1.5);
    }

    [Fact]
    public void Compute_PureDisjoint_NoMatchingNoExcess()
    {
        // Genome1: innovations [1, 3]
        // Genome2: innovations [2, 4]
        // Two-pointer: 1 vs 2 → disjoint(g1), 3 vs 2 → disjoint(g2),
        // 3 vs 4 → disjoint(g1), remaining 4 → excess(g2)
        // D=3, E=1, W=0, N=max(2,2)=2
        // Wait, let me redo: range of innovations is 1..4
        // g1 max innovation = 3, g2 max innovation = 4
        // Within range [min, max(g1.max, g2.max)] = [1, 4]
        // Genes beyond the OTHER genome's max are excess
        // g2's innovation 4 > g1's max 3 → excess from g2
        // g1 innovation 1: not in g2 within g2's range [2,4] → disjoint
        // g1 innovation 3: not in g2 within g2's range [2,4] → disjoint
        // g2 innovation 2: not in g1 within g1's range [1,3] → disjoint
        // D=3, E=1, W=0, N=max(2,2)=2
        // d = (1*1/2) + (1*3/2) + (0.4*0) = 0.5 + 1.5 = 2.0
        var nodes = new NodeGene[]
        {
            new(0, NodeType.Input),
            new(1, NodeType.Output),
            new(2, NodeType.Hidden)
        };
        var genome1 = MakeGenome(nodes,
            new ConnectionGene(1, 0, 1, 1.0, true),
            new ConnectionGene(3, 0, 2, 1.0, true));
        var genome2 = MakeGenome(nodes,
            new ConnectionGene(2, 0, 2, 1.0, true),
            new ConnectionGene(4, 2, 1, 1.0, true));

        var sut = CreateSut();

        var distance = sut.Compute(genome1, genome2);

        // Two-pointer merge: 1 vs 2 → disjoint (g1), advance g1
        // 3 vs 2 → disjoint (g2), advance g2
        // 3 vs 4 → disjoint (g1), advance g1
        // remaining 4 → excess (g2)
        // D=3, E=1, N=2
        // d = (1*1/2) + (1*3/2) + (0.4*0) = 0.5 + 1.5 = 2.0
        distance.Should().Be(2.0);
    }

    #endregion

    #region Excess Genes Only

    [Fact]
    public void Compute_OnlyExcessGenes_CountsCorrectly()
    {
        // Genome1: innovations [0, 1, 2, 3]
        // Genome2: innovations [0, 1]
        // Matching: 0, 1 (same weights → W=0)
        // Excess from g1: 2, 3 (beyond g2's max innovation 1)
        // E=2, D=0, W=0, N=max(4,2)=4
        // d = (1*2/4) + (1*0/4) + (0.4*0) = 0.5
        var nodes = new NodeGene[]
        {
            new(0, NodeType.Input),
            new(1, NodeType.Output),
            new(2, NodeType.Hidden)
        };
        var genome1 = MakeGenome(nodes,
            new ConnectionGene(0, 0, 1, 1.0, true),
            new ConnectionGene(1, 0, 2, 1.0, true),
            new ConnectionGene(2, 2, 1, 1.0, true),
            new ConnectionGene(3, 0, 1, 1.0, true));
        var genome2 = MakeGenome(nodes,
            new ConnectionGene(0, 0, 1, 1.0, true),
            new ConnectionGene(1, 0, 2, 1.0, true));

        var sut = CreateSut();

        var distance = sut.Compute(genome1, genome2);

        distance.Should().Be(0.5, "E=2, D=0, W=0, N=4 → (1*2/4)+(1*0/4)+(0.4*0) = 0.5");
    }

    [Fact]
    public void Compute_ExcessFromGenome2_CountsCorrectly()
    {
        // Genome1: innovations [0]
        // Genome2: innovations [0, 1, 2]
        // Matching: 0 (same weight → W=0)
        // Excess from g2: 1, 2
        // E=2, D=0, W=0, N=max(1,3)=3
        // d = (1*2/3) + (1*0/3) + (0.4*0) = 0.6667
        var nodes = new NodeGene[]
        {
            new(0, NodeType.Input),
            new(1, NodeType.Output),
            new(2, NodeType.Hidden)
        };
        var genome1 = MakeGenome(nodes,
            new ConnectionGene(0, 0, 1, 1.0, true));
        var genome2 = MakeGenome(nodes,
            new ConnectionGene(0, 0, 1, 1.0, true),
            new ConnectionGene(1, 0, 2, 1.0, true),
            new ConnectionGene(2, 2, 1, 1.0, true));

        var sut = CreateSut();

        var distance = sut.Compute(genome1, genome2);

        distance.Should().BeApproximately(2.0 / 3.0, 1e-10,
            "E=2, D=0, W=0, N=3 → (1*2/3) ≈ 0.6667");
    }

    #endregion

    #region Average Weight Difference

    [Fact]
    public void Compute_MatchingGenesWithDifferentWeights_ComputesAverageWeightDifference()
    {
        // Both genomes have innovations 0, 1 (all matching)
        // Weights: g1=[1.0, 3.0], g2=[2.0, 5.0]
        // |1.0-2.0| = 1.0, |3.0-5.0| = 2.0, avg = 1.5
        // E=0, D=0, W=1.5, N=max(2,2)=2
        // d = (1*0/2) + (1*0/2) + (0.4*1.5) = 0.6
        var genome1 = MakeGenome(MinimalNodes,
            new ConnectionGene(0, 0, 1, 1.0, true),
            new ConnectionGene(1, 0, 1, 3.0, true));
        var genome2 = MakeGenome(MinimalNodes,
            new ConnectionGene(0, 0, 1, 2.0, true),
            new ConnectionGene(1, 0, 1, 5.0, true));

        var sut = CreateSut();

        var distance = sut.Compute(genome1, genome2);

        distance.Should().BeApproximately(0.6, 1e-10, "E=0, D=0, W=1.5 → 0.4*1.5 = 0.6");
    }

    [Fact]
    public void Compute_SingleMatchingGeneDifferentWeight_CorrectDistance()
    {
        // g1 weight=1.0, g2 weight=3.0
        // W = |1.0-3.0| / 1 = 2.0
        // E=0, D=0, N=max(1,1)=1
        // d = 0 + 0 + 0.4*2.0 = 0.8
        var genome1 = MakeGenome(MinimalNodes,
            new ConnectionGene(0, 0, 1, 1.0, true));
        var genome2 = MakeGenome(MinimalNodes,
            new ConnectionGene(0, 0, 1, 3.0, true));

        var sut = CreateSut();

        var distance = sut.Compute(genome1, genome2);

        distance.Should().Be(0.8, "W=2.0 → 0.4*2.0 = 0.8");
    }

    #endregion

    #region N = max(connection count of larger genome, 1)

    [Fact]
    public void Compute_OneEmptyGenome_NIsOne()
    {
        // Genome1: no connections
        // Genome2: innovations [0]
        // All of g2's connections are excess (beyond g1's max which is -∞, so all excess)
        // E=1, D=0, W=0, N=max(0,1)=1
        // d = (1*1/1) + (1*0/1) + (0.4*0) = 1.0
        var genome1 = MakeGenome(MinimalNodes);
        var genome2 = MakeGenome(MinimalNodes,
            new ConnectionGene(0, 0, 1, 1.0, true));

        var sut = CreateSut();

        var distance = sut.Compute(genome1, genome2);

        distance.Should().Be(1.0, "E=1, D=0, W=0, N=max(0,1)=1 → 1.0");
    }

    [Fact]
    public void Compute_NUsesLargerGenome()
    {
        // Genome1: 4 connections [0,1,2,3]
        // Genome2: 2 connections [0,1] (same weights)
        // E=2 (innov 2,3 excess from g1), D=0, W=0, N=max(4,2)=4
        // d = (1*2/4) = 0.5
        var nodes = new NodeGene[]
        {
            new(0, NodeType.Input),
            new(1, NodeType.Output),
            new(2, NodeType.Hidden)
        };
        var genome1 = MakeGenome(nodes,
            new ConnectionGene(0, 0, 1, 1.0, true),
            new ConnectionGene(1, 0, 2, 1.0, true),
            new ConnectionGene(2, 2, 1, 1.0, true),
            new ConnectionGene(3, 0, 1, 1.0, true));
        var genome2 = MakeGenome(nodes,
            new ConnectionGene(0, 0, 1, 1.0, true),
            new ConnectionGene(1, 0, 2, 1.0, true));

        var sut = CreateSut();

        var distance = sut.Compute(genome1, genome2);

        distance.Should().Be(0.5, "N=4 (larger genome), E=2 → 2/4 = 0.5");
    }

    #endregion

    #region Configurable Coefficients

    [Fact]
    public void Compute_CustomCoefficients_AppliedCorrectly()
    {
        // c1=2, c2=3, c3=0.5
        // Genome1: innovations [0, 2] weights [1.0, 3.0]
        // Genome2: innovations [0, 1] weights [2.0, 2.0]
        // Matching: innovation 0, |1.0-2.0|=1.0, W=1.0
        // Disjoint: innovation 1 (g2 only, within range [0,2])
        // Excess: innovation 2 (g1 only, beyond g2 max=1) — wait no,
        // innovation 2 > g2's max innovation (1), so it's excess
        // D=1 (innovation 1), E=1 (innovation 2), W=1.0, N=max(2,2)=2
        // d = (2*1/2) + (3*1/2) + (0.5*1.0) = 1.0 + 1.5 + 0.5 = 3.0
        var nodes = new NodeGene[]
        {
            new(0, NodeType.Input),
            new(1, NodeType.Output),
            new(2, NodeType.Hidden)
        };
        var options = new NeatSharpOptions
        {
            Speciation =
            {
                ExcessCoefficient = 2.0,
                DisjointCoefficient = 3.0,
                WeightDifferenceCoefficient = 0.5
            }
        };
        var genome1 = MakeGenome(nodes,
            new ConnectionGene(0, 0, 1, 1.0, true),
            new ConnectionGene(2, 0, 2, 3.0, true));
        var genome2 = MakeGenome(nodes,
            new ConnectionGene(0, 0, 1, 2.0, true),
            new ConnectionGene(1, 0, 2, 2.0, true));

        var sut = CreateSut(options);

        var distance = sut.Compute(genome1, genome2);

        distance.Should().Be(3.0,
            "c1=2, E=1, c2=3, D=1, c3=0.5, W=1.0, N=2 → (2*1/2)+(3*1/2)+(0.5*1.0) = 3.0");
    }

    [Fact]
    public void Compute_ZeroCoefficients_IgnoresComponents()
    {
        // With c1=0, c2=0, c3=0 → distance should always be 0
        var options = new NeatSharpOptions
        {
            Speciation =
            {
                ExcessCoefficient = 0.0,
                DisjointCoefficient = 0.0,
                WeightDifferenceCoefficient = 0.0
            }
        };
        var genome1 = MakeGenome(MinimalNodes,
            new ConnectionGene(0, 0, 1, 1.0, true),
            new ConnectionGene(1, 0, 1, 2.0, true));
        var genome2 = MakeGenome(MinimalNodes,
            new ConnectionGene(0, 0, 1, 5.0, true));

        var sut = CreateSut(options);

        var distance = sut.Compute(genome1, genome2);

        distance.Should().Be(0.0, "all coefficients are 0");
    }

    #endregion

    #region Full Formula Verification

    [Fact]
    public void Compute_MixedExcessDisjointAndWeightDifference_FullFormulaCorrect()
    {
        // Genome1: innovations [0, 1, 3, 5] weights [1.0, 2.0, 3.0, 4.0]
        // Genome2: innovations [0, 2, 3] weights [1.5, 2.5, 4.0]
        //
        // Two-pointer merge:
        // g1[0]=innov0 vs g2[0]=innov0 → match, |1.0-1.5|=0.5
        // g1[1]=innov1 vs g2[1]=innov2 → g1 disjoint (1 < 2)
        // g1[2]=innov3 vs g2[1]=innov2 → g2 disjoint (2 < 3)
        // g1[2]=innov3 vs g2[2]=innov3 → match, |3.0-4.0|=1.0
        // g1[3]=innov5 → remaining, excess
        //
        // D=2 (innovations 1, 2), E=1 (innovation 5), matchCount=2
        // W = (0.5 + 1.0) / 2 = 0.75
        // N = max(4, 3) = 4
        // d = (1*1/4) + (1*2/4) + (0.4*0.75) = 0.25 + 0.5 + 0.3 = 1.05
        var nodes = new NodeGene[]
        {
            new(0, NodeType.Input),
            new(1, NodeType.Output),
            new(2, NodeType.Hidden),
            new(3, NodeType.Hidden)
        };
        var genome1 = MakeGenome(nodes,
            new ConnectionGene(0, 0, 1, 1.0, true),
            new ConnectionGene(1, 0, 2, 2.0, true),
            new ConnectionGene(3, 2, 1, 3.0, true),
            new ConnectionGene(5, 0, 3, 4.0, true));
        var genome2 = MakeGenome(nodes,
            new ConnectionGene(0, 0, 1, 1.5, true),
            new ConnectionGene(2, 0, 2, 2.5, true),
            new ConnectionGene(3, 2, 1, 4.0, true));

        var sut = CreateSut();

        var distance = sut.Compute(genome1, genome2);

        distance.Should().BeApproximately(1.05, 1e-10,
            "D=2, E=1, W=0.75, N=4 → (1/4)+(2/4)+(0.4*0.75) = 1.05");
    }

    #endregion

    #region Symmetry

    [Fact]
    public void Compute_IsSymmetric()
    {
        var nodes = new NodeGene[]
        {
            new(0, NodeType.Input),
            new(1, NodeType.Output),
            new(2, NodeType.Hidden)
        };
        var genome1 = MakeGenome(nodes,
            new ConnectionGene(0, 0, 1, 1.0, true),
            new ConnectionGene(1, 0, 2, 2.0, true),
            new ConnectionGene(3, 2, 1, 3.0, true));
        var genome2 = MakeGenome(nodes,
            new ConnectionGene(0, 0, 1, 1.5, true),
            new ConnectionGene(2, 0, 2, 2.5, true));

        var sut = CreateSut();

        var d12 = sut.Compute(genome1, genome2);
        var d21 = sut.Compute(genome2, genome1);

        d12.Should().Be(d21, "compatibility distance should be symmetric");
    }

    #endregion

    #region No Matching Genes

    [Fact]
    public void Compute_NoMatchingGenes_WIsZero()
    {
        // Genome1: innovations [0, 1]
        // Genome2: innovations [2, 3]
        // All disjoint/excess, no matches → W=0
        var nodes = new NodeGene[]
        {
            new(0, NodeType.Input),
            new(1, NodeType.Output),
            new(2, NodeType.Hidden)
        };
        var genome1 = MakeGenome(nodes,
            new ConnectionGene(0, 0, 1, 1.0, true),
            new ConnectionGene(1, 0, 2, 2.0, true));
        var genome2 = MakeGenome(nodes,
            new ConnectionGene(2, 0, 2, 3.0, true),
            new ConnectionGene(3, 2, 1, 4.0, true));

        var sut = CreateSut();

        var distance = sut.Compute(genome1, genome2);

        // Two-pointer: 0 vs 2 → disjoint(g1), 1 vs 2 → disjoint(g1),
        // remaining 2, 3 from g2 → excess
        // D=2, E=2, W=0, N=max(2,2)=2
        // d = (1*2/2) + (1*2/2) + (0.4*0) = 1 + 1 = 2.0
        distance.Should().Be(2.0, "D=2, E=2, W=0, N=2 → 2.0");
    }

    #endregion

    #region Non-Negative Result

    [Fact]
    public void Compute_AlwaysReturnsNonNegative()
    {
        var genome1 = MakeGenome(MinimalNodes,
            new ConnectionGene(0, 0, 1, -4.0, true));
        var genome2 = MakeGenome(MinimalNodes,
            new ConnectionGene(0, 0, 1, 4.0, true));

        var sut = CreateSut();

        var distance = sut.Compute(genome1, genome2);

        distance.Should().BeGreaterOrEqualTo(0.0);
    }

    #endregion
}
