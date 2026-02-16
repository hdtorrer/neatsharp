using System.ComponentModel.DataAnnotations;

namespace NeatSharp.Gpu.Configuration;

/// <summary>
/// Configuration options for GPU-accelerated evaluation.
/// </summary>
/// <remarks>
/// Configured via the <c>AddNeatSharpGpu()</c> extension method:
/// <code>
/// services.AddNeatSharpGpu(gpu =>
/// {
///     gpu.EnableGpu = true;
///     gpu.BestEffortDeterministic = false;
/// });
/// </code>
/// </remarks>
public sealed class GpuOptions
{
    /// <summary>
    /// Gets or sets whether GPU evaluation is enabled.
    /// When false, forces CPU-only evaluation regardless of hardware availability.
    /// Default: true (auto-detect and use GPU if available).
    /// </summary>
    public bool EnableGpu { get; set; } = true;

    /// <summary>
    /// Gets or sets the minimum CUDA compute capability as an integer
    /// (major * 10 + minor). For example, 50 = compute capability 5.0.
    /// Devices below this threshold are rejected.
    /// Default: 50 (Maxwell architecture, ~2014).
    /// </summary>
    [Range(20, 100)]
    public int MinComputeCapability { get; set; } = 50;

    /// <summary>
    /// Gets or sets whether to use best-effort deterministic algorithms
    /// on the GPU. Default: false.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>Reserved for future use.</strong> The current GPU kernel implementation
    /// uses sequential accumulation (1 thread per genome, fixed topological order) which
    /// is inherently deterministic. This option has no effect on runtime behavior in the
    /// current version.
    /// </para>
    /// <para>
    /// In a future version, a parallel accumulation kernel may be added for higher
    /// throughput. When that happens, setting this option to <c>true</c> will select the
    /// deterministic (sequential) accumulation path, while <c>false</c> will allow the
    /// non-deterministic (parallel, potentially faster) path.
    /// </para>
    /// </remarks>
    public bool BestEffortDeterministic { get; set; }

    /// <summary>
    /// Gets or sets the maximum population size for GPU buffer preallocation.
    /// When null, buffers are auto-sized from the first batch and grown as needed.
    /// Setting this avoids reallocation overhead when population size is known.
    /// Default: null (auto-size).
    /// </summary>
    [Range(1, 1_000_000)]
    public int? MaxPopulationSize { get; set; }
}
