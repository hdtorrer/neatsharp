using Microsoft.Extensions.Options;
using NeatSharp.Configuration;
using NeatSharp.Genetics;

namespace NeatSharp.Evolution.Crossover;

/// <summary>
/// NEAT crossover operator that combines two parent genomes using innovation-number-aligned
/// gene inheritance. Matching genes are randomly inherited from either parent (50/50),
/// disjoint and excess genes are inherited from the fitter parent only (or both if equal fitness).
/// </summary>
public sealed class NeatCrossover : ICrossoverOperator
{
    private readonly NeatSharpOptions _options;

    public NeatCrossover(IOptions<NeatSharpOptions> options)
    {
        _options = options.Value;
    }

    /// <inheritdoc />
    public Genome Cross(
        Genome parent1, double parent1Fitness,
        Genome parent2, double parent2Fitness,
        Random random)
    {
        var crossoverOptions = _options.Crossover;
        bool equalFitness = parent1Fitness == parent2Fitness;

        // Determine which parent is fitter. If equal, parent1 is the "fitter" reference
        // for input/output/bias node inheritance.
        Genome fitter;
        Genome lessFit;
        if (parent2Fitness > parent1Fitness)
        {
            fitter = parent2;
            lessFit = parent1;
        }
        else
        {
            fitter = parent1;
            lessFit = parent2;
        }

        // Two-pointer merge on connections sorted by innovation number
        var fitterConns = fitter.Connections;
        var lessFitConns = lessFit.Connections;
        int fi = 0, li = 0;

        var inheritedConnections = new List<ConnectionGene>();
        // Track which parent each inherited connection came from for node resolution
        var connectionParentSource = new List<Genome>();

        while (fi < fitterConns.Count || li < lessFitConns.Count)
        {
            if (fi >= fitterConns.Count)
            {
                // Remaining are excess from less-fit parent
                if (equalFitness)
                {
                    inheritedConnections.Add(lessFitConns[li]);
                    connectionParentSource.Add(lessFit);
                }
                li++;
            }
            else if (li >= lessFitConns.Count)
            {
                // Remaining are excess from fitter parent
                inheritedConnections.Add(fitterConns[fi]);
                connectionParentSource.Add(fitter);
                fi++;
            }
            else
            {
                int fitterInnov = fitterConns[fi].InnovationNumber;
                int lessFitInnov = lessFitConns[li].InnovationNumber;

                if (fitterInnov == lessFitInnov)
                {
                    // Matching gene — randomly inherit from either parent (50/50)
                    bool pickFitter = random.NextDouble() < 0.5;
                    var chosenGene = pickFitter ? fitterConns[fi] : lessFitConns[li];
                    var sourceParent = pickFitter ? fitter : lessFit;

                    // Apply disabled gene inheritance rule:
                    // Both disabled → always disabled
                    // One disabled, one enabled → probabilistic (DisabledGeneInheritanceProbability)
                    // Both enabled → stays enabled
                    bool fitterDisabled = !fitterConns[fi].IsEnabled;
                    bool lessFitDisabled = !lessFitConns[li].IsEnabled;
                    if (fitterDisabled && lessFitDisabled)
                    {
                        chosenGene = chosenGene with { IsEnabled = false };
                    }
                    else if (fitterDisabled || lessFitDisabled)
                    {
                        bool shouldDisable = random.NextDouble() < crossoverOptions.DisabledGeneInheritanceProbability;
                        chosenGene = chosenGene with { IsEnabled = !shouldDisable };
                    }

                    inheritedConnections.Add(chosenGene);
                    connectionParentSource.Add(sourceParent);
                    fi++;
                    li++;
                }
                else if (fitterInnov < lessFitInnov)
                {
                    // Disjoint gene from fitter parent — always inherited
                    inheritedConnections.Add(fitterConns[fi]);
                    connectionParentSource.Add(fitter);
                    fi++;
                }
                else
                {
                    // Disjoint gene from less-fit parent — only if equal fitness
                    if (equalFitness)
                    {
                        inheritedConnections.Add(lessFitConns[li]);
                        connectionParentSource.Add(lessFit);
                    }
                    li++;
                }
            }
        }

        // Build node set: all input/output/bias from fitter parent (or parent1 if equal),
        // plus all nodes referenced by inherited connections
        var referenceParent = fitter; // for equal fitness this is parent1 (since fitter defaults to parent1)
        var nodeMap = new Dictionary<int, NodeGene>();

        // Add all input/output/bias from the reference parent
        foreach (var node in referenceParent.Nodes)
        {
            if (node.Type is NodeType.Input or NodeType.Output or NodeType.Bias)
            {
                nodeMap[node.Id] = node;
            }
        }

        // Build lookup maps for both parents' nodes
        var fitterNodeMap = new Dictionary<int, NodeGene>();
        foreach (var node in fitter.Nodes)
            fitterNodeMap[node.Id] = node;

        var lessFitNodeMap = new Dictionary<int, NodeGene>();
        foreach (var node in lessFit.Nodes)
            lessFitNodeMap[node.Id] = node;

        // Add nodes referenced by inherited connections
        for (int i = 0; i < inheritedConnections.Count; i++)
        {
            var conn = inheritedConnections[i];
            var sourceParent = connectionParentSource[i];
            var sourceNodeMap = sourceParent == fitter ? fitterNodeMap : lessFitNodeMap;

            if (!nodeMap.ContainsKey(conn.SourceNodeId))
            {
                // Prefer the node definition from the connection's source parent,
                // fall back to fitter parent's definition
                if (sourceNodeMap.TryGetValue(conn.SourceNodeId, out var sourceNode))
                    nodeMap[conn.SourceNodeId] = sourceNode;
                else if (fitterNodeMap.TryGetValue(conn.SourceNodeId, out var fitterNode))
                    nodeMap[conn.SourceNodeId] = fitterNode;
                else if (lessFitNodeMap.TryGetValue(conn.SourceNodeId, out var lessFitNode))
                    nodeMap[conn.SourceNodeId] = lessFitNode;
            }

            if (!nodeMap.ContainsKey(conn.TargetNodeId))
            {
                if (sourceNodeMap.TryGetValue(conn.TargetNodeId, out var targetNode))
                    nodeMap[conn.TargetNodeId] = targetNode;
                else if (fitterNodeMap.TryGetValue(conn.TargetNodeId, out var fitterNode))
                    nodeMap[conn.TargetNodeId] = fitterNode;
                else if (lessFitNodeMap.TryGetValue(conn.TargetNodeId, out var lessFitNode))
                    nodeMap[conn.TargetNodeId] = lessFitNode;
            }
        }

        // Sort nodes: input/bias first, then hidden, then output (maintaining stable order)
        var sortedNodes = nodeMap.Values
            .OrderBy(n => n.Type switch
            {
                NodeType.Input => 0,
                NodeType.Bias => 1,
                NodeType.Hidden => 2,
                NodeType.Output => 3,
                _ => 4
            })
            .ThenBy(n => n.Id)
            .ToArray();

        return new Genome(sortedNodes, inheritedConnections.ToArray());
    }
}
