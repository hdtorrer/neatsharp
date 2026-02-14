using Microsoft.Extensions.Options;
using NeatSharp.Configuration;
using NeatSharp.Genetics;

namespace NeatSharp.Evolution.Mutation;

/// <summary>
/// For each connection in the genome, adjusts its weight by a small random delta
/// drawn from the configured distribution (uniform or Gaussian). Clamps result
/// to [WeightMinValue, WeightMaxValue].
/// </summary>
public sealed class WeightPerturbationMutation : IMutationOperator
{
    private readonly NeatSharpOptions _options;

    public WeightPerturbationMutation(IOptions<NeatSharpOptions> options)
    {
        _options = options.Value;
    }

    /// <inheritdoc />
    public Genome Mutate(Genome genome, Random random, IInnovationTracker tracker)
    {
        if (genome.Connections.Count == 0)
            return genome;

        var mutation = _options.Mutation;
        var newConnections = new ConnectionGene[genome.Connections.Count];

        for (int i = 0; i < genome.Connections.Count; i++)
        {
            var conn = genome.Connections[i];
            double delta = mutation.PerturbationDistribution == WeightDistributionType.Uniform
                ? random.NextDouble() * 2.0 * mutation.PerturbationPower - mutation.PerturbationPower
                : NormalSample(random, 0.0, mutation.PerturbationPower);

            double newWeight = Math.Clamp(conn.Weight + delta, mutation.WeightMinValue, mutation.WeightMaxValue);
            newConnections[i] = conn with { Weight = newWeight };
        }

        return new Genome(genome.Nodes, newConnections);
    }

    /// <summary>
    /// Box-Muller transform to sample from N(mean, stdDev).
    /// </summary>
    private static double NormalSample(Random random, double mean, double stdDev)
    {
        double u1 = 1.0 - random.NextDouble(); // avoid log(0)
        double u2 = random.NextDouble();
        double z = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
        return mean + stdDev * z;
    }
}
