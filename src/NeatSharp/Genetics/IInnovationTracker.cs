namespace NeatSharp.Genetics;

/// <summary>
/// Tracks structural mutations and assigns deterministic innovation identifiers.
/// The same structural change registered within the same generation always
/// receives the same innovation ID, enabling correct crossover alignment.
/// </summary>
/// <remarks>
/// <para>
/// Call <see cref="NextGeneration"/> at the start of each new generation
/// to clear the deduplication cache while preserving global ID counters.
/// </para>
/// <para>
/// This service operates in a single-threaded context within a generation.
/// Thread-safe concurrent access is not supported.
/// </para>
/// </remarks>
public interface IInnovationTracker
{
    /// <summary>
    /// Gets or assigns a deterministic innovation number for a new connection
    /// between the specified nodes.
    /// </summary>
    /// <param name="sourceNodeId">The source node ID.</param>
    /// <param name="targetNodeId">The target node ID.</param>
    /// <returns>
    /// The innovation number for this connection. Returns the same value
    /// if the same source/target pair is requested again within the same generation.
    /// </returns>
    int GetConnectionInnovation(int sourceNodeId, int targetNodeId);

    /// <summary>
    /// Gets or assigns deterministic IDs for splitting a connection into two
    /// connections with a new hidden node in between.
    /// </summary>
    /// <param name="connectionInnovation">
    /// The innovation number of the connection being split.
    /// </param>
    /// <returns>
    /// A <see cref="NodeSplitResult"/> containing the new node ID and
    /// innovation numbers for the two new connections.
    /// </returns>
    NodeSplitResult GetNodeSplitInnovation(int connectionInnovation);

    /// <summary>
    /// Advances to the next generation, clearing the deduplication cache
    /// while preserving global ID counters.
    /// </summary>
    void NextGeneration();
}
