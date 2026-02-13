namespace NeatSharp.Reporting;

/// <summary>
/// Point-in-time view of the entire population at run completion.
/// </summary>
/// <param name="Species">Species groupings.</param>
/// <param name="TotalCount">Total number of genomes.</param>
public record PopulationSnapshot(IReadOnlyList<SpeciesSnapshot> Species, int TotalCount);
