using NeatSharp.Reporting;

namespace NeatSharp.Evolution;

/// <summary>
/// The complete output of an evolution run.
/// Always returned (never null), even on cancellation.
/// </summary>
/// <param name="Champion">Best genome found during the run.</param>
/// <param name="Population">Final population state.</param>
/// <param name="History">Generation-by-generation record.</param>
/// <param name="Seed">
/// Seed used for this run. If <see cref="Configuration.NeatSharpOptions.Seed"/>
/// was <c>null</c>, this contains the auto-generated seed via
/// <c>Random.Shared.Next()</c>. Identical seed + configuration produces
/// identical results on CPU (FR-009).
/// </param>
/// <param name="WasCancelled">
/// <c>true</c> if the run was cancelled via <see cref="CancellationToken"/>.
/// The result still contains the best genome found so far.
/// </param>
public record EvolutionResult(
    Champion Champion,
    PopulationSnapshot Population,
    RunHistory History,
    int Seed,
    bool WasCancelled);
