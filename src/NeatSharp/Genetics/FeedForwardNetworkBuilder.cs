using NeatSharp.Exceptions;

namespace NeatSharp.Genetics;

/// <summary>
/// Converts a <see cref="Genome"/> (genotype) into a <see cref="FeedForwardNetwork"/>
/// (phenotype) by performing reachability analysis, topological sorting via
/// Kahn's algorithm, and pre-computing the evaluation order.
/// </summary>
public sealed class FeedForwardNetworkBuilder : INetworkBuilder
{
    private readonly IActivationFunctionRegistry _activationRegistry;

    public FeedForwardNetworkBuilder(IActivationFunctionRegistry activationRegistry)
    {
        ArgumentNullException.ThrowIfNull(activationRegistry);
        _activationRegistry = activationRegistry;
    }

    /// <inheritdoc />
    public IGenome Build(Genome genome)
    {
        ArgumentNullException.ThrowIfNull(genome);

        // Step 1: Filter to enabled connections only
        var enabledConnections = new List<ConnectionGene>();
        foreach (var conn in genome.Connections)
        {
            if (conn.IsEnabled)
            {
                enabledConnections.Add(conn);
            }
        }

        // Build node lookup
        var nodeById = new Dictionary<int, NodeGene>();
        foreach (var node in genome.Nodes)
        {
            nodeById[node.Id] = node;
        }

        // Step 2: Build adjacency graph from enabled connections
        var forwardAdj = new Dictionary<int, List<int>>(); // nodeId -> list of successor nodeIds
        var backwardAdj = new Dictionary<int, List<int>>(); // nodeId -> list of predecessor nodeIds

        foreach (var node in genome.Nodes)
        {
            forwardAdj[node.Id] = [];
            backwardAdj[node.Id] = [];
        }

        foreach (var conn in enabledConnections)
        {
            forwardAdj[conn.SourceNodeId].Add(conn.TargetNodeId);
            backwardAdj[conn.TargetNodeId].Add(conn.SourceNodeId);
        }

        // Step 3: Forward BFS from input + bias nodes
        var forwardReachable = new HashSet<int>();
        var queue = new Queue<int>();

        foreach (var node in genome.Nodes)
        {
            if (node.Type is NodeType.Input or NodeType.Bias)
            {
                forwardReachable.Add(node.Id);
                queue.Enqueue(node.Id);
            }
        }

        while (queue.Count > 0)
        {
            int current = queue.Dequeue();
            foreach (int successor in forwardAdj[current])
            {
                if (forwardReachable.Add(successor))
                {
                    queue.Enqueue(successor);
                }
            }
        }

        // Step 4: Backward BFS from output nodes
        var backwardReachable = new HashSet<int>();

        foreach (var node in genome.Nodes)
        {
            if (node.Type == NodeType.Output)
            {
                backwardReachable.Add(node.Id);
                queue.Enqueue(node.Id);
            }
        }

        while (queue.Count > 0)
        {
            int current = queue.Dequeue();
            foreach (int predecessor in backwardAdj[current])
            {
                if (backwardReachable.Add(predecessor))
                {
                    queue.Enqueue(predecessor);
                }
            }
        }

        // Step 5: Reachable = forward ∩ backward, plus all input/bias/output nodes always included
        var reachable = new HashSet<int>(forwardReachable);
        reachable.IntersectWith(backwardReachable);

        // Input, bias, and output nodes are always included in the reachable set
        // so the phenotype's input/output counts match the genome's declaration
        foreach (var node in genome.Nodes)
        {
            if (node.Type is NodeType.Input or NodeType.Bias or NodeType.Output)
            {
                reachable.Add(node.Id);
            }
        }

        // Filter connections to reachable subgraph
        var reachableConnections = new List<ConnectionGene>();
        foreach (var conn in enabledConnections)
        {
            if (reachable.Contains(conn.SourceNodeId) && reachable.Contains(conn.TargetNodeId))
            {
                reachableConnections.Add(conn);
            }
        }

        // Step 6: Kahn's algorithm topological sort on reachable subgraph
        var inDegree = new Dictionary<int, int>();
        var reachableForwardAdj = new Dictionary<int, List<int>>();

        foreach (int nodeId in reachable)
        {
            inDegree[nodeId] = 0;
            reachableForwardAdj[nodeId] = [];
        }

        foreach (var conn in reachableConnections)
        {
            reachableForwardAdj[conn.SourceNodeId].Add(conn.TargetNodeId);
            inDegree[conn.TargetNodeId]++;
        }

        var sortedOrder = new List<int>();
        var kahnQueue = new Queue<int>();

        foreach (var (nodeId, degree) in inDegree)
        {
            if (degree == 0)
            {
                kahnQueue.Enqueue(nodeId);
            }
        }

        while (kahnQueue.Count > 0)
        {
            int current = kahnQueue.Dequeue();
            sortedOrder.Add(current);

            foreach (int successor in reachableForwardAdj[current])
            {
                inDegree[successor]--;
                if (inDegree[successor] == 0)
                {
                    kahnQueue.Enqueue(successor);
                }
            }
        }

        // If not all reachable nodes were processed, a cycle exists
        if (sortedOrder.Count != reachable.Count)
        {
            throw new CycleDetectedException(
                "The genome's enabled connections form a cycle, making feed-forward evaluation impossible.");
        }

        // Step 7: Pre-compute evaluation data
        // Assign buffer indices to reachable nodes in topological order
        var bufferIndex = new Dictionary<int, int>();
        for (int i = 0; i < sortedOrder.Count; i++)
        {
            bufferIndex[sortedOrder[i]] = i;
        }

        // Build incoming connection map for each node
        var incomingMap = new Dictionary<int, List<(int SourceIndex, double Weight)>>();
        foreach (int nodeId in reachable)
        {
            incomingMap[nodeId] = [];
        }

        foreach (var conn in reachableConnections)
        {
            incomingMap[conn.TargetNodeId].Add((bufferIndex[conn.SourceNodeId], conn.Weight));
        }

        // Identify input, bias, and output buffer indices
        var inputIndices = new List<int>();
        var biasIndices = new List<int>();
        var outputIndices = new List<int>();

        foreach (int nodeId in sortedOrder)
        {
            var node = nodeById[nodeId];
            switch (node.Type)
            {
                case NodeType.Input:
                    inputIndices.Add(bufferIndex[nodeId]);
                    break;
                case NodeType.Bias:
                    biasIndices.Add(bufferIndex[nodeId]);
                    break;
                case NodeType.Output:
                    outputIndices.Add(bufferIndex[nodeId]);
                    break;
            }
        }

        // Build evaluation order for hidden and output nodes only
        var evalOrder = new List<FeedForwardNetwork.EvalNode>();
        foreach (int nodeId in sortedOrder)
        {
            var node = nodeById[nodeId];
            if (node.Type is NodeType.Hidden or NodeType.Output)
            {
                var activationFunc = _activationRegistry.Get(node.ActivationFunction);
                var incoming = incomingMap[nodeId].ToArray();
                evalOrder.Add(new FeedForwardNetwork.EvalNode(
                    bufferIndex[nodeId],
                    incoming,
                    activationFunc));
            }
        }

        // Step 8: Return FeedForwardNetwork
        return new FeedForwardNetwork(
            inputCount: inputIndices.Count,
            outputCount: outputIndices.Count,
            inputIndices: inputIndices.ToArray(),
            biasIndices: biasIndices.ToArray(),
            outputIndices: outputIndices.ToArray(),
            evalOrder: evalOrder.ToArray(),
            nodeCount: reachable.Count,
            connectionCount: reachableConnections.Count);
    }
}
