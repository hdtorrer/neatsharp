namespace NeatSharp.Configuration;

/// <summary>
/// Defines when evolution terminates. At least one criterion must be configured.
/// </summary>
public class StoppingCriteria
{
    /// <summary>
    /// Maximum number of generations to run. Must be greater than 0 if set.
    /// <c>null</c> means no generation limit.
    /// </summary>
    public int? MaxGenerations { get; set; }

    /// <summary>
    /// Target fitness value. Evolution stops when any genome reaches this fitness.
    /// Must be a finite value if set. <c>null</c> means no fitness target.
    /// </summary>
    public double? FitnessTarget { get; set; }

    /// <summary>
    /// Number of generations without improvement before stopping.
    /// Must be greater than 0 if set. <c>null</c> means no stagnation detection.
    /// </summary>
    public int? StagnationThreshold { get; set; }
}
