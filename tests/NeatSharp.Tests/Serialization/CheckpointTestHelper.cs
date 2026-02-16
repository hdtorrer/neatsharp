using NeatSharp.Configuration;
using NeatSharp.Genetics;
using NeatSharp.Reporting;
using NeatSharp.Serialization;

namespace NeatSharp.Tests.Serialization;

/// <summary>
/// Shared helper for building valid TrainingCheckpoint instances in tests.
/// </summary>
internal static class CheckpointTestHelper
{
    /// <summary>
    /// Creates a minimal valid genome with 1 input, 1 output, and 1 connection.
    /// </summary>
    public static Genome CreateMinimalGenome(int inputId = 0, int outputId = 1, int innovationNumber = 1)
    {
        return new Genome(
            [new NodeGene(inputId, NodeType.Input), new NodeGene(outputId, NodeType.Output)],
            [new ConnectionGene(innovationNumber, inputId, outputId, 0.5, true)]);
    }

    /// <summary>
    /// Creates a genome with a hidden node for richer testing.
    /// </summary>
    public static Genome CreateGenomeWithHiddenNode(
        int inputId = 0,
        int outputId = 1,
        int hiddenId = 2,
        int innov1 = 1,
        int innov2 = 2,
        int innov3 = 3)
    {
        return new Genome(
            [
                new NodeGene(inputId, NodeType.Input),
                new NodeGene(outputId, NodeType.Output),
                new NodeGene(hiddenId, NodeType.Hidden, "tanh")
            ],
            [
                new ConnectionGene(innov1, inputId, hiddenId, 0.7, true),
                new ConnectionGene(innov2, hiddenId, outputId, -0.3, true),
                new ConnectionGene(innov3, inputId, outputId, 0.1, false)
            ]);
    }

    /// <summary>
    /// Creates a complete, valid TrainingCheckpoint with known data for round-trip testing.
    /// </summary>
    public static TrainingCheckpoint CreateFullCheckpoint()
    {
        var genome1 = CreateMinimalGenome(0, 1, 1);
        var genome2 = CreateGenomeWithHiddenNode(0, 1, 2, 1, 2, 3);
        var population = new List<Genome> { genome1, genome2 };

        var species = new List<SpeciesCheckpoint>
        {
            new(
                Id: 1,
                RepresentativeIndex: 0,
                BestFitnessEver: 0.95,
                GenerationsSinceImprovement: 3,
                MemberIndices: [0, 1],
                MemberFitnesses: [0.8, 0.95])
        };

        var rng = new Random(42);
        var rngState = RngStateHelper.Capture(rng);

        var config = new NeatSharpOptions
        {
            InputCount = 2,
            OutputCount = 1,
            PopulationSize = 150,
            Seed = 42,
            Stopping = new StoppingCriteria { MaxGenerations = 100 }
        };

        var configHash = ConfigurationHasher.ComputeHash(config);

        var history = new RunHistory(
            [
                new GenerationStatistics(
                    Generation: 0,
                    BestFitness: 0.8,
                    AverageFitness: 0.5,
                    SpeciesCount: 1,
                    SpeciesSizes: [2],
                    Complexity: new ComplexityStatistics(2.5, 2.0),
                    Timing: new TimingBreakdown(
                        TimeSpan.FromMilliseconds(10),
                        TimeSpan.FromMilliseconds(5),
                        TimeSpan.FromMilliseconds(2)))
            ],
            TotalGenerations: 1);

        var metadata = new ArtifactMetadata(
            SchemaVersion: SchemaVersion.Current,
            LibraryVersion: "1.0.0",
            Seed: 42,
            ConfigurationHash: configHash,
            CreatedAtUtc: "2026-02-15T12:00:00Z",
            Environment: new EnvironmentInfo("Windows 10", ".NET 8.0.0", "X64"));

        return new TrainingCheckpoint(
            Population: population,
            Species: species,
            NextInnovationNumber: 4,
            NextNodeId: 3,
            NextSpeciesId: 2,
            ChampionGenome: genome2,
            ChampionFitness: 0.95,
            ChampionGeneration: 0,
            Generation: 1,
            Seed: 42,
            RngState: rngState,
            Configuration: config,
            ConfigurationHash: configHash,
            History: history,
            Metadata: metadata);
    }

    /// <summary>
    /// Creates a checkpoint with an empty population and empty species list.
    /// The champion genome is still valid (minimal genome not in population).
    /// </summary>
    public static TrainingCheckpoint CreateEmptyPopulationCheckpoint()
    {
        var championGenome = CreateMinimalGenome(0, 1, 1);

        var rng = new Random(99);
        var rngState = RngStateHelper.Capture(rng);

        var config = new NeatSharpOptions
        {
            InputCount = 2,
            OutputCount = 1,
            PopulationSize = 150,
            Seed = 99,
            Stopping = new StoppingCriteria { MaxGenerations = 50 }
        };

        var configHash = ConfigurationHasher.ComputeHash(config);

        var metadata = new ArtifactMetadata(
            SchemaVersion: SchemaVersion.Current,
            LibraryVersion: "1.0.0",
            Seed: 99,
            ConfigurationHash: configHash,
            CreatedAtUtc: "2026-02-15T13:00:00Z",
            Environment: new EnvironmentInfo("Linux", ".NET 9.0.0", "Arm64"));

        return new TrainingCheckpoint(
            Population: Array.Empty<Genome>(),
            Species: Array.Empty<SpeciesCheckpoint>(),
            NextInnovationNumber: 2,
            NextNodeId: 2,
            NextSpeciesId: 1,
            ChampionGenome: championGenome,
            ChampionFitness: 0.5,
            ChampionGeneration: 0,
            Generation: 5,
            Seed: 99,
            RngState: rngState,
            Configuration: config,
            ConfigurationHash: configHash,
            History: new RunHistory([], 5),
            Metadata: metadata);
    }
}
