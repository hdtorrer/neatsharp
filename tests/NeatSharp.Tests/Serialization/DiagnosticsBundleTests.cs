using System.Text.Json;
using FluentAssertions;
using NeatSharp.Serialization;
using Xunit;

namespace NeatSharp.Tests.Serialization;

public class DiagnosticsBundleTests
{
    private readonly IDiagnosticsBundleCreator _creator = new DiagnosticsBundleCreator();

    // -- SchemaVersion section ------------------------------------------------

    [Fact]
    public async Task CreateAsync_BundleJsonContainsSchemaVersion()
    {
        var checkpoint = CheckpointTestHelper.CreateFullCheckpoint();

        using var stream = new MemoryStream();
        await _creator.CreateAsync(stream, checkpoint);
        stream.Position = 0;

        using var doc = await JsonDocument.ParseAsync(stream);
        var root = doc.RootElement;

        root.TryGetProperty("schemaVersion", out var schemaVersionElement).Should().BeTrue();
        schemaVersionElement.GetString().Should().Be(SchemaVersion.Current);
    }

    // -- Metadata section -----------------------------------------------------

    [Fact]
    public async Task CreateAsync_BundleJsonContainsMetadataSection()
    {
        var checkpoint = CheckpointTestHelper.CreateFullCheckpoint();

        using var stream = new MemoryStream();
        await _creator.CreateAsync(stream, checkpoint);
        stream.Position = 0;

        using var doc = await JsonDocument.ParseAsync(stream);
        var root = doc.RootElement;

        root.TryGetProperty("metadata", out var metadataElement).Should().BeTrue();
        metadataElement.ValueKind.Should().Be(JsonValueKind.Object);
    }

    [Fact]
    public async Task CreateAsync_MetadataContainsLibraryVersionSeedConfigHashCreatedAtAndEnvironment()
    {
        var checkpoint = CheckpointTestHelper.CreateFullCheckpoint();

        using var stream = new MemoryStream();
        await _creator.CreateAsync(stream, checkpoint);
        stream.Position = 0;

        using var doc = await JsonDocument.ParseAsync(stream);
        var metadata = doc.RootElement.GetProperty("metadata");

        metadata.GetProperty("libraryVersion").GetString().Should().NotBeNullOrEmpty();
        metadata.GetProperty("seed").GetInt32().Should().Be(checkpoint.Seed);
        metadata.GetProperty("configurationHash").GetString().Should().Be(checkpoint.ConfigurationHash);
        metadata.GetProperty("createdAtUtc").GetString().Should().NotBeNullOrEmpty();
        metadata.TryGetProperty("environment", out var envElement).Should().BeTrue();
        envElement.ValueKind.Should().Be(JsonValueKind.Object);
    }

    // -- Checkpoint section ---------------------------------------------------

    [Fact]
    public async Task CreateAsync_BundleJsonContainsCheckpointSection()
    {
        var checkpoint = CheckpointTestHelper.CreateFullCheckpoint();

        using var stream = new MemoryStream();
        await _creator.CreateAsync(stream, checkpoint);
        stream.Position = 0;

        using var doc = await JsonDocument.ParseAsync(stream);
        var root = doc.RootElement;

        root.TryGetProperty("checkpoint", out var checkpointElement).Should().BeTrue();
        checkpointElement.ValueKind.Should().Be(JsonValueKind.Object);
    }

    [Fact]
    public async Task CreateAsync_CheckpointSectionContainsPopulationAndSpecies()
    {
        var checkpoint = CheckpointTestHelper.CreateFullCheckpoint();

        using var stream = new MemoryStream();
        await _creator.CreateAsync(stream, checkpoint);
        stream.Position = 0;

        using var doc = await JsonDocument.ParseAsync(stream);
        var checkpointSection = doc.RootElement.GetProperty("checkpoint");

        checkpointSection.TryGetProperty("population", out var populationElement).Should().BeTrue();
        populationElement.ValueKind.Should().Be(JsonValueKind.Array);
        populationElement.GetArrayLength().Should().Be(checkpoint.Population.Count);

        checkpointSection.TryGetProperty("species", out var speciesElement).Should().BeTrue();
        speciesElement.ValueKind.Should().Be(JsonValueKind.Array);
        speciesElement.GetArrayLength().Should().Be(checkpoint.Species.Count);
    }

