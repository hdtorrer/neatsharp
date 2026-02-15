using Microsoft.Extensions.Options;
using NeatSharp.Configuration;
using NeatSharp.Genetics;

namespace NeatSharp.Evolution;

/// <summary>
/// Default implementation of <see cref="IPopulationFactory"/>. Creates minimal-topology
/// genomes with input + bias nodes fully connected to output nodes.
/// </summary>
internal sealed class PopulationFactory : IPopulationFactory
{
    private readonly NeatSharpOptions _options;

    public PopulationFactory(IOptions<NeatSharpOptions> options)
    {
        _options = options.Value;
    }

    public IReadOnlyList<Genome> CreateInitialPopulation(
        int populationSize,
        int inputCount,
        int outputCount,
        Random random,
        IInnovationTracker tracker)
    {
        // Build shared node template: inputs 0..I-1, bias I, outputs I+1..I+O
        var nodes = new NodeGene[inputCount + 1 + outputCount];
        for (int i = 0; i < inputCount; i++)
        {
            nodes[i] = new NodeGene(i, NodeType.Input);
        }

        int biasId = inputCount;
        nodes[biasId] = new NodeGene(biasId, NodeType.Bias);

        int firstOutputId = inputCount + 1;
        for (int i = 0; i < outputCount; i++)
        {
            nodes[firstOutputId + i] = new NodeGene(firstOutputId + i, NodeType.Output);
        }

        // Pre-compute connection topology (source, target) pairs and innovation numbers.
        // All genomes share the same topology, so the tracker dedup cache
        // ensures identical innovation numbers for all genomes.
        int sourceCount = inputCount + 1; // inputs + bias
        int connectionCount = sourceCount * outputCount;
        var connectionTemplates = new (int SourceId, int TargetId, int Innovation)[connectionCount];

        int idx = 0;
        for (int src = 0; src < sourceCount; src++)
        {
            for (int o = 0; o < outputCount; o++)
            {
                int targetId = firstOutputId + o;
                int innovation = tracker.GetConnectionInnovation(src, targetId);
                connectionTemplates[idx++] = (src, targetId, innovation);
            }
        }

        // Create each genome with the shared topology but randomized weights
        double weightMin = _options.Mutation.WeightMinValue;
        double weightRange = _options.Mutation.WeightMaxValue - weightMin;

        var genomes = new Genome[populationSize];
        for (int g = 0; g < populationSize; g++)
        {
            var connections = new ConnectionGene[connectionCount];
            for (int c = 0; c < connectionCount; c++)
            {
                var template = connectionTemplates[c];
                double weight = weightMin + random.NextDouble() * weightRange;
                connections[c] = new ConnectionGene(
                    template.Innovation,
                    template.SourceId,
                    template.TargetId,
                    weight,
                    IsEnabled: true);
            }

            genomes[g] = new Genome(nodes, connections);
        }

        return genomes;
    }
}
