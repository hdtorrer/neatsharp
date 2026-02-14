using Microsoft.Extensions.Options;
using NeatSharp.Configuration;
using NeatSharp.Genetics;

namespace NeatSharp.Evolution.Speciation;

/// <summary>
/// Computes the NEAT compatibility distance between two genomes using the formula:
/// d = (c1 * E / N) + (c2 * D / N) + (c3 * W)
/// where E = excess genes, D = disjoint genes, W = average weight difference of matching genes,
/// and N = max(connection count of larger genome, 1).
/// </summary>
public sealed class CompatibilityDistance : ICompatibilityDistance
{
    private readonly NeatSharpOptions _options;

    public CompatibilityDistance(IOptions<NeatSharpOptions> options)
    {
        _options = options.Value;
    }

    /// <inheritdoc />
    public double Compute(Genome genome1, Genome genome2)
    {
        var speciation = _options.Speciation;
        var conns1 = genome1.Connections;
        var conns2 = genome2.Connections;

        int excess = 0;
        int disjoint = 0;
        double weightDiffSum = 0.0;
        int matchCount = 0;

        int i = 0, j = 0;

        while (i < conns1.Count && j < conns2.Count)
        {
            int innov1 = conns1[i].InnovationNumber;
            int innov2 = conns2[j].InnovationNumber;

            if (innov1 == innov2)
            {
                matchCount++;
                weightDiffSum += Math.Abs(conns1[i].Weight - conns2[j].Weight);
                i++;
                j++;
            }
            else if (innov1 < innov2)
            {
                disjoint++;
                i++;
            }
            else
            {
                disjoint++;
                j++;
            }
        }

        // Remaining genes in either genome are excess
        excess += (conns1.Count - i) + (conns2.Count - j);

        double w = matchCount > 0 ? weightDiffSum / matchCount : 0.0;
        int n = Math.Max(Math.Max(conns1.Count, conns2.Count), 1);

        return (speciation.ExcessCoefficient * excess / n)
             + (speciation.DisjointCoefficient * disjoint / n)
             + (speciation.WeightDifferenceCoefficient * w);
    }
}
