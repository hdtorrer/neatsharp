namespace NeatSharp.Reporting;

/// <summary>
/// Aggregate complexity measures for a generation, part of per-generation metrics reporting (FR-012).
/// </summary>
/// <remarks>
/// Included in each <see cref="GenerationStatistics"/> record when
/// <see cref="Configuration.NeatSharpOptions.EnableMetrics"/> is <c>true</c>.
/// Not allocated when metrics are disabled.
/// </remarks>
/// <param name="AverageNodes">Mean node count across genomes.</param>
/// <param name="AverageConnections">Mean connection count across genomes.</param>
public record ComplexityStatistics(double AverageNodes, double AverageConnections);
