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
    private const long MaxCheckpointBytes = 100 * 1024 * 1024; // 100MB

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    private static readonly JsonDocumentOptions DocOptions = new() { MaxDepth = 64 };

    private static readonly string SchemaVersionPropertyName =
        SerializerOptions.PropertyNamingPolicy?.ConvertName(nameof(CheckpointDto.SchemaVersion))
        ?? nameof(CheckpointDto.SchemaVersion);

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

        // Step 1: Buffer the stream with size limit protection
        var buffer = await ReadStreamWithLimitAsync(stream, MaxCheckpointBytes, cancellationToken).ConfigureAwait(false);

        // Step 2: Peek at schema version without full DOM parse
        var schemaVersion = PeekSchemaVersion(buffer);

        if (string.IsNullOrEmpty(schemaVersion))
        {
            SerializationLog.IncompatibleSchemaVersion(_logger, "unknown", SchemaVersion.Current);
            throw new SchemaVersionException("unknown", SchemaVersion.Current, isMigrationAvailable: false);
        }

        // Step 3: Happy path — version matches current, single-pass deserialization
        if (schemaVersion == SchemaVersion.Current)
        {
            return DeserializeAndValidate(buffer);
        }

        // Step 4: Check if migration is needed and available
        if (SchemaVersion.NeedsMigration(schemaVersion))
        {
            if (_migrator.CanMigrate(schemaVersion))
            {
                using var jsonDoc = JsonDocument.Parse(buffer, DocOptions);
                using var migratedDoc = _migrator.Migrate(jsonDoc, schemaVersion);
                return DeserializeAndValidateFromDoc(migratedDoc);
            }

            SerializationLog.IncompatibleSchemaVersion(_logger, schemaVersion, SchemaVersion.Current);
            throw new SchemaVersionException(schemaVersion, SchemaVersion.Current, isMigrationAvailable: false);
        }

        // Version is not compatible
        SerializationLog.IncompatibleSchemaVersion(_logger, schemaVersion, SchemaVersion.Current);
        throw new SchemaVersionException(schemaVersion, SchemaVersion.Current, isMigrationAvailable: false);
    }

    /// <summary>
    /// Single-pass deserialization from a byte buffer (happy path — no DOM parse needed).
    /// </summary>
    private TrainingCheckpoint DeserializeAndValidate(byte[] buffer)
    {
        CheckpointDto dto;
        try
        {
            using var memStream = new MemoryStream(buffer, writable: false);
            dto = JsonSerializer.Deserialize<CheckpointDto>(memStream, SerializerOptions)
                ?? throw new CheckpointCorruptionException(["Failed to deserialize checkpoint: result was null."]);
        }
        catch (CheckpointCorruptionException) { throw; }
        catch (JsonException ex)
        {
            throw new CheckpointCorruptionException(
                [$"Failed to parse checkpoint JSON: {ex.Message}"], ex);
        }

        return MapValidateAndReturn(dto);
    }

    /// <summary>
    /// Deserialization from a JsonDocument (migration path).
    /// </summary>
    private TrainingCheckpoint DeserializeAndValidateFromDoc(JsonDocument jsonDoc)
    {
        CheckpointDto dto;
        try
        {
            dto = jsonDoc.Deserialize<CheckpointDto>(SerializerOptions)
                ?? throw new CheckpointCorruptionException(["Failed to deserialize checkpoint: result was null."]);
        }
        catch (CheckpointCorruptionException) { throw; }
        catch (JsonException ex)
        {
            throw new CheckpointCorruptionException(
                [$"Failed to parse checkpoint JSON: {ex.Message}"], ex);
        }

        return MapValidateAndReturn(dto);
    }

    /// <summary>
    /// Maps a DTO to a domain checkpoint, validates, and checks configuration hash.
    /// </summary>
    private TrainingCheckpoint MapValidateAndReturn(CheckpointDto dto)
    {
        TrainingCheckpoint checkpoint;
        try
        {
            checkpoint = dto.ToDomain();
        }
        catch (CheckpointCorruptionException) { throw; }
        catch (Exception ex)
        {
            throw new CheckpointCorruptionException(
                [$"Failed to map checkpoint data: {ex.Message}"], ex);
        }

        var validationResult = _validator.Validate(checkpoint);
        if (!validationResult.IsValid)
        {
            SerializationLog.CheckpointValidationFailed(_logger, validationResult.Errors.Count);
            throw new CheckpointCorruptionException(validationResult.Errors);
        }

        var computedHash = ConfigurationHasher.ComputeHash(checkpoint.Configuration);
        if (!string.Equals(checkpoint.ConfigurationHash, computedHash, StringComparison.Ordinal))
        {
            SerializationLog.ConfigHashMismatch(_logger, checkpoint.ConfigurationHash, computedHash);
        }

        return checkpoint;
    }

    /// <summary>
    /// Peeks at the schema version from a JSON buffer without a full DOM parse.
    /// Uses Utf8JsonReader to scan top-level properties efficiently.
    /// </summary>
    private static string? PeekSchemaVersion(byte[] buffer)
    {
        try
        {
            var reader = new Utf8JsonReader(buffer.AsSpan(), new JsonReaderOptions { MaxDepth = 64 });

            if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
            {
                return null;
            }

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                {
                    break;
                }

                if (reader.TokenType == JsonTokenType.PropertyName)
                {
                    if (reader.ValueTextEquals(SchemaVersionPropertyName))
                    {
                        return reader.Read() && reader.TokenType == JsonTokenType.String
                            ? reader.GetString()
                            : null;
                    }

                    // Skip the value of this non-matching property
                    reader.Read();
                    reader.TrySkip();
                }
            }
        }
        catch (JsonException)
        {
            // Malformed JSON — return null; the full deserializer will produce
            // a proper CheckpointCorruptionException with details
        }

        return null;
    }

    /// <summary>
    /// Reads the entire stream into a byte array, enforcing a maximum size limit.
    /// </summary>
    private static async Task<byte[]> ReadStreamWithLimitAsync(
        Stream stream, long maxBytes, CancellationToken cancellationToken)
    {
        if (stream.CanSeek && stream.Length > maxBytes)
        {
            throw new CheckpointCorruptionException(
                [$"Checkpoint stream exceeds the maximum allowed size of {maxBytes:N0} bytes (actual: {stream.Length:N0} bytes)."]);
        }

        using var memoryStream = new MemoryStream();
        var readBuffer = new byte[81920];
        long totalRead = 0;
        int bytesRead;

        while ((bytesRead = await stream.ReadAsync(readBuffer, cancellationToken).ConfigureAwait(false)) > 0)
        {
            totalRead += bytesRead;
            if (totalRead > maxBytes)
            {
                throw new CheckpointCorruptionException(
                    [$"Checkpoint stream exceeds the maximum allowed size of {maxBytes:N0} bytes."]);
            }

            memoryStream.Write(readBuffer, 0, bytesRead);
        }

        return memoryStream.ToArray();
    }
}
