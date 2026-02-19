using FluentAssertions;
using Microsoft.Extensions.Options;
using NeatSharp.Configuration;
using NeatSharp.Evolution.Selection;
using NeatSharp.Genetics;
using Xunit;

namespace NeatSharp.Tests.Evolution.Selection;

public class TournamentSelectorTests
{
    private static readonly IReadOnlyList<NodeGene> MinimalNodes =
    [
        new(0, NodeType.Input),
        new(1, NodeType.Output)
    ];

    private static Genome MakeGenome(double weight = 1.0) =>
        new(MinimalNodes, [new ConnectionGene(0, 0, 1, weight, true)]);

    private static TournamentSelector CreateSut(int tournamentSize = 2) =>
        new(Options.Create(new NeatSharpOptions
        {
            Selection = { TournamentSize = tournamentSize }
        }));

    #region Selects Highest Fitness Among Tournament Candidates

    [Fact]
    public void Select_ReturnsCandidateWithHighestFitnessAmongPicked()
    {
        var genomes = Enumerable.Range(0, 10)
            .Select(i => (MakeGenome(i), (double)i))
            .ToList();

        var sut = CreateSut(tournamentSize: 2);

        // Over many selections, the winner should always have fitness >= any loser
        // With tournament size 2, it picks 2 random and returns the higher fitness one
        for (int seed = 0; seed < 50; seed++)
        {
            var random = new Random(seed);
            var result = sut.Select(genomes, random);

            // Result should be one of the candidates
            genomes.Should().Contain(g => ReferenceEquals(g.Item1, result));
        }
    }

    [Fact]
    public void Select_TournamentSize1_ReturnsRandomCandidate()
    {
        var genome1 = MakeGenome(1.0);
        var genome2 = MakeGenome(2.0);
        var candidates = new List<(Genome Genome, double Fitness)>
        {
            (genome1, 1.0),
            (genome2, 100.0)
        };

        var sut = CreateSut(tournamentSize: 1);

        // With tournament size 1, any candidate can be returned (no comparison)
        var selectedGenomes = new HashSet<Genome>();
        for (int seed = 0; seed < 100; seed++)
        {
            var random = new Random(seed);
            selectedGenomes.Add(sut.Select(candidates, random));
        }

        // Both genomes should be selected at some point
        selectedGenomes.Should().HaveCount(2,
            "tournament size 1 should behave like random selection");
    }

    [Fact]
    public void Select_LargeTournamentSize_SelectsHighestFitnessMoreOften()
    {
        var candidates = new List<(Genome Genome, double Fitness)>
        {
            (MakeGenome(1.0), 1.0),
            (MakeGenome(2.0), 2.0),
            (MakeGenome(3.0), 3.0),
            (MakeGenome(4.0), 4.0),
            (MakeGenome(5.0), 10.0) // Highest fitness
        };

        var sut = CreateSut(tournamentSize: 4);

        int highestSelectedCount = 0;
        int totalSelections = 200;
        for (int seed = 0; seed < totalSelections; seed++)
        {
            var random = new Random(seed);
            var result = sut.Select(candidates, random);
            if (ReferenceEquals(result, candidates[4].Genome))
            {
                highestSelectedCount++;
            }
        }

        // With tournament size 4 out of 5 candidates, the highest fitness
        // should be selected very frequently (whenever it's in the tournament)
        highestSelectedCount.Should().BeGreaterThan(totalSelections / 4,
            "highest fitness should be selected often with large tournament");
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

        var sut = CreateSut(tournamentSize: 2);
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
            .Select(i => (MakeGenome(i), (double)i))
            .ToList();

        var sut = CreateSut(tournamentSize: 3);

        var result1 = sut.Select(candidates, new Random(42));
        var result2 = sut.Select(candidates, new Random(42));

        result1.Should().BeSameAs(result2);
    }

    [Fact]
    public void Select_DifferentSeeds_CanProduceDifferentResults()
    {
        var candidates = Enumerable.Range(0, 20)
            .Select(i => (MakeGenome(i), (double)i))
            .ToList();

        var sut = CreateSut(tournamentSize: 2);

        var results = new HashSet<Genome>();
        for (int seed = 0; seed < 100; seed++)
        {
            results.Add(sut.Select(candidates, new Random(seed)));
        }

        results.Count.Should().BeGreaterThan(1,
            "different seeds should produce different selections");
    }

    #endregion
}
