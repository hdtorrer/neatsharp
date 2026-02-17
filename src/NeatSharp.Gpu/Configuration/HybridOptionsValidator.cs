using Microsoft.Extensions.Options;

namespace NeatSharp.Gpu.Configuration;

/// <summary>
/// Cross-field validation for <see cref="HybridOptions"/>.
/// </summary>
/// <remarks>
/// Registered via the Options pattern. Validates ranges for all numeric
/// properties in <see cref="HybridOptions"/>, <see cref="AdaptivePidOptions"/>,
/// and <see cref="CostModelOptions"/> that cannot be fully expressed with
/// <c>[Range]</c> attributes alone (descriptive messages, strict positivity).
/// </remarks>
public sealed class HybridOptionsValidator : IValidateOptions<HybridOptions>
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, HybridOptions options)
    {
        List<string>? failures = null;

        if (options.StaticGpuFraction < 0.0 || options.StaticGpuFraction > 1.0)
        {
            (failures ??= []).Add(
                "StaticGpuFraction must be between 0.0 and 1.0 (inclusive). " +
                $"Value: {options.StaticGpuFraction}.");
        }

        if (options.MinPopulationForSplit < 2 || options.MinPopulationForSplit > 100_000)
        {
            (failures ??= []).Add(
                "MinPopulationForSplit must be between 2 and 100,000 (inclusive). " +
                $"Value: {options.MinPopulationForSplit}.");
        }

        if (options.GpuReprobeInterval < 1 || options.GpuReprobeInterval > 1_000)
        {
            (failures ??= []).Add(
                "GpuReprobeInterval must be between 1 and 1,000 (inclusive). " +
                $"Value: {options.GpuReprobeInterval}.");
        }

        // Adaptive PID options
        if (options.Adaptive.Kp <= 0.0)
        {
            (failures ??= []).Add(
                "Adaptive.Kp must be strictly positive. " +
                $"Value: {options.Adaptive.Kp}.");
        }

        if (options.Adaptive.Ki < 0.0 || options.Adaptive.Ki > 10.0)
        {
            (failures ??= []).Add(
                "Adaptive.Ki must be between 0.0 and 10.0 (inclusive). " +
                $"Value: {options.Adaptive.Ki}.");
        }

        if (options.Adaptive.Kd < 0.0 || options.Adaptive.Kd > 10.0)
        {
            (failures ??= []).Add(
                "Adaptive.Kd must be between 0.0 and 10.0 (inclusive). " +
                $"Value: {options.Adaptive.Kd}.");
        }

        if (options.Adaptive.InitialGpuFraction < 0.0 || options.Adaptive.InitialGpuFraction > 1.0)
        {
            (failures ??= []).Add(
                "Adaptive.InitialGpuFraction must be between 0.0 and 1.0 (inclusive). " +
                $"Value: {options.Adaptive.InitialGpuFraction}.");
        }

        // Cost model options
        if (options.CostModel.NodeWeight < 0.0 || options.CostModel.NodeWeight > 1000.0)
        {
            (failures ??= []).Add(
                "CostModel.NodeWeight must be between 0.0 and 1000.0 (inclusive). " +
                $"Value: {options.CostModel.NodeWeight}.");
        }

        if (options.CostModel.ConnectionWeight < 0.0 || options.CostModel.ConnectionWeight > 1000.0)
        {
            (failures ??= []).Add(
                "CostModel.ConnectionWeight must be between 0.0 and 1000.0 (inclusive). " +
                $"Value: {options.CostModel.ConnectionWeight}.");
        }

        return failures is { Count: > 0 }
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }
}
