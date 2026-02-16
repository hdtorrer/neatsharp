namespace NeatSharp.Serialization.Dto;

/// <summary>
/// Data transfer object for <see cref="SpeciesCheckpoint"/> serialization.
/// </summary>
public class SpeciesCheckpointDto
{
    /// <summary>
    /// Gets or sets the stable species identifier.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the index of the representative genome in the population array.
    /// </summary>
    public int RepresentativeIndex { get; set; }

    /// <summary>
    /// Gets or sets the highest fitness ever achieved by this species.
    /// </summary>
    public double BestFitnessEver { get; set; }

    /// <summary>
    /// Gets or sets the consecutive generations without fitness improvement.
    /// </summary>
    public int GenerationsSinceImprovement { get; set; }

    /// <summary>
    /// Gets or sets the indices of member genomes in the population array.
    /// </summary>
    public List<int> MemberIndices { get; set; } = [];

    /// <summary>
    /// Gets or sets the fitness values for each member in the same order as <see cref="MemberIndices"/>.
    /// </summary>
    public List<double> MemberFitnesses { get; set; } = [];

    /// <summary>
    /// Maps a <see cref="SpeciesCheckpoint"/> domain object to a <see cref="SpeciesCheckpointDto"/>.
    /// </summary>
    /// <param name="species">The species checkpoint to map.</param>
    /// <returns>A new DTO representing the species checkpoint.</returns>
    public static SpeciesCheckpointDto ToDto(SpeciesCheckpoint species) => new()
    {
        Id = species.Id,
        RepresentativeIndex = species.RepresentativeIndex,
        BestFitnessEver = species.BestFitnessEver,
        GenerationsSinceImprovement = species.GenerationsSinceImprovement,
        MemberIndices = species.MemberIndices.ToList(),
        MemberFitnesses = species.MemberFitnesses.ToList()
    };

    /// <summary>
    /// Maps this DTO back to a <see cref="SpeciesCheckpoint"/> domain object.
    /// </summary>
    /// <returns>A new <see cref="SpeciesCheckpoint"/> instance.</returns>
    public SpeciesCheckpoint ToDomain() => new(
        Id,
        RepresentativeIndex,
        BestFitnessEver,
        GenerationsSinceImprovement,
        MemberIndices.AsReadOnly(),
        MemberFitnesses.AsReadOnly());
}
