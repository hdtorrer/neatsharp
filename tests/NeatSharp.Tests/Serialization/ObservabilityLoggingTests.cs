using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NeatSharp.Exceptions;
using NeatSharp.Serialization;
using NeatSharp.Serialization.Migration;
using Xunit;

namespace NeatSharp.Tests.Serialization;

/// <summary>
/// Tests verifying that CheckpointSerializer emits appropriate log messages
/// for observability: warnings on config hash mismatch, errors on corruption
/// and incompatible schema versions, and no info-level logs during normal operations.
/// </summary>
public class ObservabilityLoggingTests
{
    private readonly CapturingLogger<CheckpointSerializer> _logger;
    private readonly CheckpointSerializer _serializer;

    public ObservabilityLoggingTests()
    {
        _logger = new CapturingLogger<CheckpointSerializer>();
        var validator = new CheckpointValidator();
        var migrator = new SchemaVersionMigrator();
        _serializer = new CheckpointSerializer(validator, migrator, _logger);
    }

    [Fact]
    public async Task LoadAsync_ConfigHashMismatch_LogsWarning()
    {
        // Arrange: create a valid checkpoint, save it, then modify the ConfigurationHash
        var original = CheckpointTestHelper.CreateFullCheckpoint();

        using var stream = new MemoryStream();
        await _serializer.SaveAsync(stream, original);

        // Tamper with the configurationHash in the JSON
        var json = Encoding.UTF8.GetString(stream.ToArray());
        var tamperedJson = json.Replace(
            original.ConfigurationHash,
            "0000000000000000000000000000000000000000000000000000000000000000");

        using var tamperedStream = new MemoryStream(Encoding.UTF8.GetBytes(tamperedJson));

        // Act
        await _serializer.LoadAsync(tamperedStream);

        // Assert: a Warning-level log should have been emitted about the hash mismatch
        _logger.LogEntries.Should().Contain(entry =>
            entry.Level == LogLevel.Warning &&
            entry.Message.Contains("Configuration hash mismatch"));
    }

    [Fact]
    public async Task LoadAsync_ValidationFailure_LogsError()
    {
        // Arrange: create a checkpoint with NextInnovationNumber too low (will fail validation)
        var original = CheckpointTestHelper.CreateFullCheckpoint();

        using var stream = new MemoryStream();
        await _serializer.SaveAsync(stream, original);

        // Tamper with the nextInnovationNumber to be too low
        var json = Encoding.UTF8.GetString(stream.ToArray());
        // The original nextInnovationNumber is 4; set it to 1 which is <= max innovation 3
        var tamperedJson = json.Replace(
            "\"nextInnovationNumber\": 4",
            "\"nextInnovationNumber\": 1");

        using var tamperedStream = new MemoryStream(Encoding.UTF8.GetBytes(tamperedJson));

        // Act & Assert: should throw CheckpointCorruptionException
        var act = async () => await _serializer.LoadAsync(tamperedStream);
        await act.Should().ThrowAsync<CheckpointCorruptionException>();

        // Assert: an Error-level log should have been emitted
        _logger.LogEntries.Should().Contain(entry =>
            entry.Level == LogLevel.Error &&
            entry.Message.Contains("Checkpoint validation failed"));
    }

    [Fact]
    public async Task LoadAsync_IncompatibleSchemaVersion_LogsError()
    {
        // Arrange: create a valid checkpoint, save it, then modify schemaVersion to "2.0.0"
        var original = CheckpointTestHelper.CreateFullCheckpoint();

        using var stream = new MemoryStream();
        await _serializer.SaveAsync(stream, original);

        var json = Encoding.UTF8.GetString(stream.ToArray());
        var tamperedJson = json.Replace(
            $"\"schemaVersion\": \"{SchemaVersion.Current}\"",
            "\"schemaVersion\": \"2.0.0\"");

        using var tamperedStream = new MemoryStream(Encoding.UTF8.GetBytes(tamperedJson));

        // Act & Assert: should throw SchemaVersionException
        var act = async () => await _serializer.LoadAsync(tamperedStream);
        await act.Should().ThrowAsync<SchemaVersionException>();

        // Assert: an Error-level log should have been emitted
        _logger.LogEntries.Should().Contain(entry =>
            entry.Level == LogLevel.Error &&
            entry.Message.Contains("Incompatible schema version") &&
            entry.Message.Contains("2.0.0"));
    }

    [Fact]
    public async Task SaveAndLoad_ValidCheckpoint_NoInfoLevelLogs()
    {
        // Arrange
        var original = CheckpointTestHelper.CreateFullCheckpoint();

        using var stream = new MemoryStream();

        // Act
        await _serializer.SaveAsync(stream, original);
        stream.Position = 0;
        await _serializer.LoadAsync(stream);

        // Assert: no Info-level logs during normal operations
        _logger.LogEntries.Should().NotContain(entry =>
            entry.Level == LogLevel.Information);
    }

    /// <summary>
    /// A simple capturing logger that records all log entries for test assertions.
    /// </summary>
    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, EventId EventId, string Message)> LogEntries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            LogEntries.Add((logLevel, eventId, formatter(state, exception)));
        }
    }
}
