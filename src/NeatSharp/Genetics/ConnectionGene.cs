namespace NeatSharp.Genetics;

/// <summary>
/// Represents a directed weighted connection (connection gene) between two
/// nodes in a NEAT genome. Immutable after construction.
/// </summary>
/// <param name="InnovationNumber">
/// Globally unique identifier tracking the structural origin of this connection.
/// Assigned by <see cref="IInnovationTracker"/>.
/// </param>
/// <param name="SourceNodeId">ID of the origin node.</param>
/// <param name="TargetNodeId">ID of the destination node.</param>
/// <param name="Weight">Connection strength (numeric weight).</param>
/// <param name="IsEnabled">
/// Whether this connection participates in phenotype evaluation.
/// Disabled connections are excluded from signal propagation.
/// </param>
public sealed record ConnectionGene(
    int InnovationNumber,
    int SourceNodeId,
    int TargetNodeId,
    double Weight,
    bool IsEnabled);