    [Fact]
    public async Task CreateAsync_CheckpointSectionContainsSchemaVersion()
    {
        var checkpoint = CheckpointTestHelper.CreateFullCheckpoint();

        using var stream = new MemoryStream();
        await _creator.CreateAsync(stream, checkpoint);
        stream.Position = 0;

        using var doc = await JsonDocument.ParseAsync(stream);
        var checkpointSection = doc.RootElement.GetProperty("checkpoint");

        checkpointSection.GetProperty("schemaVersion").GetString()
            .Should().Be(SchemaVersion.Current);
    }

    // -- Configuration section ------------------------------------------------

    [Fact]
    public async Task CreateAsync_BundleJsonContainsConfigurationSection()
    {
        var checkpoint = CheckpointTestHelper.CreateFullCheckpoint();

        using var stream = new MemoryStream();
        await _creator.CreateAsync(stream, checkpoint);
        stream.Position = 0;

        using var doc = await JsonDocument.ParseAsync(stream);
        var root = doc.RootElement;

        root.TryGetProperty("configuration", out var configElement).Should().BeTrue();
        configElement.ValueKind.Should().Be(JsonValueKind.Object);

        // Verify key configuration properties are present
        configElement.GetProperty("inputCount").GetInt32().Should().Be(checkpoint.Configuration.InputCount);
        configElement.GetProperty("outputCount").GetInt32().Should().Be(checkpoint.Configuration.OutputCount);
        configElement.GetProperty("populationSize").GetInt32().Should().Be(checkpoint.Configuration.PopulationSize);
        configElement.GetProperty("seed").GetInt32().Should().Be(checkpoint.Configuration.Seed);
    }

    // -- Environment section --------------------------------------------------

    [Fact]
    public async Task CreateAsync_BundleJsonContainsEnvironmentSection()
    {
        var checkpoint = CheckpointTestHelper.CreateFullCheckpoint();

        using var stream = new MemoryStream();
        await _creator.CreateAsync(stream, checkpoint);
        stream.Position = 0;

        using var doc = await JsonDocument.ParseAsync(stream);
        var root = doc.RootElement;

        root.TryGetProperty("environment", out var envElement).Should().BeTrue();
        envElement.ValueKind.Should().Be(JsonValueKind.Object);
    }

    [Fact]
    public async Task CreateAsync_EnvironmentContainsOsDescriptionRuntimeVersionArchitecture()
    {
        var checkpoint = CheckpointTestHelper.CreateFullCheckpoint();

        using var stream = new MemoryStream();
        await _creator.CreateAsync(stream, checkpoint);
        stream.Position = 0;

        using var doc = await JsonDocument.ParseAsync(stream);
        var env = doc.RootElement.GetProperty("environment");

        env.TryGetProperty("osDescription", out var osElement).Should().BeTrue();
        osElement.GetString().Should().NotBeNullOrEmpty();

        env.TryGetProperty("runtimeVersion", out var runtimeElement).Should().BeTrue();
        runtimeElement.GetString().Should().NotBeNullOrEmpty();

        env.TryGetProperty("architecture", out var archElement).Should().BeTrue();
        archElement.GetString().Should().NotBeNullOrEmpty();
    }

    // -- History section ------------------------------------------------------

