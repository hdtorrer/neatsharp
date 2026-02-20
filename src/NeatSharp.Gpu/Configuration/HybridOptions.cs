using System.ComponentModel.DataAnnotations;

namespace NeatSharp.Gpu.Configuration;

/// <summary>
/// Partitioning policy for hybrid evaluation.
/// </summary>
public enum SplitPolicyType
{
    /// <summary>
    /// Fixed split ratio from <see cref="HybridOptions.StaticGpuFraction"/>.
    /// </summary>
    Static = 0,

    /// <summary>
    /// Complexity-driven split using genome structural properties.
    /// Routes complex genomes to GPU and simple genomes to CPU.
    /// </summary>
    CostBased = 1,

    /// <summary>
    /// Throughput-driven PID controller targeting zero idle-time difference
    /// between CPU and GPU backends.
    /// </summary>
    Adaptive = 2
}

/// <summary>
/// Configuration options for hybrid CPU+GPU evaluation.
/// </summary>
/// <remarks>
/// Configured via the <c>AddNeatSharpHybrid()</c> extension method:
/// <code>
/// services.AddNeatSharpHybrid(hybrid =>
/// {
///     hybrid.SplitPolicy = SplitPolicyType.Adaptive;
///     hybrid.MinPopulationForSplit = 50;
/// });
/// </code>
/// </remarks>
public sealed class HybridOptions
{
    /// <summary>
    /// Gets or sets whether hybrid evaluation is enabled.
    /// When false, delegates directly to the inner evaluator with zero overhead.
    /// Default: true.
    /// </summary>
    public bool EnableHybrid { get; set; } = true;

    /// <summary>
    /// Gets or sets the partitioning policy for hybrid evaluation.
    /// Default: <see cref="SplitPolicyType.Adaptive"/>.
    /// </summary>
    public SplitPolicyType SplitPolicy { get; set; } = SplitPolicyType.Adaptive;

    /// <summary>
    /// Gets or sets the GPU fraction for static split policy.
    /// 0.0 = all CPU, 1.0 = all GPU.
    /// Only used when <see cref="SplitPolicy"/> is <see cref="SplitPolicyType.Static"/>.
    /// Default: 0.7.
    /// </summary>
    [Range(0.0, 1.0)]
    public double StaticGpuFraction { get; set; } = 0.7;

    /// <summary>
    /// Gets or sets the minimum population size for hybrid splitting.
    /// Below this threshold, the entire population is evaluated on a single backend.
    /// Default: 50.
    /// </summary>
    [Range(2, 100_000)]
    public int MinPopulationForSplit { get; set; } = 50;

    /// <summary>
    /// Gets or sets the number of generations between GPU availability re-probes
    /// after a GPU failure. Default: 10.
    /// </summary>
    [Range(1, 1_000)]
    public int GpuReprobeInterval { get; set; } = 10;

    /// <summary>
    /// Gets or sets the PID controller parameters for adaptive partitioning.
    /// Only used when <see cref="SplitPolicy"/> is <see cref="SplitPolicyType.Adaptive"/>.
    /// </summary>
    public AdaptivePidOptions Adaptive { get; set; } = new();

    /// <summary>
    /// Gets or sets the cost model parameters for cost-based partitioning.
    /// Only used when <see cref="SplitPolicy"/> is <see cref="SplitPolicyType.CostBased"/>.
    /// </summary>
    public CostModelOptions CostModel { get; set; } = new();
}

/// <summary>
/// PID controller parameters for adaptive split policy.
/// </summary>
public sealed class AdaptivePidOptions
{
    /// <summary>
    /// Proportional gain. Controls response magnitude to current error.
    /// Default: 0.5.
    /// </summary>
    [Range(double.Epsilon, 10.0)]
    public double Kp { get; set; } = 0.5;

    /// <summary>
    /// Integral gain. Eliminates steady-state offset.
    /// Set to 0 to disable integral correction.
    /// Default: 0.1.
    /// </summary>
    [Range(0.0, 10.0)]
    public double Ki { get; set; } = 0.1;

    /// <summary>
    /// Derivative gain. Dampens oscillations.
    /// Set to 0 to disable derivative damping.
    /// Default: 0.05.
    /// </summary>
    [Range(0.0, 10.0)]
    public double Kd { get; set; } = 0.05;

    /// <summary>
    /// Starting GPU fraction before any measurements are available.
    /// Default: 0.5 (even split).
    /// </summary>
    [Range(0.0, 1.0)]
    public double InitialGpuFraction { get; set; } = 0.5;
}

/// <summary>
/// Cost model parameters for complexity-based genome partitioning.
/// </summary>
public sealed class CostModelOptions
{
    /// <summary>
    /// Weight for node count in cost formula: cost = NodeWeight * nodeCount + ConnectionWeight * connectionCount.
    /// Default: 1.0.
    /// </summary>
    [Range(0.0, 1000.0)]
    public double NodeWeight { get; set; } = 1.0;

    /// <summary>
    /// Weight for connection count in cost formula.
    /// Default: 1.0.
    /// </summary>
    [Range(0.0, 1000.0)]
    public double ConnectionWeight { get; set; } = 1.0;
}
