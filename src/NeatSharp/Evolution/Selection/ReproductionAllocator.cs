using Microsoft.Extensions.Options;
using NeatSharp.Configuration;
using NeatSharp.Evolution.Speciation;
using NeatSharp.Genetics;

namespace NeatSharp.Evolution.Selection;

/// <summary>
/// Allocates offspring counts to species based on their adjusted average fitness,
/// with stagnation penalties and elitism support.
/// </summary>
public sealed class ReproductionAllocator
{
    private readonly NeatSharpOptions _options;

    public ReproductionAllocator(IOptions<NeatSharpOptions> options)
    {
        _options = options.Value;
    }

    /// <summary>
    /// Computes the number of offspring each species should produce for the next generation.
    /// </summary>
    /// <param name="species">The current species list with members and fitness data.</param>
    /// <param name="populationSize">The target total population size.</param>
    /// <returns>A dictionary mapping species ID to offspring count (including elite champion).</returns>
    public IReadOnlyDictionary<int, int> AllocateOffspring(
        IReadOnlyList<Species> species,
        int populationSize)
    {
        var result = new Dictionary<int, int>();

        if (species.Count == 0)
            return result;

        var selection = _options.Selection;

        // Identify top 2 species by BestFitnessEver (protected from stagnation elimination)
        var protectedIds = new HashSet<int>();
        var sortedByPeak = species
            .OrderByDescending(s => s.BestFitnessEver)
            .Take(2)
            .Select(s => s.Id);
        foreach (var id in sortedByPeak)
        {
            protectedIds.Add(id);
        }

        // Compute adjusted average fitness per species
        var adjustedAvg = new double[species.Count];
        for (int i = 0; i < species.Count; i++)
        {
            var s = species[i];
            bool isStagnant = s.GenerationsSinceImprovement > selection.StagnationThreshold;
            bool isProtected = protectedIds.Contains(s.Id);

            if (isStagnant && !isProtected)
            {
                adjustedAvg[i] = 0.0;
            }
            else
            {
                double avg = ComputePenaltyAdjustedAverage(s);
                adjustedAvg[i] = Math.Max(avg, 0.0);
            }
        }

        // Compute total adjusted fitness
        double totalAdjusted = 0.0;
        for (int i = 0; i < adjustedAvg.Length; i++)
        {
            totalAdjusted += adjustedAvg[i];
        }

        // If total is zero (all species stagnant or zero fitness), distribute equally among protected
        if (totalAdjusted <= 0.0)
        {
            foreach (var s in species)
            {
                result[s.Id] = 0;
            }

            // Give equal share to protected species
            if (protectedIds.Count > 0)
            {
                int perProtected = populationSize / protectedIds.Count;
                int remainder = populationSize % protectedIds.Count;
                bool first = true;
                foreach (var id in protectedIds)
                {
                    result[id] = perProtected + (first ? remainder : 0);
                    first = false;
                }
            }

            return result;
        }

        // Proportional allocation with fractional tracking for largest-remainder adjustment
        var rawAllocations = new double[species.Count];
        var allocations = new int[species.Count];

        for (int i = 0; i < species.Count; i++)
        {
            rawAllocations[i] = adjustedAvg[i] / totalAdjusted * populationSize;
            allocations[i] = (int)Math.Floor(rawAllocations[i]);
            result[species[i].Id] = allocations[i];
        }

        // Adjust for rounding: distribute remaining slots to species with largest fractional remainders
        int allocated = allocations.Sum();
        int remaining = populationSize - allocated;

        if (remaining > 0)
        {
            // Build list of (index, fractional part) and sort by fractional part descending
            var fractionalParts = new (int Index, double Fraction)[species.Count];
            for (int i = 0; i < species.Count; i++)
            {
                fractionalParts[i] = (i, rawAllocations[i] - allocations[i]);
            }

            Array.Sort(fractionalParts, (a, b) => b.Fraction.CompareTo(a.Fraction));

            for (int i = 0; i < remaining; i++)
            {
                int idx = fractionalParts[i].Index;
                result[species[idx].Id]++;
            }
        }

        return result;
    }

    private double ComputePenaltyAdjustedAverage(Species species)
    {
        var penalty = _options.ComplexityPenalty;
        if (penalty.Coefficient == 0.0 || species.Members.Count == 0)
            return species.AverageFitness;

        double sum = 0.0;
        foreach (var (genome, fitness) in species.Members)
        {
            double complexityMeasure = GetComplexityMeasure(genome, penalty.Metric);
            sum += fitness - penalty.Coefficient * complexityMeasure;
        }

        return sum / species.Members.Count;
    }

    private static int GetComplexityMeasure(Genome genome, ComplexityPenaltyMetric metric) =>
        metric switch
        {
            ComplexityPenaltyMetric.NodeCount => genome.Nodes.Count,
            ComplexityPenaltyMetric.ConnectionCount => genome.Connections.Count,
            ComplexityPenaltyMetric.Both => genome.Nodes.Count + genome.Connections.Count,
            _ => 0
        };
}
