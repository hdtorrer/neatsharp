using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using NeatSharp.Configuration;
using NeatSharp.Evaluation;
using NeatSharp.Genetics;

namespace NeatSharp.Benchmarks;

/// <summary>
/// Benchmarks comparing parallel vs. sequential synchronous evaluation
/// for a CPU-bound fitness function. Validates SC-001: parallel evaluation
/// wall-clock time should be significantly less than sequential for large populations.
/// </summary>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class ParallelEvaluationBenchmark
{
    [Params(100, 1000)]
    public int PopulationSize { get; set; }

    private IReadOnlyList<IGenome> _genomes = null!;
    private double[] _fitnesses = null!;
    private IEvaluationStrategy _sequentialStrategy = null!;
    private IEvaluationStrategy _parallelStrategy = null!;

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

        Func<IGenome, double> fitnessFunction = CpuBoundFitness;

        var sequentialOptions = new EvaluationOptions { MaxDegreeOfParallelism = 1 };
        _sequentialStrategy = EvaluationStrategy.FromFunction(fitnessFunction, sequentialOptions);

        var parallelOptions = new EvaluationOptions { MaxDegreeOfParallelism = null };
        _parallelStrategy = EvaluationStrategy.FromFunction(fitnessFunction, parallelOptions);
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("CPU")]
    public async Task Sequential()
    {
        await _sequentialStrategy.EvaluatePopulationAsync(
            _genomes,
            (index, fitness) => _fitnesses[index] = fitness,
            CancellationToken.None);
    }

    [Benchmark]
    [BenchmarkCategory("CPU")]
    public async Task Parallel()
    {
        await _parallelStrategy.EvaluatePopulationAsync(
            _genomes,
            (index, fitness) => _fitnesses[index] = fitness,
            CancellationToken.None);
    }

    /// <summary>
    /// CPU-bound fitness function that activates the genome with sample inputs
    /// and computes a fitness score from the output.
    /// </summary>
    private static double CpuBoundFitness(IGenome genome)
    {
        Span<double> output = stackalloc double[1];
        double[] inputs = [0.5, 0.3, 0.7, 0.1];

        // Perform multiple activations to simulate a meaningful CPU workload
        double sum = 0.0;
        for (int i = 0; i < 100; i++)
        {
            genome.Activate(inputs, output);
            sum += output[0];
        }

        return sum;
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
