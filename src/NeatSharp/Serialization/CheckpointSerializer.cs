using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NeatSharp.Exceptions;
using NeatSharp.Serialization.Dto;
using NeatSharp.Serialization.Migration;

namespace NeatSharp.Serialization;

/// <summary>
/// Serializes and deserializes <see cref="TrainingCheckpoint"/> instances using JSON format.
/// Performs schema version checking, optional migration, and structural validation on load.
/// </summary>
public sealed class CheckpointSerializer : ICheckpointSerializer
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    private readonly ICheckpointValidator _validator;
    private readonly ISchemaVersionMigrator _migrator;
    private readonly ILogger<CheckpointSerializer> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="CheckpointSerializer"/>.
    /// </summary>
    /// <param name="validator">The checkpoint validator for structural integrity checks.</param>
    /// <param name="migrator">The schema version migrator for handling older formats.</param>
    /// <param name="logger">The logger for observability events.</param>
    public CheckpointSerializer(
        ICheckpointValidator validator,
        ISchemaVersionMigrator migrator,
        ILogger<CheckpointSerializer> logger)
    {
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _migrator = migrator ?? throw new ArgumentNullException(nameof(migrator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Initializes a new instance of <see cref="CheckpointSerializer"/> without a logger.
    /// Uses <see cref="NullLogger{T}"/> for environments where logging is not configured.
    /// </summary>
    /// <param name="validator">The checkpoint validator for structural integrity checks.</param>
    /// <param name="migrator">The schema version migrator for handling older formats.</param>
    public CheckpointSerializer(ICheckpointValidator validator, ISchemaVersionMigrator migrator)
        : this(validator, migrator, NullLogger<CheckpointSerializer>.Instance)
    {
    }

    /// <inheritdoc />
    public async Task SaveAsync(Stream stream, TrainingCheckpoint checkpoint, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(checkpoint);

        var dto = CheckpointDto.ToDto(checkpoint);
        await JsonSerializer.SerializeAsync(stream, dto, SerializerOptions, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<TrainingCheckpoint> LoadAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);

        // Step 1: Read stream into a JsonDocument to check schema version
        JsonDocument jsonDoc;
        try
        {
            jsonDoc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (JsonException ex)
        {
            throw new CheckpointCorruptionException(
                [$"Failed to parse checkpoint: the stream does not contain valid JSON. {ex.Message}"], ex);
        }

        using var _ = jsonDoc;

        // Step 2: Read "schemaVersion" property from root
        string? schemaVersion = null;
        if (jsonDoc.RootElement.TryGetProperty("schemaVersion", out var versionElement))
        {
            schemaVersion = versionElement.GetString();
        }

        if (string.IsNullOrEmpty(schemaVersion))
        {
            SerializationLog.IncompatibleSchemaVersion(_logger, "unknown", SchemaVersion.Current);
            throw new SchemaVersionException("unknown", SchemaVersion.Current, isMigrationAvailable: false);
        }

        // Step 3: Check if version matches current
        if (schemaVersion != SchemaVersion.Current)
        {
            // Step 4: Check if migration is needed and available
            if (SchemaVersion.NeedsMigration(schemaVersion))
            {
                if (_migrator.CanMigrate(schemaVersion))
                {
                    using var migratedDoc = _migrator.Migrate(jsonDoc, schemaVersion);
                    return DeserializeAndValidate(migratedDoc);
                }

                SerializationLog.IncompatibleSchemaVersion(_logger, schemaVersion, SchemaVersion.Current);
                throw new SchemaVersionException(schemaVersion, SchemaVersion.Current, isMigrationAvailable: false);
            }

            // Version is not compatible
            SerializationLog.IncompatibleSchemaVersion(_logger, schemaVersion, SchemaVersion.Current);
            throw new SchemaVersionException(schemaVersion, SchemaVersion.Current, isMigrationAvailable: false);
        }

        // Step 5-8: Deserialize, map, validate, return
        return DeserializeAndValidate(jsonDoc);
    }

    private TrainingCheckpoint DeserializeAndValidate(JsonDocument jsonDoc)
    {
        // Step 5: Deserialize to CheckpointDto
        var dto = jsonDoc.Deserialize<CheckpointDto>(SerializerOptions)
            ?? throw new CheckpointCorruptionException(["Failed to deserialize checkpoint: result was null."]);

        // Step 6: Map to TrainingCheckpoint
        var checkpoint = dto.ToDomain();

        // Step 7: Validate
        var validationResult = _validator.Validate(checkpoint);

        // Step 8: If validation fails, log error and throw
        if (!validationResult.IsValid)
        {
            SerializationLog.CheckpointValidationFailed(_logger, validationResult.Errors.Count);
            throw new CheckpointCorruptionException(validationResult.Errors);
        }

        // Step 9: Check configuration hash for mismatch (warning only, doesn't prevent loading)
        var computedHash = ConfigurationHasher.ComputeHash(checkpoint.Configuration);
        if (!string.Equals(checkpoint.ConfigurationHash, computedHash, StringComparison.Ordinal))
        {
            SerializationLog.ConfigHashMismatch(_logger, checkpoint.ConfigurationHash, computedHash);
        }

        // Step 10: Return validated checkpoint
        return checkpoint;
    }
}
