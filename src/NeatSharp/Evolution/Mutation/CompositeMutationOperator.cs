using Microsoft.Extensions.Options;
using NeatSharp.Configuration;
using NeatSharp.Genetics;

namespace NeatSharp.Evolution.Mutation;

/// <summary>
/// Orchestrates the application of individual mutations based on configured rates.
/// Applies mutations in a fixed order: weight perturbation/replacement (mutually exclusive),
/// then structural (add-node, add-connection independently), then toggle-enable.
/// Multiple mutations can apply to the same genome in a single call.
/// </summary>
public sealed class CompositeMutationOperator : IMutationOperator
{
    private readonly NeatSharpOptions _options;
    private readonly WeightPerturbationMutation _weightPerturbation;
    private readonly WeightReplacementMutation _weightReplacement;
    private readonly AddConnectionMutation _addConnection;
    private readonly AddNodeMutation _addNode;
    private readonly ToggleEnableMutation _toggleEnable;

    public CompositeMutationOperator(
        IOptions<NeatSharpOptions> options,
        WeightPerturbationMutation weightPerturbation,
        WeightReplacementMutation weightReplacement,
        AddConnectionMutation addConnection,
        AddNodeMutation addNode,
        ToggleEnableMutation toggleEnable)
    {
        _options = options.Value;
        _weightPerturbation = weightPerturbation;
        _weightReplacement = weightReplacement;
        _addConnection = addConnection;
        _addNode = addNode;
        _toggleEnable = toggleEnable;
    }

    /// <summary>
    /// Applies mutations to the genome based on configured rates.
    /// </summary>
    /// <param name="genome">The source genome. Not modified.</param>
    /// <param name="random">Seeded RNG for deterministic behavior.</param>
    /// <param name="tracker">Innovation tracker for structural mutations.</param>
    /// <returns>A new genome with applicable mutations applied.</returns>
    public Genome Mutate(Genome genome, Random random, IInnovationTracker tracker)
    {
        var mutation = _options.Mutation;
        var current = genome;

        // Step 1: Weight perturbation vs replacement (mutually exclusive)
        double weightRoll = random.NextDouble();
        if (weightRoll < mutation.WeightPerturbationRate)
        {
            current = _weightPerturbation.Mutate(current, random, tracker);
        }
        else if (weightRoll < mutation.WeightPerturbationRate + mutation.WeightReplacementRate)
        {
            current = _weightReplacement.Mutate(current, random, tracker);
        }

        // Step 2: Structural mutations (independent rolls)
        if (random.NextDouble() < mutation.AddNodeRate)
        {
            current = _addNode.Mutate(current, random, tracker);
        }

        if (random.NextDouble() < mutation.AddConnectionRate)
        {
            current = _addConnection.Mutate(current, random, tracker);
        }

        // Step 3: Toggle enable
        if (random.NextDouble() < mutation.ToggleEnableRate)
        {
            current = _toggleEnable.Mutate(current, random, tracker);
        }

        return current;
    }
}
