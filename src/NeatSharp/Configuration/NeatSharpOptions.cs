using System.ComponentModel.DataAnnotations;

namespace NeatSharp.Configuration;

/// <summary>
/// The complete set of parameters governing an evolution run.
/// Registered via the Options pattern and validated at DI startup.
/// </summary>
public class NeatSharpOptions
{
    /// <summary>
    /// Number of genomes in each generation's population.
    /// Must be between 1 and 100,000. Default is 150.
    /// </summary>
    [Range(1, 100_000)]
    public int PopulationSize { get; set; } = 150;

    /// <summary>
    /// Random seed for deterministic reproduction. When <c>null</c> (default),
    /// a seed is auto-generated via <c>Random.Shared.Next()</c> and recorded
    /// in <see cref="NeatSharp.Evolution.EvolutionResult.Seed"/> so the run
    /// can be reproduced later.
    /// </summary>
    public int? Seed { get; set; }

    /// <summary>
    /// Criteria that determine when evolution stops.
    /// At least one criterion must be configured.
    /// </summary>
    public StoppingCriteria Stopping { get; set; } = new();

    /// <summary>
    /// Bounds on network complexity to prevent unbounded growth.
    /// All limits are optional; <c>null</c> means unbounded.
    /// </summary>
    public ComplexityLimits Complexity { get; set; } = new();

    /// <summary>
    /// Controls whether per-generation statistics are collected into
    /// <see cref="NeatSharp.Reporting.RunHistory"/> (FR-012, FR-013).
    /// When <c>false</c>, the history's <c>Generations</c> list is empty
    /// and per-generation allocation is skipped entirely — achieving zero
    /// overhead. Logging uses <c>[LoggerMessage]</c> source-generated methods
    /// that short-circuit on <c>IsEnabled</c> checks before any allocation.
    /// Default is <c>true</c>.
    /// </summary>
    public bool EnableMetrics { get; set; } = true;
}
