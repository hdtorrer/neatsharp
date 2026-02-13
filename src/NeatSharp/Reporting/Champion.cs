using NeatSharp.Genetics;

namespace NeatSharp.Reporting;

/// <summary>
/// The highest-fitness genome produced by an evolution run.
/// </summary>
/// <param name="Genome">The best-performing genome.</param>
/// <param name="Fitness">Fitness score.</param>
/// <param name="Generation">Generation in which this champion was found.</param>
public record Champion(IGenome Genome, double Fitness, int Generation);
