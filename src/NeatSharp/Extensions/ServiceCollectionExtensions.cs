using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using NeatSharp.Configuration;
using NeatSharp.Evaluation;
using NeatSharp.Evolution;
using NeatSharp.Evolution.Crossover;
using NeatSharp.Evolution.Mutation;
using NeatSharp.Evolution.Selection;
using NeatSharp.Evolution.Speciation;
using NeatSharp.Genetics;
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
        services.AddScoped<INeatEvolver, NeatEvolver>();
        services.AddScoped<IPopulationFactory, PopulationFactory>();

        // Genome / Phenotype services (Spec 002)
        services.AddSingleton<IActivationFunctionRegistry, ActivationFunctionRegistry>();
        services.AddSingleton<INetworkBuilder, FeedForwardNetworkBuilder>();
        services.AddScoped<IInnovationTracker>(sp =>
        {
            var opts = sp.GetRequiredService<IOptions<NeatSharpOptions>>().Value;
            // Node IDs 0..I-1 are inputs, I is bias, I+1..I+O are outputs.
            // The tracker must start allocating new node IDs after the initial topology.
            int startNodeId = opts.InputCount + 1 + opts.OutputCount;
            return new InnovationTracker(startNodeId: startNodeId);
        });

        // Evolution: Mutation operators (Spec 003)
        services.AddSingleton<WeightPerturbationMutation>();
        services.AddSingleton<IMutationOperator>(sp => sp.GetRequiredService<WeightPerturbationMutation>());
        services.AddSingleton<WeightReplacementMutation>();
        services.AddSingleton<IMutationOperator>(sp => sp.GetRequiredService<WeightReplacementMutation>());
        services.AddSingleton<AddConnectionMutation>();
        services.AddSingleton<IMutationOperator>(sp => sp.GetRequiredService<AddConnectionMutation>());
        services.AddSingleton<AddNodeMutation>();
        services.AddSingleton<IMutationOperator>(sp => sp.GetRequiredService<AddNodeMutation>());
        services.AddSingleton<ToggleEnableMutation>();
        services.AddSingleton<IMutationOperator>(sp => sp.GetRequiredService<ToggleEnableMutation>());
        services.AddSingleton<CompositeMutationOperator>();

        // Evolution: Crossover (Spec 003)
        services.AddSingleton<ICrossoverOperator, NeatCrossover>();

        // Evolution: Speciation (Spec 003)
        services.AddSingleton<ICompatibilityDistance, CompatibilityDistance>();
        services.AddScoped<ISpeciationStrategy, CompatibilitySpeciation>();

        // Evolution: Selection (Spec 003)
        services.TryAddSingleton<IParentSelector, TournamentSelector>();
        services.AddSingleton<ReproductionAllocator>();
        services.AddSingleton<ReproductionOrchestrator>();

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

            // MutationOptions validation
            ValidateRate(ref failures, options.Mutation.WeightPerturbationRate, "Mutation.WeightPerturbationRate");
            ValidateRate(ref failures, options.Mutation.WeightReplacementRate, "Mutation.WeightReplacementRate");
            ValidateRate(ref failures, options.Mutation.AddConnectionRate, "Mutation.AddConnectionRate");
            ValidateRate(ref failures, options.Mutation.AddNodeRate, "Mutation.AddNodeRate");
            ValidateRate(ref failures, options.Mutation.ToggleEnableRate, "Mutation.ToggleEnableRate");

            if (options.Mutation.PerturbationPower <= 0.0)
            {
                (failures ??= []).Add("Mutation.PerturbationPower must be greater than 0.");
            }

            if (options.Mutation.WeightMinValue >= options.Mutation.WeightMaxValue)
            {
                (failures ??= []).Add("Mutation.WeightMinValue must be less than Mutation.WeightMaxValue.");
            }

            if (options.Mutation.MaxAddConnectionAttempts < 1)
            {
                (failures ??= []).Add("Mutation.MaxAddConnectionAttempts must be greater than or equal to 1.");
            }

            if (options.Mutation.WeightPerturbationRate + options.Mutation.WeightReplacementRate > 1.0)
            {
                (failures ??= []).Add(
                    "Mutation.WeightPerturbationRate + Mutation.WeightReplacementRate must not exceed 1.0 (they are mutually exclusive).");
            }

            // CrossoverOptions validation
            ValidateRate(ref failures, options.Crossover.CrossoverRate, "Crossover.CrossoverRate");
            ValidateRate(ref failures, options.Crossover.InterspeciesCrossoverRate, "Crossover.InterspeciesCrossoverRate");
            ValidateRate(ref failures, options.Crossover.DisabledGeneInheritanceProbability, "Crossover.DisabledGeneInheritanceProbability");

            // SpeciationOptions validation
            if (options.Speciation.ExcessCoefficient < 0.0)
            {
                (failures ??= []).Add("Speciation.ExcessCoefficient must be greater than or equal to 0.");
            }

            if (options.Speciation.DisjointCoefficient < 0.0)
            {
                (failures ??= []).Add("Speciation.DisjointCoefficient must be greater than or equal to 0.");
            }

            if (options.Speciation.WeightDifferenceCoefficient < 0.0)
            {
                (failures ??= []).Add("Speciation.WeightDifferenceCoefficient must be greater than or equal to 0.");
            }

            if (options.Speciation.CompatibilityThreshold <= 0.0)
            {
                (failures ??= []).Add("Speciation.CompatibilityThreshold must be greater than 0.");
            }

            // SelectionOptions validation
            if (options.Selection.ElitismThreshold < 1)
            {
                (failures ??= []).Add("Selection.ElitismThreshold must be greater than or equal to 1.");
            }

            if (options.Selection.StagnationThreshold < 1)
            {
                (failures ??= []).Add("Selection.StagnationThreshold must be greater than or equal to 1.");
            }

            if (options.Selection.SurvivalThreshold is <= 0.0 or > 1.0)
            {
                (failures ??= []).Add("Selection.SurvivalThreshold must be in the range (0, 1].");
            }

            if (options.Selection.TournamentSize < 1)
            {
                (failures ??= []).Add("Selection.TournamentSize must be greater than or equal to 1.");
            }

            // ComplexityPenaltyOptions validation
            if (options.ComplexityPenalty.Coefficient < 0.0)
            {
                (failures ??= []).Add("ComplexityPenalty.Coefficient must be greater than or equal to 0.");
            }

            // EvaluationOptions validation
            if (!double.IsFinite(options.Evaluation.ErrorFitnessValue) || options.Evaluation.ErrorFitnessValue < 0.0)
            {
                (failures ??= []).Add("Evaluation.ErrorFitnessValue must be a finite, non-negative value.");
            }

            return failures is { Count: > 0 }
                ? ValidateOptionsResult.Fail(failures)
                : ValidateOptionsResult.Success;
        }

        private static void ValidateRate(ref List<string>? failures, double value, string name)
        {
            if (value is < 0.0 or > 1.0)
            {
                (failures ??= []).Add($"{name} must be in the range [0, 1].");
            }
        }
    }

}
