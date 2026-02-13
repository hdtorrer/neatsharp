using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NeatSharp.Configuration;
using NeatSharp.Evaluation;
using NeatSharp.Evolution;
using NeatSharp.Reporting;

namespace NeatSharp.Extensions;

/// <summary>
/// Extension methods for registering NeatSharp services with the DI container.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers NeatSharp services and configures evolution options.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">
    /// Optional action to configure <see cref="NeatSharpOptions"/>.
    /// If <c>null</c>, default option values are used.
    /// </param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddNeatSharp(
        this IServiceCollection services,
        Action<NeatSharpOptions>? configure = null)
    {
        var optionsBuilder = services.AddOptions<NeatSharpOptions>();

        if (configure is not null)
        {
            optionsBuilder.Configure(configure);
        }

        optionsBuilder.ValidateDataAnnotations().ValidateOnStart();

        services.AddSingleton<IValidateOptions<NeatSharpOptions>, NeatSharpOptionsValidator>();
        services.AddSingleton<IRunReporter, RunReporter>();
        services.AddScoped<INeatEvolver, NeatEvolverStub>();

        return services;
    }

    /// <summary>
    /// Cross-field validation for <see cref="NeatSharpOptions"/>.
    /// </summary>
    private sealed class NeatSharpOptionsValidator : IValidateOptions<NeatSharpOptions>
    {
        public ValidateOptionsResult Validate(string? name, NeatSharpOptions options)
        {
            List<string>? failures = null;

            // Cross-field: at least one stopping criterion required
            if (options.Stopping.MaxGenerations is null
                && options.Stopping.FitnessTarget is null
                && options.Stopping.StagnationThreshold is null)
            {
                (failures ??= []).Add(
                    "At least one stopping criterion is required (MaxGenerations, FitnessTarget, or StagnationThreshold).");
            }

            if (options.Stopping.FitnessTarget.HasValue
                && !double.IsFinite(options.Stopping.FitnessTarget.Value))
            {
                (failures ??= []).Add("StoppingCriteria.FitnessTarget must be a finite value.");
            }

            // Nested StoppingCriteria field validation
            if (options.Stopping.MaxGenerations.HasValue && options.Stopping.MaxGenerations.Value < 1)
            {
                (failures ??= []).Add("StoppingCriteria.MaxGenerations must be greater than or equal to 1.");
            }

            if (options.Stopping.StagnationThreshold.HasValue && options.Stopping.StagnationThreshold.Value < 1)
            {
                (failures ??= []).Add("StoppingCriteria.StagnationThreshold must be greater than or equal to 1.");
            }

            // Nested ComplexityLimits field validation
            if (options.Complexity.MaxNodes.HasValue && options.Complexity.MaxNodes.Value < 1)
            {
                (failures ??= []).Add("ComplexityLimits.MaxNodes must be greater than or equal to 1.");
            }

            if (options.Complexity.MaxConnections.HasValue && options.Complexity.MaxConnections.Value < 1)
            {
                (failures ??= []).Add("ComplexityLimits.MaxConnections must be greater than or equal to 1.");
            }

            return failures is { Count: > 0 }
                ? ValidateOptionsResult.Fail(failures)
                : ValidateOptionsResult.Success;
        }
    }

    /// <summary>
    /// Placeholder evolver until the NEAT algorithm is implemented.
    /// </summary>
    private sealed class NeatEvolverStub : INeatEvolver
    {
        public Task<EvolutionResult> RunAsync(
            IEvaluationStrategy evaluator,
            CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException(
                "The NEAT evolution engine is not yet implemented. "
                + "This API surface defines the public contract for future implementation.");
        }
    }
}
