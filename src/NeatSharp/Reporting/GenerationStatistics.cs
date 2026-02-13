namespace NeatSharp.Reporting;

/// <summary>
/// Per-generation snapshot of population metrics used for run monitoring (FR-012).
/// </summary>
/// <remarks>
/// Instances are collected into <see cref="RunHistory.Generations"/> when
/// <see cref="Configuration.NeatSharpOptions.EnableMetrics"/> is <c>true</c>.
/// When metrics are disabled, these statistics are not allocated and the
/// history's <c>Generations</c> list remains empty — achieving zero overhead (FR-013).
/// </remarks>
/// <param name="Generation">Zero-based generation index.</param>
/// <param name="BestFitness">Highest fitness in this generation.</param>
/// <param name="AverageFitness">Mean fitness across the population.</param>
/// <param name="SpeciesCount">Number of species.</param>
/// <param name="Complexity">Complexity measures for this generation.</param>
/// <param name="Timing">Per-phase timing measures.</param>
public record GenerationStatistics(
    int Generation,
    double BestFitness,
    double AverageFitness,
    int SpeciesCount,
    ComplexityStatistics Complexity,
    TimingBreakdown Timing);
