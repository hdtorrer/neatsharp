using Microsoft.Extensions.Options;
using NeatSharp.Configuration;
using NeatSharp.Genetics;

namespace NeatSharp.Evolution.Mutation;

/// <summary>
/// Replaces the weight of a randomly selected connection with a new random value
/// drawn uniformly from [WeightMinValue, WeightMaxValue].
/// </summary>
public sealed class WeightReplacementMutation : IMutationOperator
{
    private readonly NeatSharpOptions _options;

    public WeightReplacementMutation(IOptions<NeatSharpOptions> options)
    {
        _options = options.Value;
    }

    /// <inheritdoc />
    public Genome Mutate(Genome genome, Random random, IInnovationTracker tracker)
    {
        if (genome.Connections.Count == 0)
            return genome;

        var mutation = _options.Mutation;
        int index = random.Next(genome.Connections.Count);
        double newWeight = random.NextDouble() * (mutation.WeightMaxValue - mutation.WeightMinValue)
                           + mutation.WeightMinValue;

        var newConnections = new ConnectionGene[genome.Connections.Count];
        for (int i = 0; i < genome.Connections.Count; i++)
        {
            newConnections[i] = i == index
                ? genome.Connections[i] with { Weight = newWeight }
                : genome.Connections[i];
        }

        return new Genome(genome.Nodes, newConnections);
    }
}