    [Fact]
    public async Task CreateAsync_BundleJsonContainsHistorySection()
    {
        var checkpoint = CheckpointTestHelper.CreateFullCheckpoint();

        using var stream = new MemoryStream();
        await _creator.CreateAsync(stream, checkpoint);
        stream.Position = 0;

        using var doc = await JsonDocument.ParseAsync(stream);
        var root = doc.RootElement;

        root.TryGetProperty("history", out var historyElement).Should().BeTrue();
        historyElement.ValueKind.Should().Be(JsonValueKind.Object);

        historyElement.TryGetProperty("generations", out var generationsElement).Should().BeTrue();
        generationsElement.ValueKind.Should().Be(JsonValueKind.Array);
        generationsElement.GetArrayLength().Should().Be(checkpoint.History.Generations.Count);

        historyElement.GetProperty("totalGenerations").GetInt32()
            .Should().Be(checkpoint.History.TotalGenerations);
    }

    // -- Single CreateAsync call produces complete bundle ----------------------

    [Fact]
    public async Task CreateAsync_SingleCallProducesCompleteBundleWithAllSections()
    {
        var checkpoint = CheckpointTestHelper.CreateFullCheckpoint();

        using var stream = new MemoryStream();
        await _creator.CreateAsync(stream, checkpoint);
        stream.Position = 0;

        using var doc = await JsonDocument.ParseAsync(stream);
        var root = doc.RootElement;

        // All top-level sections must be present from a single call
        root.TryGetProperty("schemaVersion", out _).Should().BeTrue("bundle should contain schemaVersion");
        root.TryGetProperty("metadata", out _).Should().BeTrue("bundle should contain metadata");
        root.TryGetProperty("checkpoint", out _).Should().BeTrue("bundle should contain checkpoint");
        root.TryGetProperty("configuration", out _).Should().BeTrue("bundle should contain configuration");
        root.TryGetProperty("environment", out _).Should().BeTrue("bundle should contain environment");
        root.TryGetProperty("history", out _).Should().BeTrue("bundle should contain history");
    }

    // -- Parseable with raw JsonDocument (no NEATSharp types) ------------------

    [Fact]
    public async Task CreateAsync_ParseableWithRawJsonDocumentWithoutNeatSharpTypes()
    {
        var checkpoint = CheckpointTestHelper.CreateFullCheckpoint();

        using var stream = new MemoryStream();
        await _creator.CreateAsync(stream, checkpoint);
        stream.Position = 0;

        // Parse with raw JsonDocument only — no NEATSharp DTO types used
        using var doc = await JsonDocument.ParseAsync(stream);
        var root = doc.RootElement;

        // Verify basic structure is traversable with standard JSON APIs
        var schemaVersion = root.GetProperty("schemaVersion").GetString();
        schemaVersion.Should().NotBeNullOrEmpty();

        var seed = root.GetProperty("metadata").GetProperty("seed").GetInt32();
        seed.Should().Be(checkpoint.Seed);

        var configInputCount = root.GetProperty("configuration").GetProperty("inputCount").GetInt32();
        configInputCount.Should().Be(checkpoint.Configuration.InputCount);

        var osDesc = root.GetProperty("environment").GetProperty("osDescription").GetString();
        osDesc.Should().NotBeNullOrEmpty();

        var totalGens = root.GetProperty("history").GetProperty("totalGenerations").GetInt32();
        totalGens.Should().Be(checkpoint.History.TotalGenerations);

        var checkpointSection = root.GetProperty("checkpoint");
        var populationCount = checkpointSection.GetProperty("population").GetArrayLength();
        populationCount.Should().Be(checkpoint.Population.Count);
    }

    // -- Null argument tests --------------------------------------------------

    [Fact]
    public async Task CreateAsync_NullStream_ThrowsArgumentNullException()
    {
        var checkpoint = CheckpointTestHelper.CreateFullCheckpoint();

        var act = async () => await _creator.CreateAsync(null!, checkpoint);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task CreateAsync_NullCheckpoint_ThrowsArgumentNullException()
    {
        using var stream = new MemoryStream();

        var act = async () => await _creator.CreateAsync(stream, null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
