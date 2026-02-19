using NeatSharp.Genetics;

namespace NeatSharp.Evolution.Speciation;

/// <summary>
/// Assigns genomes to species based on structural similarity.
/// </summary>
public interface ISpeciationStrategy
{
    /// <summary>
    /// Gets the next species ID that will be assigned.
    /// </summary>
    public int NextSpeciesId { get; }

    /// <summary>
    /// Assigns all genomes in the population to species.
    /// Modifies the species list in place (adds/removes species, updates members).
    /// </summary>
    /// <param name="population">Genomes to assign, each with its fitness score.</param>
    /// <param name="species">Current species list. Modified in place.</param>
    public void Speciate(
        IReadOnlyList<(Genome Genome, double Fitness)> population,
        List<Species> species);

    /// <summary>
    /// Restores the speciation strategy to a previously saved state.
    /// </summary>
    /// <param name="nextSpeciesId">The next species ID counter value.</param>
    public void RestoreState(int nextSpeciesId);
}
