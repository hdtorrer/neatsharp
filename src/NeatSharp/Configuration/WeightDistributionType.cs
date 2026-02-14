namespace NeatSharp.Configuration;

/// <summary>
/// Distribution used for weight perturbation during mutation.
/// </summary>
public enum WeightDistributionType
{
    /// <summary>
    /// Uniform distribution: delta in [-power, +power].
    /// </summary>
    Uniform,

    /// <summary>
    /// Gaussian distribution: delta ~ N(0, power).
    /// </summary>
    Gaussian
}
