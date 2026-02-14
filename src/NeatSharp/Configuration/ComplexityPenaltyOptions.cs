namespace NeatSharp.Configuration;

/// <summary>
/// Configuration for the optional soft complexity penalty that reduces effective fitness
/// based on genome structural size. When <see cref="Coefficient"/> is 0.0 (default),
/// the penalty is disabled.
/// </summary>
public class ComplexityPenaltyOptions
{
    /// <summary>
    /// Multiplier for the complexity measure subtracted from raw fitness.
    /// A value of 0.0 disables the penalty entirely.
    /// Must be non-negative.
    /// </summary>
    public double Coefficient { get; set; } = 0.0;

    /// <summary>
    /// Which structural metric to use when computing the complexity penalty.
    /// </summary>
    public ComplexityPenaltyMetric Metric { get; set; } = ComplexityPenaltyMetric.Both;
}
