using FluentAssertions;
using NeatSharp.Evolution.Selection;
using NeatSharp.Genetics;
using Xunit;

namespace NeatSharp.Tests.Evolution.Selection;

public class SinglePointerSelectorTests
{
    private static readonly IReadOnlyList<NodeGene> MinimalNodes =
    [
        new(0, NodeType.Input),
        new(1, NodeType.Output)
    ];

    private static Genome MakeGenome(double weight = 1.0) =>
        new(MinimalNodes, [new ConnectionGene(0, 0, 1, weight, true)]);

    private static SinglePointerSelector CreateSut() => new();

    #region Fitness-Proportional Selection

    [Fact]
    public void Select_FitnessProportional_HigherFitnessSelectedMoreOften()
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
                highFitCount++;
        }

        double proportion = (double)highFitCount / totalSelections;
        proportion.Should().BeGreaterThan(0.75,
            "candidate with 90% of total fitness should be selected most of the time");
    }

    #endregion

    #region More Uniform Than Roulette

    [Fact]
    public void Select_MoreUniformThanRoulette_OverBatchSelections()
    {
        // With equal fitness, both selectors should produce a very uniform distribution
        var genomes = Enumerable.Range(0, 5)
            .Select(i => (MakeGenome(i), 10.0)) // Equal fitness
            .ToList();

        var sut = CreateSut();
        var roulette = new RouletteWheelSelector();

        // Track selection frequency for SinglePointer
        var singlePointerFrequency = new int[5];
        var rouletteFrequency = new int[5];
        int totalSelections = 1000;

        for (int seed = 0; seed < totalSelections; seed++)
        {
            var singlePointerResult = sut.Select(genomes, new Random(seed));
            var rouletteResult = roulette.Select(genomes, new Random(seed));

            for (int i = 0; i < 5; i++)
            {
                if (ReferenceEquals(singlePointerResult, genomes[i].Item1))
                    singlePointerFrequency[i]++;
                if (ReferenceEquals(rouletteResult, genomes[i].Item1))
                    rouletteFrequency[i]++;
            }
        }

        // With equal fitness, both should roughly select each candidate 20% of the time
        double expectedFreq = totalSelections / 5.0;
        double singlePointerVariance = singlePointerFrequency.Select(f => Math.Pow(f - expectedFreq, 2)).Average();
        double rouletteVariance = rouletteFrequency.Select(f => Math.Pow(f - expectedFreq, 2)).Average();

        // Variance should be comparable
        singlePointerVariance.Should().BeLessThanOrEqualTo(rouletteVariance * 1.5,
            "SinglePointerSelector should produce at least as uniform a distribution as roulette");
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

    #region Negative Fitness Handling

    [Fact]
    public void Select_NegativeFitness_ShiftedCorrectly()
    {
        var candidates = new List<(Genome Genome, double Fitness)>
        {
            (MakeGenome(1.0), -5.0),
            (MakeGenome(2.0), -10.0),
            (MakeGenome(3.0), -3.0)
        };

        var sut = CreateSut();
        var random = new Random(42);

        // Should not throw
        var result = sut.Select(candidates, random);

        candidates.Should().Contain(c => ReferenceEquals(c.Genome, result));
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
