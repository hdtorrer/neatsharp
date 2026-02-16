using Microsoft.Extensions.Options;

namespace NeatSharp.Gpu.Configuration;

/// <summary>
/// Cross-field validation for <see cref="GpuOptions"/>.
/// </summary>
/// <remarks>
/// Registered via the Options pattern. Validates ranges for
/// <see cref="GpuOptions.MinComputeCapability"/> and
/// <see cref="GpuOptions.MaxPopulationSize"/> that cannot be
/// fully expressed with <c>[Range]</c> attributes alone
/// (nullable handling, descriptive messages).
/// </remarks>
public sealed class GpuOptionsValidator : IValidateOptions<GpuOptions>
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, GpuOptions options)
    {
        List<string>? failures = null;

        if (options.MinComputeCapability < 20 || options.MinComputeCapability > 100)
        {
            (failures ??= []).Add(
                "MinComputeCapability must be between 20 and 100 (inclusive). " +
                $"Value: {options.MinComputeCapability}.");
        }

        if (options.MaxPopulationSize.HasValue)
        {
            if (options.MaxPopulationSize.Value < 1 || options.MaxPopulationSize.Value > 1_000_000)
            {
                (failures ??= []).Add(
                    "MaxPopulationSize must be between 1 and 1,000,000 (inclusive) when specified. " +
                    $"Value: {options.MaxPopulationSize.Value}.");
            }
        }

        return failures is { Count: > 0 }
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }
}
