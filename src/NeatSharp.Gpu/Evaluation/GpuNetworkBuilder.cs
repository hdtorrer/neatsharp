using NeatSharp.Genetics;
using NeatSharp.Gpu.Exceptions;

namespace NeatSharp.Gpu.Evaluation;

/// <summary>
/// Decorator over <see cref="INetworkBuilder"/> that produces <see cref="GpuFeedForwardNetwork"/>
/// instances wrapping both a CPU phenotype and flat GPU topology arrays.
/// </summary>
/// <remarks>
/// <para>
/// The builder delegates CPU phenotype construction to the inner builder
/// (typically <see cref="FeedForwardNetworkBuilder"/>), then independently extracts
/// flat GPU topology from the <see cref="Genome"/> using the same reachability analysis
/// and topological sort algorithm.
/// </para>
/// <para>
/// Activation function names are mapped to <see cref="GpuActivationFunction"/> enum values
/// using a case-insensitive dictionary. Unknown activation functions throw
/// <see cref="GpuEvaluationException"/>.
/// </para>
/// </remarks>
internal sealed class GpuNetworkBuilder : INetworkBuilder
{
    private readonly INetworkBuilder _innerBuilder;

    private static readonly Dictionary<string, int> ActivationMap =
        new(StringComparer.OrdinalIgnoreCase)
        {
            [ActivationFunctions.Sigmoid] = (int)GpuActivationFunction.Sigmoid,
            [ActivationFunctions.Tanh] = (int)GpuActivationFunction.Tanh,
            [ActivationFunctions.ReLU] = (int)GpuActivationFunction.ReLU,
            [ActivationFunctions.Step] = (int)GpuActivationFunction.Step,
            [ActivationFunctions.Identity] = (int)GpuActivationFunction.Identity
        };

    /// <summary>
    /// Initializes a new instance of <see cref="GpuNetworkBuilder"/>.
    /// </summary>
    /// <param name="innerBuilder">
    /// The inner network builder (typically <see cref="FeedForwardNetworkBuilder"/>)
    /// used for CPU phenotype construction.
    /// </param>
    public GpuNetworkBuilder(INetworkBuilder innerBuilder)
    {
        ArgumentNullException.ThrowIfNull(innerBuilder);
        _innerBuilder = innerBuilder;
    }

    /// <inheritdoc />
    public IGenome Build(Genome genome)
    {
        ArgumentNullException.ThrowIfNull(genome);

        // Step 1: Build CPU phenotype via inner builder
        var cpuNetwork = _innerBuilder.Build(genome);

        // Step 2: Extract flat GPU topology from the Genome
        // This duplicates the reachability analysis + topological sort from FeedForwardNetworkBuilder
        // but extracts flat arrays suitable for GPU kernels.

        // Filter enabled connections
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

        // Build adjacency graph
        var forwardAdj = new Dictionary<int, List<int>>();
        var backwardAdj = new Dictionary<int, List<int>>();

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

        // Forward BFS from input + bias nodes
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

        // Backward BFS from output nodes
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

        // Reachable = forward intersection backward, plus all input/bias/output always included
        var reachable = new HashSet<int>(forwardReachable);
        reachable.IntersectWith(backwardReachable);

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

        // Kahn's topological sort on reachable subgraph
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

        // Assign buffer indices in topological order
        var bufferIndex = new Dictionary<int, int>();
        for (int i = 0; i < sortedOrder.Count; i++)
        {
            bufferIndex[sortedOrder[i]] = i;
        }

        // Build incoming connection map for each node
        var incomingMap = new Dictionary<int, List<(int SourceIndex, float Weight)>>();
        foreach (int nodeId in reachable)
        {
            incomingMap[nodeId] = [];
        }

        foreach (var conn in reachableConnections)
        {
            incomingMap[conn.TargetNodeId].Add((bufferIndex[conn.SourceNodeId], (float)conn.Weight));
        }

        // Map activation functions and build node activation types array
        var nodeActivationTypes = new int[sortedOrder.Count];
        for (int i = 0; i < sortedOrder.Count; i++)
        {
            var node = nodeById[sortedOrder[i]];
            nodeActivationTypes[i] = MapActivationFunction(node.ActivationFunction);
        }

        // Identify input, bias, and output buffer indices (in genome declaration order)
        var inputIndices = new List<int>();
        var biasIndices = new List<int>();
        var outputIndices = new List<int>();

        foreach (var node in genome.Nodes)
        {
            if (!bufferIndex.TryGetValue(node.Id, out int idx))
            {
                continue;
            }

            switch (node.Type)
            {
                case NodeType.Input:
                    inputIndices.Add(idx);
                    break;
                case NodeType.Bias:
                    biasIndices.Add(idx);
                    break;
                case NodeType.Output:
                    outputIndices.Add(idx);
                    break;
            }
        }

        // Build eval order for hidden + output nodes
        var evalOrder = new List<GpuEvalNode>();
        foreach (int nodeId in sortedOrder)
        {
            var node = nodeById[nodeId];
            if (node.Type is NodeType.Hidden or NodeType.Output)
            {
                var incoming = incomingMap[nodeId];
                var sources = new int[incoming.Count];
                var weights = new float[incoming.Count];
                for (int i = 0; i < incoming.Count; i++)
                {
                    sources[i] = incoming[i].SourceIndex;
                    weights[i] = incoming[i].Weight;
                }

                evalOrder.Add(new GpuEvalNode(
                    bufferIndex[nodeId],
                    sources,
                    weights,
                    MapActivationFunction(node.ActivationFunction)));
            }
        }

        return new GpuFeedForwardNetwork(
            cpuNetwork,
            inputIndices.ToArray(),
            biasIndices.ToArray(),
            outputIndices.ToArray(),
            nodeActivationTypes,
            evalOrder.ToArray());
    }

    private static int MapActivationFunction(string name)
    {
        if (ActivationMap.TryGetValue(name, out int type))
        {
            return type;
        }

        var available = string.Join(", ", ActivationMap.Keys);
        throw new GpuEvaluationException(
            $"Unknown activation function '{name}'. Supported GPU activation functions: {available}.");
    }
}
