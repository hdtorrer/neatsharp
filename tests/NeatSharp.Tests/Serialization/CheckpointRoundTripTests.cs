using System.Text;
using System.Text.Json;
using FluentAssertions;
using NeatSharp.Exceptions;
using NeatSharp.Serialization;
using NeatSharp.Serialization.Migration;
using Xunit;

namespace NeatSharp.Tests.Serialization;

public class CheckpointRoundTripTests
{
    private readonly ICheckpointSerializer _serializer;

    public CheckpointRoundTripTests()
    {
        var validator = new CheckpointValidator();
        var migrator = new SchemaVersionMigrator();
        _serializer = new CheckpointSerializer(validator, migrator);
    }

    [Fact]
    public async Task SaveAndLoad_FullCheckpoint_PopulationGenomeCountMatches()
    {
        var original = CheckpointTestHelper.CreateFullCheckpoint();

        using var stream = new MemoryStream();
        await _serializer.SaveAsync(stream, original);
        stream.Position = 0;
        var restored = await _serializer.LoadAsync(stream);

        restored.Population.Should().HaveCount(original.Population.Count);
    }

    [Fact]
    public async Task SaveAndLoad_FullCheckpoint_GenomeNodesAndConnectionsMatch()
    {
        var original = CheckpointTestHelper.CreateFullCheckpoint();

        using var stream = new MemoryStream();
        await _serializer.SaveAsync(stream, original);
        stream.Position = 0;
        var restored = await _serializer.LoadAsync(stream);

        for (int i = 0; i < original.Population.Count; i++)
        {
            var origGenome = original.Population[i];
            var restoredGenome = restored.Population[i];

            restoredGenome.Nodes.Should().HaveCount(origGenome.Nodes.Count);
            restoredGenome.Connections.Should().HaveCount(origGenome.Connections.Count);

            for (int n = 0; n < origGenome.Nodes.Count; n++)
            {
                restoredGenome.Nodes[n].Id.Should().Be(origGenome.Nodes[n].Id);
                restoredGenome.Nodes[n].Type.Should().Be(origGenome.Nodes[n].Type);
                restoredGenome.Nodes[n].ActivationFunction.Should().Be(origGenome.Nodes[n].ActivationFunction);
            }

            for (int c = 0; c < origGenome.Connections.Count; c++)
            {
                restoredGenome.Connections[c].InnovationNumber.Should().Be(origGenome.Connections[c].InnovationNumber);
                restoredGenome.Connections[c].SourceNodeId.Should().Be(origGenome.Connections[c].SourceNodeId);
                restoredGenome.Connections[c].TargetNodeId.Should().Be(origGenome.Connections[c].TargetNodeId);
                restoredGenome.Connections[c].Weight.Should().Be(origGenome.Connections[c].Weight);
                restoredGenome.Connections[c].IsEnabled.Should().Be(origGenome.Connections[c].IsEnabled);
            }
        }
    }

    [Fact]
    public async Task SaveAndLoad_FullCheckpoint_SpeciesMetadataMatches()
    {
        var original = CheckpointTestHelper.CreateFullCheckpoint();

        using var stream = new MemoryStream();
        await _serializer.SaveAsync(stream, original);
        stream.Position = 0;
        var restored = await _serializer.LoadAsync(stream);

        restored.Species.Should().HaveCount(original.Species.Count);

        for (int i = 0; i < original.Species.Count; i++)
        {
            var origSpecies = original.Species[i];
            var restoredSpecies = restored.Species[i];

            restoredSpecies.Id.Should().Be(origSpecies.Id);
            restoredSpecies.RepresentativeIndex.Should().Be(origSpecies.RepresentativeIndex);
            restoredSpecies.BestFitnessEver.Should().Be(origSpecies.BestFitnessEver);
            restoredSpecies.GenerationsSinceImprovement.Should().Be(origSpecies.GenerationsSinceImprovement);
            restoredSpecies.MemberIndices.Should().BeEquivalentTo(origSpecies.MemberIndices);
            restoredSpecies.MemberFitnesses.Should().BeEquivalentTo(origSpecies.MemberFitnesses);
        }
    }

