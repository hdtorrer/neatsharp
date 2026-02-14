using NeatSharp.Genetics;

namespace NeatSharp.Evolution.Speciation;

/// <summary>
/// A group of structurally similar genomes tracked across generations.
/// Mutable within a generation cycle: members are added/cleared and
/// metadata is updated as part of the speciation process.
/// </summary>
/// <remarks>
/// <para>
/// Species IDs are stable across generations. The same ID is reused as long
/// as the species has members assigned to it. Empty species are removed.
/// </para>
/// <para>
/// State transitions per generation:
/// 1. Clear members (keep representative and metadata).
/// 2. Assign genomes to members list.
/// 3. Update representative to best-performing member.
/// 4. Update stagnation counters.
/// </para>
/// </remarks>
public sealed class Species
{
    /// <summary>
    /// Gets the stable identifier for this species.
    /// </summary>
    public int Id { get; }

    /// <summary>
    /// Gets or sets the representative genome used for compatibility distance comparisons.
    /// Updated each generation to the best-performing member.
    /// </summary>
    public Genome Representative { get; internal set; }

    /// <summary>
    /// Gets the list of members assigned to this species in the current generation.
    /// Each entry includes the genome and its fitness score.
    /// </summary>
    public List<(Genome Genome, double Fitness)> Members { get; } = [];

    /// <summary>
    /// Gets or sets the highest fitness score ever achieved by any member of this species.
    /// </summary>
    public double BestFitnessEver { get; internal set; }

    /// <summary>
    /// Gets or sets the number of consecutive generations without fitness improvement.
    /// Reset to 0 when a member exceeds <see cref="BestFitnessEver"/>.
    /// </summary>
    public int GenerationsSinceImprovement { get; internal set; }

    /// <summary>
    /// Gets the average fitness of all current members.
    /// Returns 0.0 if there are no members.
    /// </summary>
    public double AverageFitness =>
        Members.Count > 0 ? Members.Average(m => m.Fitness) : 0.0;

    /// <summary>
    /// Creates a new species with the specified identifier and representative genome.
    /// </summary>
    /// <param name="id">The stable species identifier.</param>
    /// <param name="representative">The initial representative genome.</param>
    public Species(int id, Genome representative)
    {
        ArgumentNullException.ThrowIfNull(representative);
        Id = id;
        Representative = representative;
    }
}
