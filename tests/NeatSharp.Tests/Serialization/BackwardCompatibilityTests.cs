using FluentAssertions;
using NeatSharp.Genetics;
using NeatSharp.Serialization;
using NeatSharp.Serialization.Migration;
using Xunit;

namespace NeatSharp.Tests.Serialization;

/// <summary>
/// Backward compatibility tests verifying that v1.0.0 checkpoint fixtures
/// can be loaded correctly and that the migration infrastructure behaves
/// as expected for the current schema version.
/// </summary>
public class BackwardCompatibilityTests
{
    private readonly CheckpointSerializer _serializer;
    private readonly SchemaVersionMigrator _migrator;

    public BackwardCompatibilityTests()
    {
        var validator = new CheckpointValidator();
        _migrator = new SchemaVersionMigrator();
        _serializer = new CheckpointSerializer(validator, _migrator);
    }

    [Fact]
    public async Task LoadAsync_V100Fixture_AllFieldsPopulatedCorrectly()
    {
        // Arrange
        var fixturePath = Path.Combine(
            AppContext.BaseDirectory, "Serialization", "Fixtures", "v1_0_0_checkpoint.json");

        // Act
        TrainingCheckpoint checkpoint;
        using (var stream = File.OpenRead(fixturePath))
        {
            checkpoint = await _serializer.LoadAsync(stream);
        }

        // Assert — Population
        checkpoint.Population.Should().HaveCount(2);

        // Genome 1: minimal genome (2 nodes, 1 connection)
        var genome1 = checkpoint.Population[0];
        genome1.Nodes.Should().HaveCount(2);
        genome1.Nodes[0].Id.Should().Be(0);
        genome1.Nodes[0].Type.Should().Be(NodeType.Input);
        genome1.Nodes[1].Id.Should().Be(1);
        genome1.Nodes[1].Type.Should().Be(NodeType.Output);
        genome1.Connections.Should().HaveCount(1);
        genome1.Connections[0].InnovationNumber.Should().Be(1);
        genome1.Connections[0].SourceNodeId.Should().Be(0);
        genome1.Connections[0].TargetNodeId.Should().Be(1);
        genome1.Connections[0].Weight.Should().Be(0.5);
        genome1.Connections[0].IsEnabled.Should().BeTrue();

        // Genome 2: genome with hidden node (3 nodes, 3 connections)
        var genome2 = checkpoint.Population[1];
        genome2.Nodes.Should().HaveCount(3);
        genome2.Nodes[0].Id.Should().Be(0);
        genome2.Nodes[0].Type.Should().Be(NodeType.Input);
        genome2.Nodes[1].Id.Should().Be(1);
        genome2.Nodes[1].Type.Should().Be(NodeType.Output);
        genome2.Nodes[2].Id.Should().Be(2);
        genome2.Nodes[2].Type.Should().Be(NodeType.Hidden);
        genome2.Nodes[2].ActivationFunction.Should().Be("tanh");
        genome2.Connections.Should().HaveCount(3);
        genome2.Connections[0].InnovationNumber.Should().Be(1);
        genome2.Connections[0].Weight.Should().Be(0.7);
        genome2.Connections[0].IsEnabled.Should().BeTrue();
        genome2.Connections[1].InnovationNumber.Should().Be(2);
        genome2.Connections[1].Weight.Should().Be(-0.3);
        genome2.Connections[2].InnovationNumber.Should().Be(3);
        genome2.Connections[2].Weight.Should().Be(0.1);
        genome2.Connections[2].IsEnabled.Should().BeFalse();

        // Assert — Species
        checkpoint.Species.Should().HaveCount(1);
        var species = checkpoint.Species[0];
        species.Id.Should().Be(1);
        species.RepresentativeIndex.Should().Be(0);
        species.BestFitnessEver.Should().Be(0.95);
        species.GenerationsSinceImprovement.Should().Be(3);
        species.MemberIndices.Should().BeEquivalentTo([0, 1]);
        species.MemberFitnesses.Should().BeEquivalentTo([0.8, 0.95]);

        // Assert — Counters
        checkpoint.NextInnovationNumber.Should().Be(4);
        checkpoint.NextNodeId.Should().Be(3);
        checkpoint.NextSpeciesId.Should().Be(2);

        // Assert — Champion
        checkpoint.ChampionGenome.Nodes.Should().HaveCount(3);
        checkpoint.ChampionGenome.Connections.Should().HaveCount(3);
        checkpoint.ChampionFitness.Should().Be(0.95);
        checkpoint.ChampionGeneration.Should().Be(0);

        // Assert — Generation and Seed
        checkpoint.Generation.Should().Be(1);
        checkpoint.Seed.Should().Be(42);

        // Assert — Configuration
        checkpoint.Configuration.InputCount.Should().Be(2);
        checkpoint.Configuration.OutputCount.Should().Be(1);
        checkpoint.Configuration.PopulationSize.Should().Be(150);
        checkpoint.Configuration.Seed.Should().Be(42);
        checkpoint.Configuration.Stopping.MaxGenerations.Should().Be(100);

        // Assert — Configuration Hash
        checkpoint.ConfigurationHash.Should().Be(
            "b4bfb68dfce7a97cd0b19a2c1cb9da3f02e2c0462bd5eeb50a3e7d9773deb551");

        // Assert — Metadata
        checkpoint.Metadata.SchemaVersion.Should().Be("1.0.0");
        checkpoint.Metadata.LibraryVersion.Should().Be("1.0.0");
        checkpoint.Metadata.Seed.Should().Be(42);
        checkpoint.Metadata.ConfigurationHash.Should().Be(
            "b4bfb68dfce7a97cd0b19a2c1cb9da3f02e2c0462bd5eeb50a3e7d9773deb551");
        checkpoint.Metadata.CreatedAtUtc.Should().Be("2026-02-15T12:00:00Z");
        checkpoint.Metadata.Environment.OsDescription.Should().Be("Windows 10");
        checkpoint.Metadata.Environment.RuntimeVersion.Should().Be(".NET 8.0.0");
        checkpoint.Metadata.Environment.Architecture.Should().Be("X64");

        // Assert — RNG State
        checkpoint.RngState.Should().NotBeNull();
        checkpoint.RngState.SeedArray.Should().HaveCount(RngState.SeedArrayLength);
        checkpoint.RngState.Inext.Should().Be(0);
        checkpoint.RngState.Inextp.Should().Be(21);

        // Assert — History
        checkpoint.History.TotalGenerations.Should().Be(1);
        checkpoint.History.Generations.Should().HaveCount(1);
        var genStat = checkpoint.History.Generations[0];
        genStat.Generation.Should().Be(0);
        genStat.BestFitness.Should().Be(0.8);
        genStat.AverageFitness.Should().Be(0.5);
        genStat.SpeciesCount.Should().Be(1);
        genStat.SpeciesSizes.Should().BeEquivalentTo([2]);
        genStat.Complexity.AverageNodes.Should().Be(2.5);
        genStat.Complexity.AverageConnections.Should().Be(2.0);
    }

