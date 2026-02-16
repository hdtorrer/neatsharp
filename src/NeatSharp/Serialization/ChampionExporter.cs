using System.Text.Json;
using System.Text.Json.Serialization;
using NeatSharp.Evolution;
using NeatSharp.Serialization.Dto;

namespace NeatSharp.Serialization;

/// <summary>
/// Exports the champion genome as a self-describing JSON graph suitable for
/// interoperability with external tools. The output is parseable by any standard
/// JSON reader without requiring the NEATSharp library.
/// </summary>
public sealed class ChampionExporter : IChampionExporter
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    /// <inheritdoc />
    public async Task ExportAsync(Stream stream, EvolutionResult result, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(result);

        var genotype = result.Champion.Genotype
            ?? throw new InvalidOperationException(
                "Champion genotype is not available on the EvolutionResult. " +
                "Ensure the Champion was constructed with a Genotype, or use the " +
                "ExportAsync(Stream, TrainingCheckpoint, CancellationToken) overload instead.");

        var metadata = BuildMetadata(result.Seed, configurationHash: string.Empty);

        var dto = ChampionExportDto.FromGenome(
            genotype,
            result.Champion.Fitness,
            result.Champion.Generation,
            metadata);

        await JsonSerializer.SerializeAsync(stream, dto, SerializerOptions, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task ExportAsync(Stream stream, TrainingCheckpoint checkpoint, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(checkpoint);

        var metadata = BuildMetadata(checkpoint.Seed, checkpoint.ConfigurationHash);

        var dto = ChampionExportDto.FromGenome(
            checkpoint.ChampionGenome,
            checkpoint.ChampionFitness,
            checkpoint.ChampionGeneration,
            metadata);

        await JsonSerializer.SerializeAsync(stream, dto, SerializerOptions, cancellationToken).ConfigureAwait(false);
    }

    private static ArtifactMetadata BuildMetadata(int seed, string configurationHash)
    {
        var libraryVersion = LibraryInfo.Version;
        return new ArtifactMetadata(
            SchemaVersion: SchemaVersion.Current,
            LibraryVersion: libraryVersion,
            Seed: seed,
            ConfigurationHash: configurationHash,
            CreatedAtUtc: DateTime.UtcNow.ToString("O"),
            Environment: EnvironmentInfo.CreateCurrent());
    }
}
