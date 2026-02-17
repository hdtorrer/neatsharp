namespace NeatSharp.Gpu.Scheduling;

/// <summary>
/// Details of a GPU fallback event within a generation.
/// </summary>
/// <param name="Timestamp">When the fallback was triggered.</param>
/// <param name="FailureReason">Exception message or error description.</param>
/// <param name="GenomesRerouted">Number of genomes rerouted from GPU to CPU.</param>
public readonly record struct FallbackEventInfo(
    DateTimeOffset Timestamp,
    string FailureReason,
    int GenomesRerouted);
