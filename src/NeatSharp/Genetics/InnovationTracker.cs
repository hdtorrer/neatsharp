namespace NeatSharp.Genetics;

/// <summary>
/// Assigns deterministic, monotonically increasing innovation IDs and node IDs
/// for structural mutations. Same structural change within a generation receives
/// the same ID. <see cref="NextGeneration"/> clears dedup caches while preserving counters.
/// </summary>
public sealed class InnovationTracker : IInnovationTracker
{
    private readonly Dictionary<(int SourceNodeId, int TargetNodeId), int> _connectionCache = [];
    private readonly Dictionary<int, NodeSplitResult> _nodeSplitCache = [];
    private int _nextInnovationNumber;
    private int _nextNodeId;

    /// <summary>
    /// Initializes a new instance of <see cref="InnovationTracker"/> with custom starting values.
    /// </summary>
    /// <param name="startInnovationNumber">The starting innovation number counter.</param>
    /// <param name="startNodeId">The starting node ID counter.</param>
    public InnovationTracker(int startInnovationNumber = 0, int startNodeId = 0)
    {
        _nextInnovationNumber = startInnovationNumber;
        _nextNodeId = startNodeId;
    }

    /// <inheritdoc />
    public int GetConnectionInnovation(int sourceNodeId, int targetNodeId)
    {
        var key = (sourceNodeId, targetNodeId);

        if (_connectionCache.TryGetValue(key, out int existing))
        {
            return existing;
        }

        int innovation = _nextInnovationNumber++;
        _connectionCache[key] = innovation;
        return innovation;
    }

    /// <inheritdoc />
    public NodeSplitResult GetNodeSplitInnovation(int connectionInnovation)
    {
        if (_nodeSplitCache.TryGetValue(connectionInnovation, out NodeSplitResult existing))
        {
            return existing;
        }

        var result = new NodeSplitResult(
            NewNodeId: _nextNodeId++,
            IncomingConnectionInnovation: _nextInnovationNumber++,
            OutgoingConnectionInnovation: _nextInnovationNumber++);

        _nodeSplitCache[connectionInnovation] = result;
        return result;
    }

    /// <inheritdoc />
    public void NextGeneration()
    {
        _connectionCache.Clear();
        _nodeSplitCache.Clear();
    }
}
