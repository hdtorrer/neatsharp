using NeatSharp.Exceptions;
using NeatSharp.Genetics;

namespace NeatSharp.Serialization.Dto;

/// <summary>
/// Data transfer object for <see cref="NodeGene"/> serialization.
/// </summary>
public class NodeGeneDto
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
    /// Gets or sets the activation function name.
    /// </summary>
    public string ActivationFunction { get; set; } = string.Empty;

    /// <summary>
    /// Maps a <see cref="NodeGene"/> domain object to a <see cref="NodeGeneDto"/>.
    /// </summary>
    /// <param name="node">The node gene to map.</param>
    /// <returns>A new DTO representing the node gene.</returns>
    public static NodeGeneDto ToDto(NodeGene node) => new()
    {
        Id = node.Id,
        Type = node.Type.ToString().ToLowerInvariant(),
        ActivationFunction = node.ActivationFunction
    };

    /// <summary>
    /// Maps this DTO back to a <see cref="NodeGene"/> domain object.
    /// </summary>
    /// <returns>A new <see cref="NodeGene"/> instance.</returns>
    public NodeGene ToDomain()
    {
        if (!Enum.TryParse<NodeType>(Type, ignoreCase: true, out var nodeType))
        {
            throw new CheckpointCorruptionException(
                [$"Node {Id} has unrecognized type '{Type}'. Expected one of: {string.Join(", ", Enum.GetNames<NodeType>())}"]);
        }

        return new NodeGene(Id, nodeType, ActivationFunction);
    }
}
