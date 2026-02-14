namespace NeatSharp.Genetics;

/// <summary>
/// Contains the deterministic IDs assigned when splitting a connection
/// into two connections with a new hidden node.
/// </summary>
/// <param name="NewNodeId">ID for the new hidden node created by the split.</param>
/// <param name="IncomingConnectionInnovation">
/// Innovation number for the connection from the original source to the new node.
/// </param>
/// <param name="OutgoingConnectionInnovation">
/// Innovation number for the connection from the new node to the original target.
/// </param>
public readonly record struct NodeSplitResult(
    int NewNodeId,
    int IncomingConnectionInnovation,
    int OutgoingConnectionInnovation);
