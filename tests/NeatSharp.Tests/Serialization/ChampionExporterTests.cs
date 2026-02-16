using System.Text.Json;
using FluentAssertions;
using NeatSharp.Evolution;
using NeatSharp.Genetics;
using NeatSharp.Reporting;
using NeatSharp.Serialization;
using Xunit;

namespace NeatSharp.Tests.Serialization;

public class ChampionExporterTests
{
    private readonly IChampionExporter _exporter = new ChampionExporter();

    // ── Export from TrainingCheckpoint ──────────────────────────────────

    [Fact]
    public async Task ExportAsync_TrainingCheckpoint_JsonContainsNodesArray()
    {
        var checkpoint = CheckpointTestHelper.CreateFullCheckpoint();

        using var stream = new MemoryStream();
        await _exporter.ExportAsync(stream, checkpoint);
        stream.Position = 0;

        using var doc = await JsonDocument.ParseAsync(stream);
        var root = doc.RootElement;

        root.TryGetProperty("nodes", out var nodesElement).Should().BeTrue();
        nodesElement.ValueKind.Should().Be(JsonValueKind.Array);
        nodesElement.GetArrayLength().Should().Be(checkpoint.ChampionGenome.Nodes.Count);
    }

    [Fact]
    public async Task ExportAsync_TrainingCheckpoint_NodesContainIdTypeActivationFunction()
    {
        var checkpoint = CheckpointTestHelper.CreateFullCheckpoint();

        using var stream = new MemoryStream();
        await _exporter.ExportAsync(stream, checkpoint);
        stream.Position = 0;

        using var doc = await JsonDocument.ParseAsync(stream);
        var nodes = doc.RootElement.GetProperty("nodes");

        foreach (var node in nodes.EnumerateArray())
        {
            node.TryGetProperty("id", out _).Should().BeTrue();
            node.TryGetProperty("type", out _).Should().BeTrue();
            node.TryGetProperty("activationFunction", out _).Should().BeTrue();
        }
    }

    [Fact]
    public async Task ExportAsync_TrainingCheckpoint_NodeTypesAreLowercaseStrings()
    {
        var checkpoint = CheckpointTestHelper.CreateFullCheckpoint();

        using var stream = new MemoryStream();
        await _exporter.ExportAsync(stream, checkpoint);
        stream.Position = 0;

        using var doc = await JsonDocument.ParseAsync(stream);
        var nodes = doc.RootElement.GetProperty("nodes");

        var validTypes = new HashSet<string> { "input", "hidden", "output", "bias" };
        foreach (var node in nodes.EnumerateArray())
        {
            var nodeType = node.GetProperty("type").GetString();
            nodeType.Should().NotBeNullOrEmpty();
            validTypes.Should().Contain(nodeType!);
        }
    }

    [Fact]
    public async Task ExportAsync_TrainingCheckpoint_JsonContainsEdgesArray()
    {
        var checkpoint = CheckpointTestHelper.CreateFullCheckpoint();

        using var stream = new MemoryStream();
        await _exporter.ExportAsync(stream, checkpoint);
        stream.Position = 0;

        using var doc = await JsonDocument.ParseAsync(stream);
        var root = doc.RootElement;

        root.TryGetProperty("edges", out var edgesElement).Should().BeTrue();
        edgesElement.ValueKind.Should().Be(JsonValueKind.Array);
        edgesElement.GetArrayLength().Should().Be(checkpoint.ChampionGenome.Connections.Count);
    }

    [Fact]
    public async Task ExportAsync_TrainingCheckpoint_EdgesContainSourceTargetWeightEnabled()
    {
        var checkpoint = CheckpointTestHelper.CreateFullCheckpoint();

        using var stream = new MemoryStream();
        await _exporter.ExportAsync(stream, checkpoint);
        stream.Position = 0;

        using var doc = await JsonDocument.ParseAsync(stream);
        var edges = doc.RootElement.GetProperty("edges");

        foreach (var edge in edges.EnumerateArray())
        {
            edge.TryGetProperty("source", out _).Should().BeTrue();
            edge.TryGetProperty("target", out _).Should().BeTrue();
            edge.TryGetProperty("weight", out _).Should().BeTrue();
            edge.TryGetProperty("enabled", out _).Should().BeTrue();
        }
    }

