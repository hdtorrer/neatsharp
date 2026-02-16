using NeatSharp.Configuration;
using NeatSharp.Genetics;
using NeatSharp.Reporting;

namespace NeatSharp.Serialization.Dto;

/// <summary>
/// Top-level data transfer object for checkpoint serialization.
/// Contains the complete state needed to resume an evolution run.
/// </summary>
public class CheckpointDto
{
    /// <summary>
    /// Gets or sets the schema version of the checkpoint format.
    /// </summary>
    public string SchemaVersion { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the artifact metadata.
    /// </summary>
    public ArtifactMetadataDto Metadata { get; set; } = new();

    /// <summary>
    /// Gets or sets the list of genome DTOs in the population.
    /// </summary>
    public List<GenomeDto> Population { get; set; } = [];

    /// <summary>
    /// Gets or sets the species checkpoint DTOs.
    /// </summary>
    public List<SpeciesCheckpointDto> Species { get; set; } = [];

    /// <summary>
    /// Gets or sets the counter state.
    /// </summary>
    public CountersDto Counters { get; set; } = new();

    /// <summary>
    /// Gets or sets the champion information.
    /// </summary>
    public ChampionDto Champion { get; set; } = new();

    /// <summary>
    /// Gets or sets the current generation number.
    /// </summary>
    public int Generation { get; set; }

    /// <summary>
    /// Gets or sets the random seed.
    /// </summary>
    public int Seed { get; set; }

    /// <summary>
    /// Gets or sets the RNG state DTO.
    /// </summary>
    public RngStateDto RngState { get; set; } = new();

    /// <summary>
    /// Gets or sets the configuration. Serialized directly as a plain POCO.
    /// </summary>
    public NeatSharpOptions Configuration { get; set; } = new();

    /// <summary>
    /// Gets or sets the configuration hash.
    /// </summary>
    public string ConfigurationHash { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the run history. Serialized directly as a record.
    /// </summary>
    public RunHistory History { get; set; } = new([], 0);

    /// <summary>
    /// Maps a <see cref="TrainingCheckpoint"/> domain object to a <see cref="CheckpointDto"/>.
    /// </summary>
    /// <param name="checkpoint">The training checkpoint to map.</param>
    /// <returns>A new DTO representing the checkpoint.</returns>
    public static CheckpointDto ToDto(TrainingCheckpoint checkpoint)
    {
        // Find champion genome index in population
        int championIndex = -1;
        for (int i = 0; i < checkpoint.Population.Count; i++)
        {
            if (ReferenceEquals(checkpoint.Population[i], checkpoint.ChampionGenome))
            {
                championIndex = i;
                break;
            }
        }

        // If reference equality fails, fall back to structural comparison
        if (championIndex == -1)
        {
            for (int i = 0; i < checkpoint.Population.Count; i++)
            {
                var genome = checkpoint.Population[i];
                if (GenomesAreEqual(genome, checkpoint.ChampionGenome))
                {
                    championIndex = i;
                    break;
                }
            }
        }

        // If champion is not in population (e.g., empty/extinct population),
        // serialize the champion genome separately
        GenomeDto? championGenomeDto = championIndex == -1
            ? GenomeDto.ToDto(checkpoint.ChampionGenome)
            : null;

        return new CheckpointDto
        {
            SchemaVersion = Serialization.SchemaVersion.Current,
            Metadata = ArtifactMetadataDto.ToDto(checkpoint.Metadata),
            Population = checkpoint.Population.Select(GenomeDto.ToDto).ToList(),
            Species = checkpoint.Species.Select(SpeciesCheckpointDto.ToDto).ToList(),
            Counters = new CountersDto
            {
                NextInnovationNumber = checkpoint.NextInnovationNumber,
                NextNodeId = checkpoint.NextNodeId,
                NextSpeciesId = checkpoint.NextSpeciesId
            },
            Champion = new ChampionDto
            {
                GenomeIndex = championIndex,
                Fitness = checkpoint.ChampionFitness,
                Generation = checkpoint.ChampionGeneration,
                Genome = championGenomeDto
            },
            Generation = checkpoint.Generation,
            Seed = checkpoint.Seed,
            RngState = RngStateDto.ToDto(checkpoint.RngState),
            Configuration = checkpoint.Configuration,
            ConfigurationHash = checkpoint.ConfigurationHash,
            History = checkpoint.History
        };
    }

    /// <summary>
    /// Maps this DTO back to a <see cref="TrainingCheckpoint"/> domain object.
    /// </summary>
    /// <returns>A new <see cref="TrainingCheckpoint"/> instance.</returns>
    public TrainingCheckpoint ToDomain()
    {
        var population = Population.Select(g => g.ToDomain()).ToArray();
        Genome championGenome;
        if (Champion.GenomeIndex >= 0 && Champion.GenomeIndex < population.Length)
        {
            championGenome = population[Champion.GenomeIndex];
        }
        else if (Champion.Genome is not null)
        {
            championGenome = Champion.Genome.ToDomain();
        }
        else if (population.Length > 0)
        {
            championGenome = population[0];
        }
        else
        {
            throw new InvalidOperationException(
                "Cannot determine champion genome: population is empty and no standalone champion genome was serialized.");
        }

        return new TrainingCheckpoint(
            Population: population,
            Species: Species.Select(s => s.ToDomain()).ToArray(),
            NextInnovationNumber: Counters.NextInnovationNumber,
            NextNodeId: Counters.NextNodeId,
            NextSpeciesId: Counters.NextSpeciesId,
            ChampionGenome: championGenome,
            ChampionFitness: Champion.Fitness,
            ChampionGeneration: Champion.Generation,
            Generation: Generation,
            Seed: Seed,
            RngState: RngState.ToDomain(),
            Configuration: Configuration,
            ConfigurationHash: ConfigurationHash,
            History: History,
            Metadata: Metadata.ToDomain());
    }

    private static bool GenomesAreEqual(Genome a, Genome b)
    {
        if (a.Nodes.Count != b.Nodes.Count || a.Connections.Count != b.Connections.Count)
            return false;

        for (int i = 0; i < a.Nodes.Count; i++)
        {
            if (a.Nodes[i] != b.Nodes[i])
                return false;
        }

        for (int i = 0; i < a.Connections.Count; i++)
        {
            if (a.Connections[i] != b.Connections[i])
                return false;
        }

        return true;
    }
}

/// <summary>
/// Nested DTO for innovation and node ID counters.
/// </summary>
public class CountersDto
{
    /// <summary>
    /// Gets or sets the next innovation number.
    /// </summary>
    public int NextInnovationNumber { get; set; }

    /// <summary>
    /// Gets or sets the next node ID.
    /// </summary>
    public int NextNodeId { get; set; }

    /// <summary>
    /// Gets or sets the next species ID.
    /// </summary>
    public int NextSpeciesId { get; set; }
}

/// <summary>
/// Nested DTO for champion genome information.
/// </summary>
public class ChampionDto
{
    /// <summary>
    /// Gets or sets the index of the champion genome in the population array.
    /// A value of -1 indicates the champion is not in the population (e.g., extinct population).
    /// </summary>
    public int GenomeIndex { get; set; }

    /// <summary>
    /// Gets or sets the champion's fitness.
    /// </summary>
    public double Fitness { get; set; }

    /// <summary>
    /// Gets or sets the generation in which the champion was found.
    /// </summary>
    public int Generation { get; set; }

    /// <summary>
    /// Gets or sets the standalone champion genome, used when the champion is not in the population
    /// (e.g., empty/extinct population). Null when the champion is referenced by <see cref="GenomeIndex"/>.
    /// </summary>
    public GenomeDto? Genome { get; set; }
}
