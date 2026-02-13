namespace NeatSharp.Reporting;

/// <summary>
/// A single species within a population snapshot.
/// </summary>
/// <param name="Id">Species identifier.</param>
/// <param name="Members">Genomes in this species.</param>
public record SpeciesSnapshot(int Id, IReadOnlyList<GenomeInfo> Members);