    [Fact]
    public async Task ExportAsync_TrainingCheckpoint_MetadataContainsFitnessAndGeneration()
    {
        var checkpoint = CheckpointTestHelper.CreateFullCheckpoint();

        using var stream = new MemoryStream();
        await _exporter.ExportAsync(stream, checkpoint);
        stream.Position = 0;

        using var doc = await JsonDocument.ParseAsync(stream);
        var champion = doc.RootElement.GetProperty("champion");

        champion.GetProperty("fitness").GetDouble().Should().Be(checkpoint.ChampionFitness);
        champion.GetProperty("generationFound").GetInt32().Should().Be(checkpoint.ChampionGeneration);
    }

    [Fact]
    public async Task ExportAsync_TrainingCheckpoint_MetadataContainsSchemaAndLibraryVersion()
    {
        var checkpoint = CheckpointTestHelper.CreateFullCheckpoint();

        using var stream = new MemoryStream();
        await _exporter.ExportAsync(stream, checkpoint);
        stream.Position = 0;

        using var doc = await JsonDocument.ParseAsync(stream);
        var root = doc.RootElement;

        root.GetProperty("schemaVersion").GetString().Should().Be(SchemaVersion.Current);

        var metadata = root.GetProperty("metadata");
        metadata.GetProperty("schemaVersion").GetString().Should().Be(SchemaVersion.Current);
        metadata.GetProperty("libraryVersion").GetString().Should().NotBeNullOrEmpty();
        metadata.GetProperty("seed").GetInt32().Should().Be(checkpoint.Seed);
    }

    [Fact]
    public async Task ExportAsync_TrainingCheckpoint_MetadataContainsConfigurationHash()
    {
        var checkpoint = CheckpointTestHelper.CreateFullCheckpoint();

        using var stream = new MemoryStream();
        await _exporter.ExportAsync(stream, checkpoint);
        stream.Position = 0;

        using var doc = await JsonDocument.ParseAsync(stream);
        var metadata = doc.RootElement.GetProperty("metadata");

        metadata.GetProperty("configurationHash").GetString()
            .Should().Be(checkpoint.ConfigurationHash);
    }

    [Fact]
    public async Task ExportAsync_TrainingCheckpoint_MetadataContainsEnvironmentInfo()
    {
        var checkpoint = CheckpointTestHelper.CreateFullCheckpoint();

        using var stream = new MemoryStream();
        await _exporter.ExportAsync(stream, checkpoint);
        stream.Position = 0;

        using var doc = await JsonDocument.ParseAsync(stream);
        var env = doc.RootElement.GetProperty("metadata").GetProperty("environment");

        env.TryGetProperty("osDescription", out _).Should().BeTrue();
        env.TryGetProperty("runtimeVersion", out _).Should().BeTrue();
        env.TryGetProperty("architecture", out _).Should().BeTrue();
    }

    // ── Parse with raw JsonDocument (independence test) ────────────────

    [Fact]
    public async Task ExportAsync_TrainingCheckpoint_ParseableWithRawJsonDocumentWithoutNeatSharpTypes()
    {
        var checkpoint = CheckpointTestHelper.CreateFullCheckpoint();

        using var stream = new MemoryStream();
        await _exporter.ExportAsync(stream, checkpoint);
        stream.Position = 0;

        // Parse with raw JsonDocument — no NEATSharp DTO types used
        using var doc = await JsonDocument.ParseAsync(stream);
        var root = doc.RootElement;

        // Verify basic structure is traversable with standard JSON APIs only
        var schemaVersion = root.GetProperty("schemaVersion").GetString();
        schemaVersion.Should().NotBeNullOrEmpty();

        var nodes = root.GetProperty("nodes");
        var firstNode = nodes[0];
        var nodeId = firstNode.GetProperty("id").GetInt32();
        var nodeType = firstNode.GetProperty("type").GetString();
        var activationFn = firstNode.GetProperty("activationFunction").GetString();

        nodeId.Should().BeGreaterThanOrEqualTo(0);
        nodeType.Should().NotBeNullOrEmpty();
        activationFn.Should().NotBeNullOrEmpty();

        var edges = root.GetProperty("edges");
        var firstEdge = edges[0];
        var source = firstEdge.GetProperty("source").GetInt32();
        var target = firstEdge.GetProperty("target").GetInt32();
        var weight = firstEdge.GetProperty("weight").GetDouble();
        var enabled = firstEdge.GetProperty("enabled").GetBoolean();

        source.Should().BeGreaterThanOrEqualTo(0);
        target.Should().BeGreaterThanOrEqualTo(0);
        weight.Should().NotBe(double.NaN);
        enabled.Should().BeTrue(); // first connection of the test genome is enabled
    }

