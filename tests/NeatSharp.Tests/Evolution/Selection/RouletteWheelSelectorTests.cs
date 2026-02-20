using FluentAssertions;
using NeatSharp.Evolution.Selection;
using NeatSharp.Genetics;
using Xunit;

namespace NeatSharp.Tests.Evolution.Selection;

public class RouletteWheelSelectorTests
{
    private static readonly IReadOnlyList<NodeGene> MinimalNodes =
    [
        new(0, NodeType.Input),
        new(1, NodeType.Output)
    ];

    private static Genome MakeGenome(double weight = 1.0) =>
        new(MinimalNodes, [new ConnectionGene(0, 0, 1, weight, true)]);

    private static RouletteWheelSelector CreateSut() => new();

    #region Fitness-Proportional Selection

    [Fact]
    public void Select_HigherFitnessSelectedMoreOften()
    {
        var highFitGenome = MakeGenome(1.0);
        var lowFitGenome = MakeGenome(2.0);

        var candidates = new List<(Genome Genome, double Fitness)>
        {
            (highFitGenome, 90.0),
            (lowFitGenome, 10.0)
        };

        var sut = CreateSut();

        int highFitCount = 0;
        int totalSelections = 1000;
        for (int seed = 0; seed < totalSelections; seed++)
        {
            var random = new Random(seed);
            var result = sut.Select(candidates, random);
            if (ReferenceEquals(result, highFitGenome))
            {
                highFitCount++;
            }
        }

        // With 90% fitness proportion, high-fit should be selected ~90% of the time
        double proportion = (double)highFitCount / totalSelections;
        proportion.Should().BeGreaterThan(0.75,
            "candidate with 90% of total fitness should be selected most of the time");
        proportion.Should().BeLessThan(0.99,
            "low-fitness candidate should sometimes be selected");
    }

    [Fact]
    public void Select_EqualFitness_EqualSelectionProbability()
    {
        var genome1 = MakeGenome(1.0);
        var genome2 = MakeGenome(2.0);

        var candidates = new List<(Genome Genome, double Fitness)>
        {
            (genome1, 50.0),
            (genome2, 50.0)
        };

        var sut = CreateSut();

        int genome1Count = 0;
        int totalSelections = 1000;
        for (int seed = 0; seed < totalSelections; seed++)
        {
            var random = new Random(seed);
            var result = sut.Select(candidates, random);
            if (ReferenceEquals(result, genome1))
            {
                genome1Count++;
            }
        }

        double proportion = (double)genome1Count / totalSelections;
        proportion.Should().BeInRange(0.4, 0.6,
            "equal fitness should give roughly equal selection probability");
    }

    #endregion

    #region Negative and Zero Fitness Handling

    [Fact]
    public void Select_NegativeFitness_ShiftedByMinPlusEpsilon()
    {
        var genome1 = MakeGenome(1.0);
        var genome2 = MakeGenome(2.0);

        // genome1 has higher fitness after shifting
        var candidates = new List<(Genome Genome, double Fitness)>
        {
            (genome1, -5.0),
            (genome2, -10.0)
        };

        var sut = CreateSut();

        int genome1Count = 0;
        int totalSelections = 500;
        for (int seed = 0; seed < totalSelections; seed++)
        {
            var random = new Random(seed);
            var result = sut.Select(candidates, random);
            if (ReferenceEquals(result, genome1))
            {
                genome1Count++;
            }
        }

        // genome1 has relatively higher fitness (-5 vs -10), so should be selected more often
        double proportion = (double)genome1Count / totalSelections;
        proportion.Should().BeGreaterThan(0.5,
            "candidate with higher (less negative) fitness should be selected more often");
    }

    [Fact]
    public void Select_ZeroFitness_ShiftedCorrectly()
    {
        var genome1 = MakeGenome(1.0);
        var genome2 = MakeGenome(2.0);

        var candidates = new List<(Genome Genome, double Fitness)>
        {
            (genome1, 0.0),
            (genome2, 10.0)
        };

        var sut = CreateSut();

        int genome2Count = 0;
        int totalSelections = 500;
        for (int seed = 0; seed < totalSelections; seed++)
        {
            var random = new Random(seed);
            var result = sut.Select(candidates, random);
            if (ReferenceEquals(result, genome2))
            {
                genome2Count++;
            }
        }

        // genome2 should be selected much more often
        double proportion = (double)genome2Count / totalSelections;
        proportion.Should().BeGreaterThan(0.8,
            "candidate with 10.0 fitness should dominate when other has 0.0");
    }

    [Fact]
    public void Select_AllNegativeFitness_StillSelects()
    {
        var candidates = new List<(Genome Genome, double Fitness)>
        {
            (MakeGenome(1.0), -5.0),
            (MakeGenome(2.0), -10.0),
            (MakeGenome(3.0), -3.0)
        };

        var sut = CreateSut();
        var random = new Random(42);

        // Should not throw — all fitness shifted to positive
        var result = sut.Select(candidates, random);

        candidates.Should().Contain(c => ReferenceEquals(c.Genome, result));
    }

    #endregion

    #region Single Candidate

    [Fact]
    public void Select_SingleCandidate_ReturnsThatCandidate()
    {
        var genome = MakeGenome();
        var candidates = new List<(Genome Genome, double Fitness)>
        {
            (genome, 5.0)
        };

        var sut = CreateSut();
        var random = new Random(42);

        var result = sut.Select(candidates, random);

        result.Should().BeSameAs(genome);
    }

    #endregion

    #region Determinism

    [Fact]
    public void Select_Deterministic_SameResultWithSameSeed()
    {
        var candidates = Enumerable.Range(0, 10)
            .Select(i => (MakeGenome(i), (double)(i + 1)))
            .ToList();

        var sut = CreateSut();

        var result1 = sut.Select(candidates, new Random(42));
        var result2 = sut.Select(candidates, new Random(42));

        result1.Should().BeSameAs(result2);
    }

    #endregion
}
