namespace NeatSharp.Configuration;

/// <summary>
/// Metric used to measure genome complexity for the soft complexity penalty.
/// </summary>
public enum ComplexityPenaltyMetric
{
    /// <summary>
    /// Penalty based on the number of nodes in the genome.
    /// </summary>
    NodeCount,

    /// <summary>
    /// Penalty based on the number of connections in the genome.
    /// </summary>
    ConnectionCount,

    /// <summary>
    /// Penalty based on the sum of node count and connection count.
    /// </summary>
    Both
}