    // ── Export from EvolutionResult ────────────────────────────────────

    [Fact]
    public async Task ExportAsync_EvolutionResult_WithGenotype_ProducesValidJson()
    {
        var genome = CheckpointTestHelper.CreateGenomeWithHiddenNode();
        var phenotype = new FakeGenome(genome.Nodes.Count, genome.Connections.Count);
        var champion = new Champion(phenotype, 3.98, 12, Genotype: genome);
        var result = new EvolutionResult(
            champion,
            new PopulationSnapshot([], 0),
            new RunHistory([], 1),
            Seed: 42,
            WasCancelled: false);

        using var stream = new MemoryStream();
        await _exporter.ExportAsync(stream, result);
        stream.Position = 0;

        using var doc = await JsonDocument.ParseAsync(stream);
        var root = doc.RootElement;

        root.GetProperty("nodes").GetArrayLength().Should().Be(genome.Nodes.Count);
        root.GetProperty("edges").GetArrayLength().Should().Be(genome.Connections.Count);
        root.GetProperty("champion").GetProperty("fitness").GetDouble().Should().Be(3.98);
        root.GetProperty("champion").GetProperty("generationFound").GetInt32().Should().Be(12);
    }

    [Fact]
    public async Task ExportAsync_EvolutionResult_WithGenotype_GraphMatchesOriginalGenome()
    {
        var genome = CheckpointTestHelper.CreateGenomeWithHiddenNode();
        var phenotype = new FakeGenome(genome.Nodes.Count, genome.Connections.Count);
        var champion = new Champion(phenotype, 2.5, 5, Genotype: genome);
        var result = new EvolutionResult(
            champion,
            new PopulationSnapshot([], 0),
            new RunHistory([], 1),
            Seed: 99,
            WasCancelled: false);

        using var stream = new MemoryStream();
        await _exporter.ExportAsync(stream, result);
        stream.Position = 0;

        using var doc = await JsonDocument.ParseAsync(stream);
        var nodes = doc.RootElement.GetProperty("nodes");
        var edges = doc.RootElement.GetProperty("edges");

        // Verify node count and values match
        nodes.GetArrayLength().Should().Be(genome.Nodes.Count);
        for (int i = 0; i < genome.Nodes.Count; i++)
        {
            var node = nodes[i];
            node.GetProperty("id").GetInt32().Should().Be(genome.Nodes[i].Id);
            node.GetProperty("type").GetString().Should().Be(genome.Nodes[i].Type.ToString().ToLowerInvariant());
            node.GetProperty("activationFunction").GetString().Should().Be(genome.Nodes[i].ActivationFunction);
        }

        // Verify edge count and values match
        edges.GetArrayLength().Should().Be(genome.Connections.Count);
        for (int i = 0; i < genome.Connections.Count; i++)
        {
            var edge = edges[i];
            edge.GetProperty("source").GetInt32().Should().Be(genome.Connections[i].SourceNodeId);
            edge.GetProperty("target").GetInt32().Should().Be(genome.Connections[i].TargetNodeId);
            edge.GetProperty("weight").GetDouble().Should().Be(genome.Connections[i].Weight);
            edge.GetProperty("enabled").GetBoolean().Should().Be(genome.Connections[i].IsEnabled);
        }
    }

    [Fact]
    public async Task ExportAsync_EvolutionResult_WithoutGenotype_ThrowsInvalidOperationException()
    {
        var phenotype = new FakeGenome(2, 1);
        var champion = new Champion(phenotype, 1.0, 0); // No Genotype
        var result = new EvolutionResult(
            champion,
            new PopulationSnapshot([], 0),
            new RunHistory([], 1),
            Seed: 42,
            WasCancelled: false);

        using var stream = new MemoryStream();

        var act = async () => await _exporter.ExportAsync(stream, result);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*genotype*");
    }

    // ── Verify graph structure matches original genome ─────────────────

