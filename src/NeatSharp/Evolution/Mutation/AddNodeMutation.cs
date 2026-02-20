using Microsoft.Extensions.Options;
using NeatSharp.Configuration;
using NeatSharp.Genetics;

namespace NeatSharp.Evolution.Mutation;

/// <summary>
/// Selects a random enabled connection, disables it, inserts a new hidden node,
/// and creates two new connections: source-to-new (weight 1.0) and new-to-target
/// (original weight). Preserves phenotype equivalence.
/// </summary>
public sealed class AddNodeMutation : IMutationOperator
{
    private readonly NeatSharpOptions _options;

    public AddNodeMutation(IOptions<NeatSharpOptions> options)
    {
        _options = options.Value;
    }

    /// <inheritdoc />
    public Genome Mutate(Genome genome, Random random, IInnovationTracker tracker)
    {
        // Check MaxNodes limit
        if (_options.Complexity.MaxNodes.HasValue && genome.Nodes.Count >= _options.Complexity.MaxNodes.Value)
        {
            return genome;
        }

        // Find enabled connections
        var enabledIndices = new List<int>();
        for (int i = 0; i < genome.Connections.Count; i++)
        {
            if (genome.Connections[i].IsEnabled)
            {
                enabledIndices.Add(i);
            }
        }

        if (enabledIndices.Count == 0)
        {
            return genome;
        }

        // Select random enabled connection
        int selectedIndex = enabledIndices[random.Next(enabledIndices.Count)];
        var selectedConn = genome.Connections[selectedIndex];

        // Get innovation IDs from tracker
        var splitResult = tracker.GetNodeSplitInnovation(selectedConn.InnovationNumber);

        // Build new node list
        var newNodes = new NodeGene[genome.Nodes.Count + 1];
        for (int i = 0; i < genome.Nodes.Count; i++)
        {
            newNodes[i] = genome.Nodes[i];
        }

        newNodes[^1] = new NodeGene(splitResult.NewNodeId, NodeType.Hidden);

        // Build new connection list
        var newConnections = new ConnectionGene[genome.Connections.Count + 2];
        for (int i = 0; i < genome.Connections.Count; i++)
        {
            newConnections[i] = i == selectedIndex
                ? selectedConn with { IsEnabled = false }
                : genome.Connections[i];
        }

        // Source -> New node (weight = 1.0)
        newConnections[^2] = new ConnectionGene(
            splitResult.IncomingConnectionInnovation,
            selectedConn.SourceNodeId,
            splitResult.NewNodeId,
            1.0,
            true);

        // New node -> Target (weight = original weight)
        newConnections[^1] = new ConnectionGene(
            splitResult.OutgoingConnectionInnovation,
            splitResult.NewNodeId,
            selectedConn.TargetNodeId,
            selectedConn.Weight,
            true);

        return new Genome(newNodes, newConnections);
    }
}
