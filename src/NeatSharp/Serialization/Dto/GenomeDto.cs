using NeatSharp.Genetics;

namespace NeatSharp.Serialization.Dto;

/// <summary>
/// Data transfer object for <see cref="Genome"/> serialization.
/// </summary>
public class GenomeDto
{
    /// <summary>
    /// Gets or sets the list of node gene DTOs.
    /// </summary>
    public List<NodeGeneDto> Nodes { get; set; } = [];

    /// <summary>
    /// Gets or sets the list of connection gene DTOs.
    /// </summary>
    public List<ConnectionGeneDto> Connections { get; set; } = [];

    /// <summary>
    /// Maps a <see cref="Genome"/> domain object to a <see cref="GenomeDto"/>.
    /// </summary>
    /// <param name="genome">The genome to map.</param>
    /// <returns>A new DTO representing the genome.</returns>
    public static GenomeDto ToDto(Genome genome) => new()
    {
        Nodes = genome.Nodes.Select(NodeGeneDto.ToDto).ToList(),
        Connections = genome.Connections.Select(ConnectionGeneDto.ToDto).ToList()
    };

    /// <summary>
    /// Maps this DTO back to a <see cref="Genome"/> domain object.
    /// </summary>
    /// <returns>A new <see cref="Genome"/> instance.</returns>
    public Genome ToDomain() => new(
        Nodes.Select(n => n.ToDomain()).ToArray(),
        Connections.Select(c => c.ToDomain()).ToArray());
}