    [Fact]
    public async Task ExportAsync_TrainingCheckpoint_GraphStructureMatchesOriginalGenome()
    {
        var checkpoint = CheckpointTestHelper.CreateFullCheckpoint();

        using var stream = new MemoryStream();
        await _exporter.ExportAsync(stream, checkpoint);
        stream.Position = 0;

        using var doc = await JsonDocument.ParseAsync(stream);
        var nodes = doc.RootElement.GetProperty("nodes");
        var edges = doc.RootElement.GetProperty("edges");

        var championGenome = checkpoint.ChampionGenome;

        // Verify every node matches
        nodes.GetArrayLength().Should().Be(championGenome.Nodes.Count);
        for (int i = 0; i < championGenome.Nodes.Count; i++)
        {
            var node = nodes[i];
            node.GetProperty("id").GetInt32().Should().Be(championGenome.Nodes[i].Id);
            node.GetProperty("type").GetString()
                .Should().Be(championGenome.Nodes[i].Type.ToString().ToLowerInvariant());
            node.GetProperty("activationFunction").GetString()
                .Should().Be(championGenome.Nodes[i].ActivationFunction);
        }

        // Verify every edge matches
        edges.GetArrayLength().Should().Be(championGenome.Connections.Count);
        for (int i = 0; i < championGenome.Connections.Count; i++)
        {
            var edge = edges[i];
            edge.GetProperty("source").GetInt32().Should().Be(championGenome.Connections[i].SourceNodeId);
            edge.GetProperty("target").GetInt32().Should().Be(championGenome.Connections[i].TargetNodeId);
            edge.GetProperty("weight").GetDouble().Should().Be(championGenome.Connections[i].Weight);
            edge.GetProperty("enabled").GetBoolean().Should().Be(championGenome.Connections[i].IsEnabled);
        }
    }

    // ── Null argument tests ────────────────────────────────────────────

    [Fact]
    public async Task ExportAsync_NullStream_ForCheckpoint_ThrowsArgumentNullException()
    {
        var checkpoint = CheckpointTestHelper.CreateFullCheckpoint();

        var act = async () => await _exporter.ExportAsync(null!, checkpoint);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ExportAsync_NullCheckpoint_ThrowsArgumentNullException()
    {
        using var stream = new MemoryStream();

        var act = async () => await _exporter.ExportAsync(stream, (TrainingCheckpoint)null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ExportAsync_NullStream_ForResult_ThrowsArgumentNullException()
    {
        var phenotype = new FakeGenome(2, 1);
        var champion = new Champion(phenotype, 1.0, 0);
        var result = new EvolutionResult(champion, new PopulationSnapshot([], 0), new RunHistory([], 0), 42, false);

        var act = async () => await _exporter.ExportAsync(null!, result);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ExportAsync_NullResult_ThrowsArgumentNullException()
    {
        using var stream = new MemoryStream();

        var act = async () => await _exporter.ExportAsync(stream, (EvolutionResult)null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    // ── Seed in metadata ──────────────────────────────────────────────

    [Fact]
    public async Task ExportAsync_EvolutionResult_MetadataSeedMatchesResultSeed()
    {
        var genome = CheckpointTestHelper.CreateMinimalGenome();
        var phenotype = new FakeGenome(genome.Nodes.Count, genome.Connections.Count);
        var champion = new Champion(phenotype, 1.0, 0, Genotype: genome);
        var result = new EvolutionResult(
            champion,
            new PopulationSnapshot([], 0),
            new RunHistory([], 1),
            Seed: 12345,
            WasCancelled: false);

        using var stream = new MemoryStream();
        await _exporter.ExportAsync(stream, result);
        stream.Position = 0;

        using var doc = await JsonDocument.ParseAsync(stream);
        doc.RootElement.GetProperty("metadata").GetProperty("seed").GetInt32().Should().Be(12345);
    }

    // ── Helper ─────────────────────────────────────────────────────────

    /// <summary>
    /// Minimal fake IGenome for test purposes. Does not actually activate.
    /// </summary>
    private sealed class FakeGenome : IGenome
    {
        public int NodeCount { get; }
        public int ConnectionCount { get; }

        public FakeGenome(int nodeCount, int connectionCount)
        {
            NodeCount = nodeCount;
            ConnectionCount = connectionCount;
        }

        public void Activate(ReadOnlySpan<double> inputs, Span<double> outputs) => outputs.Clear();
    }
}
