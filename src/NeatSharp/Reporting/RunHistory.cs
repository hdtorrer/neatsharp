namespace NeatSharp.Reporting;

/// <summary>
/// Chronological record of an evolution run.
/// </summary>
/// <param name="Generations">
/// Per-generation statistics. Empty when
/// <see cref="Configuration.NeatSharpOptions.EnableMetrics"/> is <c>false</c>.
/// </param>
/// <param name="TotalGenerations">
/// Number of generations completed. Always reflects the actual count,
/// regardless of the <c>EnableMetrics</c> setting.
/// </param>
public record RunHistory(IReadOnlyList<GenerationStatistics> Generations, int TotalGenerations);
