using System.Globalization;
using System.Text;
using NeatSharp.Evolution;

namespace NeatSharp.Reporting;

/// <summary>
/// Default implementation of <see cref="IRunReporter"/> that produces a
/// human-readable text summary of an evolution run.
/// </summary>
/// <remarks>
/// The summary includes champion fitness, generation count, seed used,
/// species count, and cancellation status. Registered as a singleton
/// by <c>AddNeatSharp()</c>. Consumers can replace it with a custom
/// implementation via DI.
/// </remarks>
public sealed class RunReporter : IRunReporter
{
    /// <inheritdoc />
    public string GenerateSummary(EvolutionResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        var sb = new StringBuilder();
        sb.AppendLine(CultureInfo.InvariantCulture, $"Champion Fitness: {result.Champion.Fitness}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"Champion Generation: {result.Champion.Generation}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"Total Generations: {result.History.TotalGenerations}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"Species Count: {result.Population.Species.Count}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"Seed: {result.Seed}");

        if (result.WasCancelled)
        {
            sb.AppendLine("Status: Cancelled");
        }

        return sb.ToString();
    }
}
