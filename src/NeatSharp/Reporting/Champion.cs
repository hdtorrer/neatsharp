using NeatSharp.Genetics;

namespace NeatSharp.Reporting;

/// <summary>
/// The highest-fitness genome produced by an evolution run.
/// </summary>
/// <param name="Genome">The best-performing genome (phenotype).</param>
/// <param name="Fitness">Fitness score.</param>
/// <param name="Generation">Generation in which this champion was found.</param>
/// <param name="Genotype">
/// The raw genotype (<see cref="Genetics.Genome"/>) backing the champion phenotype.
/// Provides access to nodes and connections for serialization and export.
/// May be <c>null</c> if the champion was constructed without genotype information.
/// </param>
public record Champion(IGenome Genome, double Fitness, int Generation, Genome? Genotype = null);
