using NeatSharp.Configuration;
using NeatSharp.Genetics;
using NeatSharp.Reporting;

namespace NeatSharp.Serialization;

/// <summary>
/// Complete snapshot of an evolution run at a generation boundary.
/// Contains all state needed to resume training deterministically.
/// </summary>
/// <param name="Population">All genomes in the current population.</param>
/// <param name="Species">Species snapshots referencing genomes by population index.</param>
/// <param name="NextInnovationNumber">Next innovation number counter for the innovation tracker.</param>
/// <param name="NextNodeId">Next node ID counter for the innovation tracker.</param>
/// <param name="NextSpeciesId">Next species ID counter for the speciation strategy.</param>
/// <param name="ChampionGenome">The best genome found so far.</param>
/// <param name="ChampionFitness">Fitness of the champion genome.</param>
/// <param name="ChampionGeneration">Generation in which the champion was found.</param>
/// <param name="Generation">Current generation number.</param>
/// <param name="Seed">Random seed used for the evolution run.</param>
/// <param name="RngState">Internal state of the random number generator.</param>
/// <param name="Configuration">The full configuration used for this run.</param>
/// <param name="ConfigurationHash">SHA-256 hash of the serialized configuration.</param>
/// <param name="History">Generation-by-generation run history.</param>
/// <param name="Metadata">Artifact metadata including schema version and environment info.</param>
public record TrainingCheckpoint(
    IReadOnlyList<Genome> Population,
    IReadOnlyList<SpeciesCheckpoint> Species,
    int NextInnovationNumber,
    int NextNodeId,
    int NextSpeciesId,
    Genome ChampionGenome,
    double ChampionFitness,
    int ChampionGeneration,
    int Generation,
    int Seed,
    RngState RngState,
    NeatSharpOptions Configuration,
    string ConfigurationHash,
    RunHistory History,
    ArtifactMetadata Metadata);
