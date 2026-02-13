using FluentAssertions;
using NeatSharp.Evolution;
using NeatSharp.Genetics;
using NeatSharp.Reporting;
using Xunit;

namespace NeatSharp.Tests.Evolution;

public class EvolutionResultTests
{
    [Fact]
    public void Construction_SetsAllProperties()
    {
        var genome = new StubGenome(5, 10);
        var champion = new Champion(genome, 3.95, 42);
        var population = new PopulationSnapshot(
            new[] { new SpeciesSnapshot(1, new[] { new GenomeInfo(3.95, 5, 10) }) },
            1);
        var history = new RunHistory(Array.Empty<GenerationStatistics>(), 42);

        var result = new EvolutionResult(champion, population, history, Seed: 123, WasCancelled: false);

        result.Champion.Should().BeSameAs(champion);
        result.Population.Should().BeSameAs(population);
        result.History.Should().BeSameAs(history);
        result.Seed.Should().Be(123);
        result.WasCancelled.Should().BeFalse();
    }

    [Fact]
    public void Seed_IsNonNullableInt()
    {
        var result = CreateMinimalResult(seed: 42);

        result.Seed.Should().Be(42);
        result.Seed.GetType().Should().Be(typeof(int));
    }

    [Fact]
    public void WasCancelled_True_IndicatesCancellation()
    {
        var result = CreateMinimalResult(wasCancelled: true);

        result.WasCancelled.Should().BeTrue();
    }

    [Fact]
    public void WasCancelled_False_IndicatesNormalCompletion()
    {
        var result = CreateMinimalResult(wasCancelled: false);

        result.WasCancelled.Should().BeFalse();
    }

    [Fact]
    public void Record_SupportsValueEquality()
    {
        var genome = new StubGenome(5, 10);
        var champion = new Champion(genome, 3.95, 42);
        var population = new PopulationSnapshot(Array.Empty<SpeciesSnapshot>(), 0);
        var history = new RunHistory(Array.Empty<GenerationStatistics>(), 0);

        var result1 = new EvolutionResult(champion, population, history, 42, false);
        var result2 = new EvolutionResult(champion, population, history, 42, false);

        result1.Should().Be(result2);
    }

    [Fact]
    public void Record_WithExpression_CreatesModifiedCopy()
    {
        var original = CreateMinimalResult(seed: 42, wasCancelled: false);

        var modified = original with { WasCancelled = true };

        modified.Seed.Should().Be(42);
        modified.WasCancelled.Should().BeTrue();
        original.WasCancelled.Should().BeFalse();
    }

    private static EvolutionResult CreateMinimalResult(int seed = 0, bool wasCancelled = false)
    {
        var genome = new StubGenome(1, 1);
        var champion = new Champion(genome, 1.0, 0);
        var population = new PopulationSnapshot(Array.Empty<SpeciesSnapshot>(), 0);
        var history = new RunHistory(Array.Empty<GenerationStatistics>(), 0);
        return new EvolutionResult(champion, population, history, seed, wasCancelled);
    }

    private sealed class StubGenome(int nodeCount, int connectionCount) : IGenome
    {
        public int NodeCount => nodeCount;
        public int ConnectionCount => connectionCount;

        public void Activate(ReadOnlySpan<double> inputs, Span<double> outputs)
        {
            // Stub: no-op
        }
    }
}
