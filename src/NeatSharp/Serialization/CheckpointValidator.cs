namespace NeatSharp.Serialization;

/// <summary>
/// Validates the structural integrity of a <see cref="TrainingCheckpoint"/>
/// by checking cross-references between population, species, and counters.
/// Collects all errors into a single result rather than stopping at the first error.
/// </summary>
public sealed class CheckpointValidator : ICheckpointValidator
{
    /// <inheritdoc />
    public CheckpointValidationResult Validate(TrainingCheckpoint checkpoint)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);

        var errors = new List<string>();

        ValidateSpeciesIndices(checkpoint, errors);
        ValidateCounterConsistency(checkpoint, errors);
        ValidateChampionInPopulation(checkpoint, errors);

        return new CheckpointValidationResult(errors);
    }

    private static void ValidateSpeciesIndices(TrainingCheckpoint checkpoint, List<string> errors)
    {
        int populationSize = checkpoint.Population.Count;

        for (int s = 0; s < checkpoint.Species.Count; s++)
        {
            var species = checkpoint.Species[s];

            if (species.RepresentativeIndex < 0 || species.RepresentativeIndex >= populationSize)
            {
                errors.Add(
                    $"Species {species.Id} has representative index {species.RepresentativeIndex} " +
                    $"which is out of range for population size {populationSize}.");
            }

            for (int m = 0; m < species.MemberIndices.Count; m++)
            {
                int memberIndex = species.MemberIndices[m];
                if (memberIndex < 0 || memberIndex >= populationSize)
                {
                    errors.Add(
                        $"Species {species.Id} has member index {memberIndex} " +
                        $"which is out of range for population size {populationSize}.");
                }
            }
        }
    }

    private static void ValidateCounterConsistency(TrainingCheckpoint checkpoint, List<string> errors)
    {
        if (checkpoint.Population.Count > 0)
        {
            int maxInnovationNumber = 0;
            int maxNodeId = 0;

            foreach (var genome in checkpoint.Population)
            {
                foreach (var connection in genome.Connections)
                {
                    if (connection.InnovationNumber > maxInnovationNumber)
                    {
                        maxInnovationNumber = connection.InnovationNumber;
                    }
                }

                foreach (var node in genome.Nodes)
                {
                    if (node.Id > maxNodeId)
                    {
                        maxNodeId = node.Id;
                    }
                }
            }

            if (checkpoint.NextInnovationNumber <= maxInnovationNumber)
            {
                errors.Add(
                    $"NextInnovationNumber ({checkpoint.NextInnovationNumber}) must be strictly greater than " +
                    $"the maximum innovation number ({maxInnovationNumber}) across all genomes.");
            }

            if (checkpoint.NextNodeId <= maxNodeId)
            {
                errors.Add(
                    $"NextNodeId ({checkpoint.NextNodeId}) must be strictly greater than " +
                    $"the maximum node ID ({maxNodeId}) across all genomes.");
            }
        }

        if (checkpoint.Species.Count > 0)
        {
            int maxSpeciesId = 0;

            foreach (var species in checkpoint.Species)
            {
                if (species.Id > maxSpeciesId)
                {
                    maxSpeciesId = species.Id;
                }
            }

            if (checkpoint.NextSpeciesId <= maxSpeciesId)
            {
                errors.Add(
                    $"NextSpeciesId ({checkpoint.NextSpeciesId}) must be strictly greater than " +
                    $"the maximum species ID ({maxSpeciesId}) across all species.");
            }
        }
    }

    private static void ValidateChampionInPopulation(TrainingCheckpoint checkpoint, List<string> errors)
    {
        // Skip champion check if population is empty (extinct population scenario)
        if (checkpoint.Population.Count == 0)
        {
            return;
        }

        // Check reference equality first
        foreach (var genome in checkpoint.Population)
        {
            if (ReferenceEquals(genome, checkpoint.ChampionGenome))
            {
                return;
            }
        }

        // Fall back to structural equality
        foreach (var genome in checkpoint.Population)
        {
            if (GenomesAreStructurallyEqual(genome, checkpoint.ChampionGenome))
            {
                return;
            }
        }

        errors.Add("Champion genome is not found in the population.");
    }

    private static bool GenomesAreStructurallyEqual(Genetics.Genome a, Genetics.Genome b)
    {
        if (a.Nodes.Count != b.Nodes.Count || a.Connections.Count != b.Connections.Count)
        {
            return false;
        }

        for (int i = 0; i < a.Nodes.Count; i++)
        {
            if (a.Nodes[i] != b.Nodes[i])
            {
                return false;
            }
        }

        for (int i = 0; i < a.Connections.Count; i++)
        {
            if (a.Connections[i] != b.Connections[i])
            {
                return false;
            }
        }

        return true;
    }
}
