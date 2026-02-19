using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using NeatSharp.Genetics;

namespace NeatSharp.Benchmarks;

/// <summary>
/// CPU evaluator benchmarks measuring batch evaluation throughput
/// across various population sizes using <see cref="FeedForwardNetworkBuilder"/>
/// and direct <see cref="IGenome.Activate"/> calls.
/// </summary>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class CpuEvaluatorBenchmarks
{
    [Params(150, 500, 1000, 5000)]
    public int PopulationSize { get; set; }

    private IReadOnlyList<IGenome> _genomes = null!;
    private double[] _fitnesses = null!;

    [GlobalSetup]
    public void Setup()
    {
        var random = new Random(42);
        var registry = new ActivationFunctionRegistry();
        var builder = new FeedForwardNetworkBuilder(registry);

        var genomes = new List<IGenome>(PopulationSize);
        for (int i = 0; i < PopulationSize; i++)
        {
            genomes.Add(builder.Build(CreateGenome(random)));
        }

        _genomes = genomes;
        _fitnesses = new double[PopulationSize];
    }

    [Benchmark]
    [BenchmarkCategory("CPU", "CI")]
    public void EvaluateBatch()
    {
        Span<double> output = stackalloc double[1];
        double[] inputs = [0.5, 0.3, 0.7, 0.1];
        for (int i = 0; i < _genomes.Count; i++)
        {
            _genomes[i].Activate(inputs, output);
            _fitnesses[i] = output[0];
        }
    }

    private static Genome CreateGenome(Random random)
    {
        // 4 inputs + bias -> 1 output (simple topology)
        var nodes = new NodeGene[]
        {
            new(0, NodeType.Input), new(1, NodeType.Input),
            new(2, NodeType.Input), new(3, NodeType.Input),
            new(4, NodeType.Bias), new(5, NodeType.Output)
        };
        var connections = new ConnectionGene[]
        {
            new(1, 0, 5, random.NextDouble() * 4.0 - 2.0, true),
            new(2, 1, 5, random.NextDouble() * 4.0 - 2.0, true),
            new(3, 2, 5, random.NextDouble() * 4.0 - 2.0, true),
            new(4, 3, 5, random.NextDouble() * 4.0 - 2.0, true),
            new(5, 4, 5, random.NextDouble() * 4.0 - 2.0, true),
        };
        return new Genome(nodes, connections);
    }
}