    [Fact]
    public async Task SaveAndLoad_FullCheckpoint_InnovationCountersMatch()
    {
        var original = CheckpointTestHelper.CreateFullCheckpoint();

        using var stream = new MemoryStream();
        await _serializer.SaveAsync(stream, original);
        stream.Position = 0;
        var restored = await _serializer.LoadAsync(stream);

        restored.NextInnovationNumber.Should().Be(original.NextInnovationNumber);
        restored.NextNodeId.Should().Be(original.NextNodeId);
        restored.NextSpeciesId.Should().Be(original.NextSpeciesId);
    }

    [Fact]
    public async Task SaveAndLoad_FullCheckpoint_ChampionGenomeFitnessAndGenerationMatch()
    {
        var original = CheckpointTestHelper.CreateFullCheckpoint();

        using var stream = new MemoryStream();
        await _serializer.SaveAsync(stream, original);
        stream.Position = 0;
        var restored = await _serializer.LoadAsync(stream);

        restored.ChampionFitness.Should().Be(original.ChampionFitness);
        restored.ChampionGeneration.Should().Be(original.ChampionGeneration);

        // Champion genome structural equality
        restored.ChampionGenome.Nodes.Should().HaveCount(original.ChampionGenome.Nodes.Count);
        restored.ChampionGenome.Connections.Should().HaveCount(original.ChampionGenome.Connections.Count);

        for (int n = 0; n < original.ChampionGenome.Nodes.Count; n++)
        {
            restored.ChampionGenome.Nodes[n].Id.Should().Be(original.ChampionGenome.Nodes[n].Id);
            restored.ChampionGenome.Nodes[n].Type.Should().Be(original.ChampionGenome.Nodes[n].Type);
        }

        for (int c = 0; c < original.ChampionGenome.Connections.Count; c++)
        {
            restored.ChampionGenome.Connections[c].InnovationNumber.Should().Be(original.ChampionGenome.Connections[c].InnovationNumber);
            restored.ChampionGenome.Connections[c].Weight.Should().Be(original.ChampionGenome.Connections[c].Weight);
        }
    }

    [Fact]
    public async Task SaveAndLoad_FullCheckpoint_ConfigurationMatches()
    {
        var original = CheckpointTestHelper.CreateFullCheckpoint();

        using var stream = new MemoryStream();
        await _serializer.SaveAsync(stream, original);
        stream.Position = 0;
        var restored = await _serializer.LoadAsync(stream);

        restored.Configuration.InputCount.Should().Be(original.Configuration.InputCount);
        restored.Configuration.OutputCount.Should().Be(original.Configuration.OutputCount);
        restored.Configuration.PopulationSize.Should().Be(original.Configuration.PopulationSize);
        restored.Configuration.Seed.Should().Be(original.Configuration.Seed);
        restored.Configuration.Stopping.MaxGenerations.Should().Be(original.Configuration.Stopping.MaxGenerations);
        restored.ConfigurationHash.Should().Be(original.ConfigurationHash);
    }

    [Fact]
    public async Task SaveAndLoad_FullCheckpoint_SeedAndGenerationMatch()
    {
        var original = CheckpointTestHelper.CreateFullCheckpoint();

        using var stream = new MemoryStream();
        await _serializer.SaveAsync(stream, original);
        stream.Position = 0;
        var restored = await _serializer.LoadAsync(stream);

        restored.Seed.Should().Be(original.Seed);
        restored.Generation.Should().Be(original.Generation);
    }

    [Fact]
    public async Task SaveAndLoad_FullCheckpoint_RngStateMatches()
    {
        var original = CheckpointTestHelper.CreateFullCheckpoint();

        using var stream = new MemoryStream();
        await _serializer.SaveAsync(stream, original);
        stream.Position = 0;
        var restored = await _serializer.LoadAsync(stream);

        restored.RngState.SeedArray.Should().BeEquivalentTo(original.RngState.SeedArray);
        restored.RngState.Inext.Should().Be(original.RngState.Inext);
        restored.RngState.Inextp.Should().Be(original.RngState.Inextp);
    }

