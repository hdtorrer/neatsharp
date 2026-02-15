using System.ComponentModel.DataAnnotations;

namespace NeatSharp.Configuration;

/// <summary>
/// The complete set of parameters governing an evolution run.
/// Registered via the Options pattern and validated at DI startup.
/// </summary>
public class NeatSharpOptions
{
    /// <summary>
    /// Number of input nodes per genome. Determines the initial
    /// network topology alongside <see cref="OutputCount"/>.
    /// Must be between 1 and 10,000. Default is 2.
    /// </summary>
    [Range(1, 10_000)]
    public int InputCount { get; set; } = 2;

    /// <summary>
    /// Number of output nodes per genome. Determines the initial
    /// network topology alongside <see cref="InputCount"/>.
    /// Must be between 1 and 10,000. Default is 1.
    /// </summary>
    [Range(1, 10_000)]
    public int OutputCount { get; set; } = 1;

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

    /// <summary>
    /// Mutation operator rates and parameters.
    /// </summary>
    public MutationOptions Mutation { get; set; } = new();

    /// <summary>
    /// Crossover operator parameters.
    /// </summary>
    public CrossoverOptions Crossover { get; set; } = new();

    /// <summary>
    /// Compatibility distance coefficients and speciation threshold.
    /// </summary>
    public SpeciationOptions Speciation { get; set; } = new();

    /// <summary>
    /// Parent selection, elitism, and stagnation parameters.
    /// </summary>
    public SelectionOptions Selection { get; set; } = new();

    /// <summary>
    /// Optional soft complexity penalty that reduces effective fitness based on genome size.
    /// Disabled by default (Coefficient = 0.0).
    /// </summary>
    public ComplexityPenaltyOptions ComplexityPenalty { get; set; } = new();

    /// <summary>
    /// Controls how the evaluation phase handles errors during genome
    /// fitness evaluation. Default behavior assigns fitness 0.0 to
    /// failing genomes and continues the run.
    /// </summary>
    public EvaluationOptions Evaluation { get; set; } = new();
}
