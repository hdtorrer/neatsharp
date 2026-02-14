namespace NeatSharp.Configuration;

/// <summary>
/// Configuration for mutation operators. All rates are probabilities in [0.0, 1.0].
/// Defaults are sourced from the original NEAT paper (Stanley &amp; Miikkulainen, 2002).
/// </summary>
public class MutationOptions
{
    /// <summary>
    /// Probability of applying weight perturbation mutation.
    /// Mutually exclusive with <see cref="WeightReplacementRate"/> per application.
    /// </summary>
    public double WeightPerturbationRate { get; set; } = 0.8;

    /// <summary>
    /// Probability of applying weight replacement mutation.
    /// Mutually exclusive with <see cref="WeightPerturbationRate"/> per application.
    /// </summary>
    public double WeightReplacementRate { get; set; } = 0.1;

    /// <summary>
    /// Probability of adding a new connection between unconnected nodes.
    /// </summary>
    public double AddConnectionRate { get; set; } = 0.05;

    /// <summary>
    /// Probability of splitting an existing connection with a new node.
    /// </summary>
    public double AddNodeRate { get; set; } = 0.03;

    /// <summary>
    /// Probability of toggling a connection's enabled state.
    /// </summary>
    public double ToggleEnableRate { get; set; } = 0.01;

    /// <summary>
    /// Maximum absolute delta for uniform perturbation, or standard deviation for Gaussian.
    /// Must be greater than 0.
    /// </summary>
    public double PerturbationPower { get; set; } = 0.5;

    /// <summary>
    /// Distribution used for weight perturbation.
    /// </summary>
    public WeightDistributionType PerturbationDistribution { get; set; } = WeightDistributionType.Uniform;

    /// <summary>
    /// Minimum allowed weight value. Must be less than <see cref="WeightMaxValue"/>.
    /// </summary>
    public double WeightMinValue { get; set; } = -4.0;

    /// <summary>
    /// Maximum allowed weight value. Must be greater than <see cref="WeightMinValue"/>.
    /// </summary>
    public double WeightMaxValue { get; set; } = 4.0;

    /// <summary>
    /// Maximum random node-pair attempts before skipping add-connection mutation.
    /// Must be at least 1.
    /// </summary>
    public int MaxAddConnectionAttempts { get; set; } = 20;
}
