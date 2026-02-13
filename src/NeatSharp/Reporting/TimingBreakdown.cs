namespace NeatSharp.Reporting;

/// <summary>
/// Per-phase timing measures for a generation.
/// </summary>
/// <param name="Evaluation">Time spent evaluating genomes.</param>
/// <param name="Reproduction">Time spent on selection, crossover, and mutation.</param>
/// <param name="Speciation">Time spent on speciation.</param>
public record TimingBreakdown(TimeSpan Evaluation, TimeSpan Reproduction, TimeSpan Speciation);
