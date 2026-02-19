using NeatSharp.Genetics;

namespace NeatSharp.Evolution.Mutation;

/// <summary>
/// Selects a random connection and flips its IsEnabled state.
/// </summary>
public sealed class ToggleEnableMutation : IMutationOperator
{
    /// <inheritdoc />
    public Genome Mutate(Genome genome, Random random, IInnovationTracker tracker)
    {
        if (genome.Connections.Count == 0)
        {
            return genome;
        }

        int index = random.Next(genome.Connections.Count);

        var newConnections = new ConnectionGene[genome.Connections.Count];
        for (int i = 0; i < genome.Connections.Count; i++)
        {
            newConnections[i] = i == index
                ? genome.Connections[i] with { IsEnabled = !genome.Connections[i].IsEnabled }
                : genome.Connections[i];
        }

        return new Genome(genome.Nodes, newConnections);
    }
}