    [Fact]
    public async Task SaveAndLoad_FullCheckpoint_HistoryMatches()
    {
        var original = CheckpointTestHelper.CreateFullCheckpoint();

        using var stream = new MemoryStream();
        await _serializer.SaveAsync(stream, original);
        stream.Position = 0;
        var restored = await _serializer.LoadAsync(stream);

        restored.History.TotalGenerations.Should().Be(original.History.TotalGenerations);
        restored.History.Generations.Should().HaveCount(original.History.Generations.Count);

        for (int i = 0; i < original.History.Generations.Count; i++)
        {
            var origGen = original.History.Generations[i];
            var restoredGen = restored.History.Generations[i];

            restoredGen.Generation.Should().Be(origGen.Generation);
            restoredGen.BestFitness.Should().Be(origGen.BestFitness);
            restoredGen.AverageFitness.Should().Be(origGen.AverageFitness);
            restoredGen.SpeciesCount.Should().Be(origGen.SpeciesCount);
            restoredGen.SpeciesSizes.Should().BeEquivalentTo(origGen.SpeciesSizes);
            restoredGen.Complexity.AverageNodes.Should().Be(origGen.Complexity.AverageNodes);
            restoredGen.Complexity.AverageConnections.Should().Be(origGen.Complexity.AverageConnections);
        }
    }

    [Fact]
    public async Task SaveAndLoad_FullCheckpoint_MetadataMatches()
    {
        var original = CheckpointTestHelper.CreateFullCheckpoint();

        using var stream = new MemoryStream();
        await _serializer.SaveAsync(stream, original);
        stream.Position = 0;
        var restored = await _serializer.LoadAsync(stream);

        restored.Metadata.SchemaVersion.Should().Be(original.Metadata.SchemaVersion);
        restored.Metadata.LibraryVersion.Should().Be(original.Metadata.LibraryVersion);
        restored.Metadata.Seed.Should().Be(original.Metadata.Seed);
        restored.Metadata.ConfigurationHash.Should().Be(original.Metadata.ConfigurationHash);
        restored.Metadata.CreatedAtUtc.Should().Be(original.Metadata.CreatedAtUtc);
        restored.Metadata.Environment.OsDescription.Should().Be(original.Metadata.Environment.OsDescription);
        restored.Metadata.Environment.RuntimeVersion.Should().Be(original.Metadata.Environment.RuntimeVersion);
        restored.Metadata.Environment.Architecture.Should().Be(original.Metadata.Environment.Architecture);
    }

    [Fact]
    public async Task SaveAndLoad_StreamBasedIO_NoFilesystemDependency()
    {
        var original = CheckpointTestHelper.CreateFullCheckpoint();

        // Use MemoryStream to prove stream-based I/O works without filesystem
        using var stream = new MemoryStream();
        await _serializer.SaveAsync(stream, original);

        stream.Length.Should().BeGreaterThan(0);
        stream.Position = 0;

        var restored = await _serializer.LoadAsync(stream);

        restored.Generation.Should().Be(original.Generation);
        restored.Seed.Should().Be(original.Seed);
    }

