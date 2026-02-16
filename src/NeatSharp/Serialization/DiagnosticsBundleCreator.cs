using System.Text.Json;
using System.Text.Json.Serialization;
using NeatSharp.Serialization.Dto;

namespace NeatSharp.Serialization;

/// <summary>
/// Creates a diagnostics bundle containing the full checkpoint, configuration,
/// environment metadata, and run history in a single JSON document suitable for
/// bug reports and troubleshooting.
/// </summary>
public sealed class DiagnosticsBundleCreator : IDiagnosticsBundleCreator
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    /// <inheritdoc />
    public async Task CreateAsync(Stream stream, TrainingCheckpoint checkpoint, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(checkpoint);

        var dto = DiagnosticsBundleDto.ToDto(checkpoint);
        await JsonSerializer.SerializeAsync(stream, dto, SerializerOptions, cancellationToken).ConfigureAwait(false);
    }
}
