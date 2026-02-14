using Microsoft.Extensions.Options;
using NeatSharp.Configuration;
using NeatSharp.Genetics;

namespace NeatSharp.Evolution.Mutation;

/// <summary>
/// Selects two random nodes that are not already directly connected and adds a new
/// connection between them with a random weight. Rejects connections that would
/// create a cycle in feed-forward mode.
/// </summary>
public sealed class AddConnectionMutation : IMutationOperator
{
    private readonly NeatSharpOptions _options;

    public AddConnectionMutation(IOptions<NeatSharpOptions> options)
    {
        _options = options.Value;
    }

    /// <inheritdoc />
    public Genome Mutate(Genome genome, Random random, IInnovationTracker tracker)
    {
        // Check MaxConnections limit
        if (_options.Complexity.MaxConnections.HasValue
            && genome.Connections.Count >= _options.Complexity.MaxConnections.Value)
            return genome;

        var mutation = _options.Mutation;

        // Build existing connection set for quick lookup
        var existingConnections = new HashSet<(int Source, int Target)>();
        foreach (var conn in genome.Connections)
        {
            existingConnections.Add((conn.SourceNodeId, conn.TargetNodeId));
        }

        // Build adjacency for cycle detection (enabled connections only)
        var adjacency = new Dictionary<int, List<int>>();
        foreach (var node in genome.Nodes)
        {
            adjacency[node.Id] = [];
        }
        foreach (var conn in genome.Connections)
        {
            if (conn.IsEnabled)
            {
                adjacency[conn.SourceNodeId].Add(conn.TargetNodeId);
            }
        }

        // Build node type lookup
        var nodeTypes = new Dictionary<int, NodeType>();
        foreach (var node in genome.Nodes)
        {
            nodeTypes[node.Id] = node.Type;
        }

        // Try random pairs up to MaxAddConnectionAttempts
        for (int attempt = 0; attempt < mutation.MaxAddConnectionAttempts; attempt++)
        {
            int sourceIdx = random.Next(genome.Nodes.Count);
            int targetIdx = random.Next(genome.Nodes.Count);

            var sourceNode = genome.Nodes[sourceIdx];
            var targetNode = genome.Nodes[targetIdx];

            // Target must not be input or bias
            if (targetNode.Type is NodeType.Input or NodeType.Bias)
                continue;

            // No self-connections
            if (sourceNode.Id == targetNode.Id)
                continue;

            // No duplicate connections
            if (existingConnections.Contains((sourceNode.Id, targetNode.Id)))
                continue;

            // Cycle detection: would adding source -> target create a cycle?
            // A cycle exists if target can reach source via existing enabled connections
            if (WouldCreateCycle(adjacency, sourceNode.Id, targetNode.Id))
                continue;

            // Valid pair found — create the new connection
            int innovation = tracker.GetConnectionInnovation(sourceNode.Id, targetNode.Id);
            double weight = random.NextDouble() * (mutation.WeightMaxValue - mutation.WeightMinValue)
                            + mutation.WeightMinValue;

            var newConnection = new ConnectionGene(innovation, sourceNode.Id, targetNode.Id, weight, true);

            var newConnections = new ConnectionGene[genome.Connections.Count + 1];
            for (int i = 0; i < genome.Connections.Count; i++)
                newConnections[i] = genome.Connections[i];
            newConnections[^1] = newConnection;

            return new Genome(genome.Nodes, newConnections);
        }

        // No valid pair found after all attempts
        return genome;
    }

    /// <summary>
    /// Checks if adding a connection from source to target would create a cycle.
    /// Uses DFS from target to see if source is reachable via existing enabled connections.
    /// </summary>
    private static bool WouldCreateCycle(Dictionary<int, List<int>> adjacency, int sourceId, int targetId)
    {
        // If target can reach source via existing connections, adding source -> target creates a cycle
        var visited = new HashSet<int>();
        var stack = new Stack<int>();
        stack.Push(targetId);

        while (stack.Count > 0)
        {
            int current = stack.Pop();
            if (current == sourceId)
                return true;

            if (!visited.Add(current))
                continue;

            if (adjacency.TryGetValue(current, out var neighbors))
            {
                foreach (int neighbor in neighbors)
                {
                    if (!visited.Contains(neighbor))
                        stack.Push(neighbor);
                }
            }
        }

        return false;
    }
}