    [Fact]
    public async Task SaveAndLoad_EmptyPopulation_SaveSucceeds()
    {
        var original = CheckpointTestHelper.CreateEmptyPopulationCheckpoint();

        using var stream = new MemoryStream();

        var act = async () => await _serializer.SaveAsync(stream, original);

        await act.Should().NotThrowAsync();
        stream.Length.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task SaveAndLoad_EmptyPopulation_LoadSucceeds()
    {
        var original = CheckpointTestHelper.CreateEmptyPopulationCheckpoint();

        using var stream = new MemoryStream();
        await _serializer.SaveAsync(stream, original);
        stream.Position = 0;

        var act = async () => await _serializer.LoadAsync(stream);

        var restored = await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task SaveAndLoad_EmptyPopulation_RestoredStateHasEmptyPopulationAndSpecies()
    {
        var original = CheckpointTestHelper.CreateEmptyPopulationCheckpoint();

        using var stream = new MemoryStream();
        await _serializer.SaveAsync(stream, original);
        stream.Position = 0;

        var restored = await _serializer.LoadAsync(stream);

        restored.Population.Should().BeEmpty();
        restored.Species.Should().BeEmpty();
        restored.Generation.Should().Be(original.Generation);
        restored.Seed.Should().Be(original.Seed);
        restored.ChampionFitness.Should().Be(original.ChampionFitness);
    }

    [Fact]
    public async Task SaveAndLoad_CancellationToken_CanBePassed()
    {
        var original = CheckpointTestHelper.CreateFullCheckpoint();
        using var cts = new CancellationTokenSource();

        using var stream = new MemoryStream();
        await _serializer.SaveAsync(stream, original, cts.Token);
        stream.Position = 0;
        var restored = await _serializer.LoadAsync(stream, cts.Token);

        restored.Generation.Should().Be(original.Generation);
    }

    [Fact]
    public async Task LoadAsync_MatchingSchemaVersion_LoadsSuccessfully()
    {
        var original = CheckpointTestHelper.CreateFullCheckpoint();

        using var stream = new MemoryStream();
        await _serializer.SaveAsync(stream, original);
        stream.Position = 0;

        var act = async () => await _serializer.LoadAsync(stream);

        var restored = await act.Should().NotThrowAsync();
        restored.Subject.Generation.Should().Be(original.Generation);
        restored.Subject.Seed.Should().Be(original.Seed);
    }

    [Fact]
    public async Task LoadAsync_NewerSchemaVersion_ThrowsSchemaVersionException()
    {
        var original = CheckpointTestHelper.CreateFullCheckpoint();

        using var stream = new MemoryStream();
        await _serializer.SaveAsync(stream, original);

        // Read the serialized JSON and modify the schemaVersion to a newer version
        var json = Encoding.UTF8.GetString(stream.ToArray());
        var modifiedJson = json.Replace(
            $"\"schemaVersion\": \"{SchemaVersion.Current}\"",
            "\"schemaVersion\": \"2.0.0\"");

        using var modifiedStream = new MemoryStream(Encoding.UTF8.GetBytes(modifiedJson));

        var act = async () => await _serializer.LoadAsync(modifiedStream);

        var exception = await act.Should().ThrowAsync<SchemaVersionException>();
        exception.Which.ArtifactVersion.Should().Be("2.0.0");
        exception.Which.ExpectedVersion.Should().Be(SchemaVersion.Current);
        exception.Which.IsMigrationAvailable.Should().BeFalse();
    }

    [Fact]
    public async Task LoadAsync_UnsupportedOlderSchemaVersion_ThrowsSchemaVersionException()
    {
        var original = CheckpointTestHelper.CreateFullCheckpoint();

        using var stream = new MemoryStream();
        await _serializer.SaveAsync(stream, original);

        // Read the serialized JSON and modify the schemaVersion to an older unsupported version
        var json = Encoding.UTF8.GetString(stream.ToArray());
        var modifiedJson = json.Replace(
            $"\"schemaVersion\": \"{SchemaVersion.Current}\"",
            "\"schemaVersion\": \"0.5.0\"");

        using var modifiedStream = new MemoryStream(Encoding.UTF8.GetBytes(modifiedJson));

        var act = async () => await _serializer.LoadAsync(modifiedStream);

        var exception = await act.Should().ThrowAsync<SchemaVersionException>();
        exception.Which.ArtifactVersion.Should().Be("0.5.0");
        exception.Which.ExpectedVersion.Should().Be(SchemaVersion.Current);
        exception.Which.IsMigrationAvailable.Should().BeFalse();
    }

    [Fact]
    public async Task SaveAsync_NewlyCreatedArtifact_ContainsCorrectVersionInfo()
    {
        var original = CheckpointTestHelper.CreateFullCheckpoint();

        using var stream = new MemoryStream();
        await _serializer.SaveAsync(stream, original);

        // Parse the raw JSON to verify version fields
        var json = Encoding.UTF8.GetString(stream.ToArray());
        using var jsonDoc = JsonDocument.Parse(json);
        var root = jsonDoc.RootElement;

        root.GetProperty("schemaVersion").GetString().Should().Be("1.0.0");

        var metadata = root.GetProperty("metadata");
        metadata.GetProperty("schemaVersion").GetString().Should().Be("1.0.0");
        metadata.GetProperty("libraryVersion").GetString().Should().NotBeNullOrEmpty();
    }
}
