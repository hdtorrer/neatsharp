using NeatSharp.Evolution;

namespace NeatSharp.Reporting;

/// <summary>
/// Produces human-readable text summaries of evolution run results.
/// </summary>
/// <remarks>
/// A default implementation is registered by <c>AddNeatSharp()</c>.
/// Consumers can replace it with a custom implementation via DI.
/// The default summary includes champion fitness, generation count,
/// seed used, species count, and cancellation status.
/// </remarks>
public interface IRunReporter
{
    /// <summary>
    /// Generates a human-readable text summary of an evolution run.
    /// </summary>
    /// <param name="result">The evolution result to summarize.</param>
    /// <returns>A formatted text summary of the run.</returns>
    public string GenerateSummary(EvolutionResult result);
}
