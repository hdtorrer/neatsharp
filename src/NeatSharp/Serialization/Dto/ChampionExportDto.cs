using NeatSharp.Genetics;

namespace NeatSharp.Serialization.Dto;

/// <summary>
/// Top-level data transfer object for champion genome export.
/// Contains the champion's neural network graph as nodes and edges,
/// along with metadata for interoperability with external tools.
/// </summary>
public class ChampionExportDto
{
    /// <summary>
    /// Gets or sets the schema version of the export format.
    /// </summary>
    public string SchemaVersion { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the artifact metadata.
    /// </summary>
    public ArtifactMetadataDto Metadata { get; set; } = new();

    /// <summary>
    /// Gets or sets the champion information (fitness and generation found).
    /// </summary>
    public ChampionInfoDto Champion { get; set; } = new();

    /// <summary>
    /// Gets or sets the list of neural network nodes.
    /// </summary>
    public List<ExportNodeDto> Nodes { get; set; } = [];

    /// <summary>
    /// Gets or sets the list of neural network edges (connections).
    /// </summary>
    public List<ExportEdgeDto> Edges { get; set; } = [];

    /// <summary>
    /// Creates a <see cref="ChampionExportDto"/> from a champion genome with associated metadata.
    /// </summary>
    /// <param name="genome">The champion genome containing nodes and connections.</param>
    /// <param name="fitness">The champion's fitness score.</param>
    /// <param name="generationFound">The generation in which the champion was found.</param>
    /// <param name="metadata">The artifact metadata for the export.</param>
    /// <returns>A new <see cref="ChampionExportDto"/> representing the champion's neural network.</returns>
    public static ChampionExportDto FromGenome(
        Genome genome,
        double fitness,
        int generationFound,
        ArtifactMetadata metadata)
    {
        var nodes = new List<ExportNodeDto>(genome.Nodes.Count);
        foreach (var node in genome.Nodes)
        {
            nodes.Add(new ExportNodeDto
            {
                Id = node.Id,
                Type = node.Type.ToString().ToLowerInvariant(),
                ActivationFunction = node.ActivationFunction
            });
        }

        var edges = new List<ExportEdgeDto>(genome.Connections.Count);
        foreach (var connection in genome.Connections)
        {
            edges.Add(new ExportEdgeDto
            {
                Source = connection.SourceNodeId,
                Target = connection.TargetNodeId,
                Weight = connection.Weight,
                Enabled = connection.IsEnabled
            });
        }

        return new ChampionExportDto
        {
            SchemaVersion = Serialization.SchemaVersion.Current,
            Metadata = ArtifactMetadataDto.ToDto(metadata),
            Champion = new ChampionInfoDto
            {
                Fitness = fitness,
                GenerationFound = generationFound
            },
            Nodes = nodes,
            Edges = edges
        };
    }
}

/// <summary>
/// Data transfer object for champion summary information in the export format.
/// </summary>
public class ChampionInfoDto
{
    /// <summary>
    /// Gets or sets the champion's fitness score.
    /// </summary>
    public double Fitness { get; set; }

    /// <summary>
    /// Gets or sets the generation in which the champion was found.
    /// </summary>
    public int GenerationFound { get; set; }
}

/// <summary>
/// Data transfer object for an exported neural network node.
/// </summary>
public class ExportNodeDto
{
    /// <summary>
    /// Gets or sets the unique node identifier.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the node type as a lowercase string (e.g., "input", "hidden", "output", "bias").
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the activation function name (e.g., "sigmoid", "tanh", "identity").
    /// </summary>
    public string ActivationFunction { get; set; } = string.Empty;
}

/// <summary>
/// Data transfer object for an exported neural network edge (connection).
/// </summary>
public class ExportEdgeDto
{
    /// <summary>
    /// Gets or sets the source node identifier.
    /// </summary>
    public int Source { get; set; }

    /// <summary>
    /// Gets or sets the target node identifier.
    /// </summary>
    public int Target { get; set; }

    /// <summary>
    /// Gets or sets the connection weight.
    /// </summary>
    public double Weight { get; set; }

    /// <summary>
    /// Gets or sets whether this connection is enabled.
    /// </summary>
    public bool Enabled { get; set; }
}
