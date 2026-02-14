using NeatSharp.Genetics;

namespace NeatSharp.Evolution.Speciation;

/// <summary>
/// Assigns genomes to species based on structural similarity.
/// </summary>
public interface ISpeciationStrategy
{
    /// <summary>
    /// Assigns all genomes in the population to species.
    /// Modifies the species list in place (adds/removes species, updates members).
    /// </summary>
    /// <param name="population">Genomes to assign, each with its fitness score.</param>
    /// <param name="species">Current species list. Modified in place.</param>
    void Speciate(
        IReadOnlyList<(Genome Genome, double Fitness)> population,
        List<Species> species);
}
