// Contract definition — not compilable source code.
// Defines the immutable genome class for NEAT.

namespace NeatSharp.Genetics;

/// <summary>
/// A complete neural network blueprint composed of an immutable, ordered
/// collection of node genes and connection genes. Fully read-only after
/// construction — mutation operations (future specs) produce new genome instances.
/// </summary>
/// <remarks>
/// <para>
/// The genome serves as the genotype in the NEAT algorithm. To execute
/// inference, convert it to a phenotype using <see cref="INetworkBuilder.Build"/>.
/// </para>
/// <para>
/// Constructor validation throws <see cref="InvalidGenomeException"/> if:
/// node IDs are not unique, connections reference non-existent nodes,
/// or the genome lacks required input/output nodes.
/// </para>
/// </remarks>
public sealed class Genome
{
    /// <summary>
    /// Gets the ordered collection of node genes in this genome.
    /// </summary>
    public IReadOnlyList<NodeGene> Nodes { get; }

    /// <summary>
    /// Gets the ordered collection of connection genes in this genome.
    /// </summary>
    public IReadOnlyList<ConnectionGene> Connections { get; }

    /// <summary>
    /// Gets the number of input-type nodes in this genome.
    /// </summary>
    public int InputCount { get; }

    /// <summary>
    /// Gets the number of output-type nodes in this genome.
    /// </summary>
    public int OutputCount { get; }

    /// <summary>
    /// Creates a new genome from the specified node and connection genes.
    /// Collections are defensively copied — subsequent modifications to the
    /// original collections do not affect the genome.
    /// </summary>
    /// <param name="nodes">
    /// The node genes. Must contain at least one input and one output node.
    /// All node IDs must be unique.
    /// </param>
    /// <param name="connections">
    /// The connection genes. All source/target node IDs must reference
    /// nodes present in <paramref name="nodes"/>.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="nodes"/> or <paramref name="connections"/> is null.
    /// </exception>
    /// <exception cref="InvalidGenomeException">
    /// Thrown when node IDs are not unique, connections reference non-existent
    /// nodes, or required input/output nodes are missing.
    /// </exception>
    public Genome(IReadOnlyList<NodeGene> nodes, IReadOnlyList<ConnectionGene> connections);
}
