namespace NeatSharp.Serialization;

/// <summary>
/// Serializable snapshot of a species at a generation boundary.
/// References genomes by index into the population array rather than by value.
/// </summary>
/// <param name="Id">The stable species identifier.</param>
/// <param name="RepresentativeIndex">Index of the representative genome in the population array.</param>
/// <param name="BestFitnessEver">Highest fitness ever achieved by any member of this species.</param>
/// <param name="GenerationsSinceImprovement">Consecutive generations without fitness improvement.</param>
/// <param name="MemberIndices">Indices of member genomes in the population array.</param>
/// <param name="MemberFitnesses">Fitness values corresponding to each member, in the same order as <paramref name="MemberIndices"/>.</param>
public record SpeciesCheckpoint(
    int Id,
    int RepresentativeIndex,
    double BestFitnessEver,
    int GenerationsSinceImprovement,
    IReadOnlyList<int> MemberIndices,
    IReadOnlyList<double> MemberFitnesses);