    [Fact]
    public void CanMigrate_CurrentVersion_ReturnsFalse()
    {
        // The current version (1.0.0) should not require migration.
        _migrator.CanMigrate(SchemaVersion.Current).Should().BeFalse();
    }

    [Fact]
    public void CanMigrate_V100_ReturnsFalse()
    {
        // v1.0.0 is the current version, so no migration is needed.
        _migrator.CanMigrate("1.0.0").Should().BeFalse();
    }

    [Fact(Skip = "No v2.0.0 schema exists yet")]
    public async Task LoadAsync_V100Fixture_MigratedToV200_AllFieldsPopulated()
    {
        // Placeholder for future v1.0.0 -> v2.0.0 migration testing.
        // When a v2.0.0 schema is introduced:
        // 1. Register a v1.0.0 -> v2.0.0 migrator in SchemaVersionMigrator
        // 2. Load the v1_0_0_checkpoint.json fixture
        // 3. Verify migration produces a valid v2.0.0 checkpoint with all fields populated
        // 4. Verify any new v2.0.0 fields have sensible defaults

        await Task.CompletedTask; // Suppress CS1998
    }

    [Fact]
    public async Task LoadAsync_V100Fixture_RoundTripsMatchOriginalCheckpoint()
    {
        // Verify that loading the fixture produces the same result as
        // creating a checkpoint programmatically and round-tripping it.
        var original = CheckpointTestHelper.CreateFullCheckpoint();

        // Serialize the original to a MemoryStream and reload
        using var roundTripStream = new MemoryStream();
        await _serializer.SaveAsync(roundTripStream, original);
        roundTripStream.Position = 0;
        var roundTripped = await _serializer.LoadAsync(roundTripStream);

        // Load the fixture
        var fixturePath = Path.Combine(
            AppContext.BaseDirectory, "Serialization", "Fixtures", "v1_0_0_checkpoint.json");
        TrainingCheckpoint fixtureLoaded;
        using (var stream = File.OpenRead(fixturePath))
        {
            fixtureLoaded = await _serializer.LoadAsync(stream);
        }

        // Both should produce equivalent results
        fixtureLoaded.Population.Should().HaveCount(roundTripped.Population.Count);
        fixtureLoaded.Species.Should().HaveCount(roundTripped.Species.Count);
        fixtureLoaded.NextInnovationNumber.Should().Be(roundTripped.NextInnovationNumber);
        fixtureLoaded.NextNodeId.Should().Be(roundTripped.NextNodeId);
        fixtureLoaded.NextSpeciesId.Should().Be(roundTripped.NextSpeciesId);
        fixtureLoaded.ChampionFitness.Should().Be(roundTripped.ChampionFitness);
        fixtureLoaded.ChampionGeneration.Should().Be(roundTripped.ChampionGeneration);
        fixtureLoaded.Generation.Should().Be(roundTripped.Generation);
        fixtureLoaded.Seed.Should().Be(roundTripped.Seed);
        fixtureLoaded.ConfigurationHash.Should().Be(roundTripped.ConfigurationHash);
        fixtureLoaded.Metadata.SchemaVersion.Should().Be(roundTripped.Metadata.SchemaVersion);
        fixtureLoaded.RngState.SeedArray.Should().BeEquivalentTo(roundTripped.RngState.SeedArray);
        fixtureLoaded.RngState.Inext.Should().Be(roundTripped.RngState.Inext);
        fixtureLoaded.RngState.Inextp.Should().Be(roundTripped.RngState.Inextp);
    }
}
