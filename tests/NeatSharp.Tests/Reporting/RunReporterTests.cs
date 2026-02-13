using FluentAssertions;
using NeatSharp.Evolution;
using NeatSharp.Reporting;
using NeatSharp.Tests.TestDoubles;
using Xunit;

namespace NeatSharp.Tests.Reporting;

public class RunReporterTests
{
    private readonly RunReporter _reporter = new();

    [Fact]
    public void GenerateSummary_IncludesChampionFitness()
    {
        var result = CreateResult(championFitness: 3.95);

        var summary = _reporter.GenerateSummary(result);

        summary.Should().Contain("3.95");
    }

    [Fact]
    public void GenerateSummary_IncludesGenerationCount()
    {
        var result = CreateResult(totalGenerations: 42);

        var summary = _reporter.GenerateSummary(result);

        summary.Should().Contain("42");
    }

    [Fact]
    public void GenerateSummary_IncludesSeed()
    {
        var result = CreateResult(seed: 12345);

        var summary = _reporter.GenerateSummary(result);

        summary.Should().Contain("12345");
    }

    [Fact]
    public void GenerateSummary_IncludesSpeciesCount()
    {
        var species = new[]
        {
            new SpeciesSnapshot(1, new[] { new GenomeInfo(3.0, 5, 10) }),
            new SpeciesSnapshot(2, new[] { new GenomeInfo(2.5, 4, 8) }),
            new SpeciesSnapshot(3, new[] { new GenomeInfo(1.0, 3, 6) })
        };
        var population = new PopulationSnapshot(species, 3);
        var result = CreateResult(population: population);

        var summary = _reporter.GenerateSummary(result);

        summary.Should().Contain("3");
    }

    [Fact]
    public void GenerateSummary_WhenCancelled_IndicatesCancellation()
    {
        var result = CreateResult(wasCancelled: true);

        var summary = _reporter.GenerateSummary(result);

        summary.Should().ContainEquivalentOf("cancelled");
    }

    [Fact]
    public void GenerateSummary_WhenNotCancelled_DoesNotIndicateCancellation()
    {
        var result = CreateResult(wasCancelled: false);

        var summary = _reporter.GenerateSummary(result);

        summary.Should().NotContainEquivalentOf("cancelled");
    }

    [Fact]
    public void GenerateSummary_ReturnsNonEmptyString()
    {
        var result = CreateResult();

        var summary = _reporter.GenerateSummary(result);

        summary.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void GenerateSummary_IncludesChampionGeneration()
    {
        var result = CreateResult(championGeneration: 99);

        var summary = _reporter.GenerateSummary(result);

        summary.Should().Contain("99");
    }

    [Fact]
    public void GenerateSummary_ImplementsIRunReporter()
    {
        IRunReporter reporter = _reporter;

        var result = CreateResult();
        var summary = reporter.GenerateSummary(result);

        summary.Should().NotBeNullOrWhiteSpace();
    }

    private static EvolutionResult CreateResult(
        double championFitness = 1.0,
        int championGeneration = 0,
        int totalGenerations = 0,
        int seed = 0,
        bool wasCancelled = false,
        PopulationSnapshot? population = null)
    {
        var genome = new StubGenome(1, 1);
        var champion = new Champion(genome, championFitness, championGeneration);
        population ??= new PopulationSnapshot(
            new[] { new SpeciesSnapshot(1, new[] { new GenomeInfo(championFitness, 1, 1) }) },
            1);
        var history = new RunHistory(Array.Empty<GenerationStatistics>(), totalGenerations);
        return new EvolutionResult(champion, population, history, seed, wasCancelled);
    }

}
