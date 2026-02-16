using NeatSharp.Genetics;

namespace NeatSharp.Serialization.Dto;

/// <summary>
/// Data transfer object for <see cref="ConnectionGene"/> serialization.
/// </summary>
public class ConnectionGeneDto
{
    /// <summary>
    /// Gets or sets the globally unique innovation number.
    /// </summary>
    public int InnovationNumber { get; set; }

    /// <summary>
    /// Gets or sets the source node ID.
    /// </summary>
    public int SourceNodeId { get; set; }

    /// <summary>
    /// Gets or sets the target node ID.
    /// </summary>
    public int TargetNodeId { get; set; }

    /// <summary>
    /// Gets or sets the connection weight.
    /// </summary>
    public double Weight { get; set; }

    /// <summary>
    /// Gets or sets whether this connection is enabled.
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// Maps a <see cref="ConnectionGene"/> domain object to a <see cref="ConnectionGeneDto"/>.
    /// </summary>
    /// <param name="connection">The connection gene to map.</param>
    /// <returns>A new DTO representing the connection gene.</returns>
    public static ConnectionGeneDto ToDto(ConnectionGene connection) => new()
    {
        InnovationNumber = connection.InnovationNumber,
        SourceNodeId = connection.SourceNodeId,
        TargetNodeId = connection.TargetNodeId,
        Weight = connection.Weight,
        IsEnabled = connection.IsEnabled
    };

    /// <summary>
    /// Maps this DTO back to a <see cref="ConnectionGene"/> domain object.
    /// </summary>
    /// <returns>A new <see cref="ConnectionGene"/> instance.</returns>
    public ConnectionGene ToDomain() => new(
        InnovationNumber,
        SourceNodeId,
        TargetNodeId,
        Weight,
        IsEnabled);
}
