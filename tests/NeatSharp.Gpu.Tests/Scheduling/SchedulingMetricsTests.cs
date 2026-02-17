using FluentAssertions;
using Microsoft.Extensions.Logging;
using NeatSharp.Gpu.Configuration;
using NeatSharp.Gpu.Scheduling;
using Xunit;

namespace NeatSharp.Gpu.Tests.Scheduling;

public class SchedulingMetricsTests
{
    // --- Construction: all required fields populated ---

    [Fact]
    public void Construction_AllRequiredFieldsPopulated_ReturnsCorrectValues()
    {
        var metrics = new SchedulingMetrics
        {
            Generation = 42,
            CpuGenomeCount = 300,
            GpuGenomeCount = 700,
            CpuThroughput = 1500.0,
            GpuThroughput = 5000.0,
            CpuLatency = TimeSpan.FromMilliseconds(200),
            GpuLatency = TimeSpan.FromMilliseconds(140),
            SplitRatio = 0.7,
            ActivePolicy = SplitPolicyType.Static,
            SchedulerOverhead = TimeSpan.FromMilliseconds(5)
        };

        metrics.Generation.Should().Be(42);
        metrics.CpuGenomeCount.Should().Be(300);
        metrics.GpuGenomeCount.Should().Be(700);
        metrics.CpuThroughput.Should().Be(1500.0);
        metrics.GpuThroughput.Should().Be(5000.0);
        metrics.CpuLatency.Should().Be(TimeSpan.FromMilliseconds(200));
        metrics.GpuLatency.Should().Be(TimeSpan.FromMilliseconds(140));
        metrics.SplitRatio.Should().Be(0.7);
        metrics.ActivePolicy.Should().Be(SplitPolicyType.Static);
        metrics.SchedulerOverhead.Should().Be(TimeSpan.FromMilliseconds(5));
    }

    // --- FallbackEventInfo populated on fallback ---

    [Fact]
    public void FallbackEvent_WithFallbackInfo_ReturnsPopulatedEvent()
    {
        var timestamp = new DateTimeOffset(2026, 2, 16, 12, 0, 0, TimeSpan.Zero);
        var fallback = new FallbackEventInfo(timestamp, "GPU device lost", 700);

        var metrics = new SchedulingMetrics
        {
            Generation = 5,
            CpuGenomeCount = 300,
            GpuGenomeCount = 700,
            CpuThroughput = 1500.0,
            GpuThroughput = 0.0,
            CpuLatency = TimeSpan.FromMilliseconds(200),
            GpuLatency = TimeSpan.Zero,
            SplitRatio = 0.7,
            ActivePolicy = SplitPolicyType.Static,
            FallbackEvent = fallback,
            SchedulerOverhead = TimeSpan.FromMilliseconds(3)
        };

        metrics.FallbackEvent.Should().NotBeNull();
        metrics.FallbackEvent!.Value.Timestamp.Should().Be(timestamp);
        metrics.FallbackEvent.Value.FailureReason.Should().Be("GPU device lost");
        metrics.FallbackEvent.Value.GenomesRerouted.Should().Be(700);
    }

    // --- FallbackEvent is null by default ---

    [Fact]
    public void FallbackEvent_NotSet_IsNullByDefault()
    {
        var metrics = new SchedulingMetrics
        {
            Generation = 1,
            CpuGenomeCount = 100,
            GpuGenomeCount = 0,
            CpuThroughput = 1000.0,
            GpuThroughput = 0.0,
            CpuLatency = TimeSpan.FromMilliseconds(100),
            GpuLatency = TimeSpan.Zero,
            SplitRatio = 0.0,
            ActivePolicy = SplitPolicyType.Static,
            SchedulerOverhead = TimeSpan.FromMilliseconds(1)
        };

        metrics.FallbackEvent.Should().BeNull();
    }

    // --- Metrics immutability (sealed record — construct and read back) ---

    [Fact]
    public void Construction_SealedRecord_ValuesAreImmutableAfterCreation()
    {
        var metrics = new SchedulingMetrics
        {
            Generation = 10,
            CpuGenomeCount = 500,
            GpuGenomeCount = 500,
            CpuThroughput = 2000.0,
            GpuThroughput = 3000.0,
            CpuLatency = TimeSpan.FromMilliseconds(250),
            GpuLatency = TimeSpan.FromMilliseconds(167),
            SplitRatio = 0.5,
            ActivePolicy = SplitPolicyType.Adaptive,
            SchedulerOverhead = TimeSpan.FromMilliseconds(2)
        };

        // Records are immutable by design; verify values don't change after construction
        metrics.Generation.Should().Be(10);
        metrics.CpuGenomeCount.Should().Be(500);
        metrics.GpuGenomeCount.Should().Be(500);
        metrics.SplitRatio.Should().Be(0.5);
        metrics.ActivePolicy.Should().Be(SplitPolicyType.Adaptive);

        // With-expression creates a new instance — original is unchanged
        var modified = metrics with { Generation = 99 };
        modified.Generation.Should().Be(99);
        metrics.Generation.Should().Be(10);
    }

    // --- Throughput calculation correctness ---

    [Theory]
    [InlineData(1000, 0.5, 2000.0)]   // 1000 genomes / 0.5 seconds = 2000 g/s
    [InlineData(500, 1.0, 500.0)]      // 500 genomes / 1.0 seconds = 500 g/s
    [InlineData(100, 0.1, 1000.0)]     // 100 genomes / 0.1 seconds = 1000 g/s
    [InlineData(1, 0.001, 1000.0)]     // 1 genome / 0.001 seconds = 1000 g/s
    public void Throughput_GenomeCountDividedByLatency_CalculatedCorrectly(
        int genomeCount, double latencySeconds, double expectedThroughput)
    {
        // Throughput formula: genomeCount / latency.TotalSeconds
        var latency = TimeSpan.FromSeconds(latencySeconds);
        var throughput = genomeCount / latency.TotalSeconds;

        throughput.Should().BeApproximately(expectedThroughput, 0.001);

        // Verify metrics stores the pre-calculated value correctly
        var metrics = new SchedulingMetrics
        {
            Generation = 1,
            CpuGenomeCount = genomeCount,
            GpuGenomeCount = 0,
            CpuThroughput = throughput,
            GpuThroughput = 0.0,
            CpuLatency = latency,
            GpuLatency = TimeSpan.Zero,
            SplitRatio = 0.0,
            ActivePolicy = SplitPolicyType.Static,
            SchedulerOverhead = TimeSpan.FromMilliseconds(1)
        };

        metrics.CpuThroughput.Should().BeApproximately(expectedThroughput, 0.001);
    }

    // --- Various TimeSpan values ---

    [Fact]
    public void Construction_ZeroTimeSpanValues_HandledCorrectly()
    {
        var metrics = new SchedulingMetrics
        {
            Generation = 1,
            CpuGenomeCount = 100,
            GpuGenomeCount = 0,
            CpuThroughput = 0.0,
            GpuThroughput = 0.0,
            CpuLatency = TimeSpan.Zero,
            GpuLatency = TimeSpan.Zero,
            SplitRatio = 0.0,
            ActivePolicy = SplitPolicyType.Static,
            SchedulerOverhead = TimeSpan.Zero
        };

        metrics.CpuLatency.Should().Be(TimeSpan.Zero);
        metrics.GpuLatency.Should().Be(TimeSpan.Zero);
        metrics.SchedulerOverhead.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void Construction_LargeTimeSpanValues_HandledCorrectly()
    {
        var cpuLatency = TimeSpan.FromSeconds(30);
        var gpuLatency = TimeSpan.FromSeconds(15);
        var overhead = TimeSpan.FromSeconds(2);

        var metrics = new SchedulingMetrics
        {
            Generation = 1,
            CpuGenomeCount = 5000,
            GpuGenomeCount = 5000,
            CpuThroughput = 5000.0 / 30.0,
            GpuThroughput = 5000.0 / 15.0,
            CpuLatency = cpuLatency,
            GpuLatency = gpuLatency,
            SplitRatio = 0.5,
            ActivePolicy = SplitPolicyType.CostBased,
            SchedulerOverhead = overhead
        };

        metrics.CpuLatency.Should().Be(TimeSpan.FromSeconds(30));
        metrics.GpuLatency.Should().Be(TimeSpan.FromSeconds(15));
        metrics.SchedulerOverhead.Should().Be(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void Construction_SubMillisecondTimeSpan_HandledCorrectly()
    {
        var microLatency = TimeSpan.FromMicroseconds(500);

        var metrics = new SchedulingMetrics
        {
            Generation = 1,
            CpuGenomeCount = 10,
            GpuGenomeCount = 10,
            CpuThroughput = 10.0 / microLatency.TotalSeconds,
            GpuThroughput = 10.0 / microLatency.TotalSeconds,
            CpuLatency = microLatency,
            GpuLatency = microLatency,
            SplitRatio = 0.5,
            ActivePolicy = SplitPolicyType.Static,
            SchedulerOverhead = TimeSpan.FromMicroseconds(10)
        };

        metrics.CpuLatency.TotalMilliseconds.Should().Be(0.5);
        metrics.GpuLatency.TotalMilliseconds.Should().Be(0.5);
        metrics.SchedulerOverhead.TotalMicroseconds.Should().Be(10);
    }

    // --- FallbackEventInfo construction ---

    [Fact]
    public void FallbackEventInfo_Construction_AllFieldsPopulated()
    {
        var timestamp = DateTimeOffset.UtcNow;
        var info = new FallbackEventInfo(timestamp, "Out of memory", 250);

        info.Timestamp.Should().Be(timestamp);
        info.FailureReason.Should().Be("Out of memory");
        info.GenomesRerouted.Should().Be(250);
    }

    // --- ActivePolicy enum values ---

    [Theory]
    [InlineData(SplitPolicyType.Static)]
    [InlineData(SplitPolicyType.CostBased)]
    [InlineData(SplitPolicyType.Adaptive)]
    public void ActivePolicy_AllPolicyTypes_StoredCorrectly(SplitPolicyType policy)
    {
        var metrics = new SchedulingMetrics
        {
            Generation = 1,
            CpuGenomeCount = 50,
            GpuGenomeCount = 50,
            CpuThroughput = 1000.0,
            GpuThroughput = 2000.0,
            CpuLatency = TimeSpan.FromMilliseconds(50),
            GpuLatency = TimeSpan.FromMilliseconds(25),
            SplitRatio = 0.5,
            ActivePolicy = policy,
            SchedulerOverhead = TimeSpan.FromMilliseconds(1)
        };

        metrics.ActivePolicy.Should().Be(policy);
    }
}

public class LoggingMetricsReporterTests
{
    // --- Metrics summary logged at Information level ---

    [Fact]
    public void Report_NormalMetrics_LogsAtInformationLevel()
    {
        var logger = new RecordingLogger();
        var reporter = new LoggingMetricsReporter(logger);

        var metrics = new SchedulingMetrics
        {
            Generation = 5,
            CpuGenomeCount = 300,
            GpuGenomeCount = 700,
            CpuThroughput = 1500.0,
            GpuThroughput = 5000.0,
            CpuLatency = TimeSpan.FromMilliseconds(200),
            GpuLatency = TimeSpan.FromMilliseconds(140),
            SplitRatio = 0.7,
            ActivePolicy = SplitPolicyType.Static,
            SchedulerOverhead = TimeSpan.FromMilliseconds(5)
        };

        reporter.Report(metrics);

        logger.Entries.Should().ContainSingle(e => e.Level == LogLevel.Information);
        var infoEntry = logger.Entries.First(e => e.Level == LogLevel.Information);
        infoEntry.Message.Should().Contain("Generation 5");
        infoEntry.Message.Should().Contain("CPU=300");
        infoEntry.Message.Should().Contain("GPU=700");
        infoEntry.Message.Should().Contain("1500");
        infoEntry.Message.Should().Contain("5000");
        infoEntry.Message.Should().Contain("70");  // SplitRatio 0.7 formatted as percentage
        infoEntry.Message.Should().Contain("Static");
    }

    // --- Metrics summary contains throughput, split ratio, overhead ---

    [Fact]
    public void Report_NormalMetrics_LogsGenomeCounts()
    {
        var logger = new RecordingLogger();
        var reporter = new LoggingMetricsReporter(logger);

        var metrics = new SchedulingMetrics
        {
            Generation = 10,
            CpuGenomeCount = 200,
            GpuGenomeCount = 800,
            CpuThroughput = 2000.0,
            GpuThroughput = 8000.0,
            CpuLatency = TimeSpan.FromMilliseconds(100),
            GpuLatency = TimeSpan.FromMilliseconds(100),
            SplitRatio = 0.8,
            ActivePolicy = SplitPolicyType.Adaptive,
            SchedulerOverhead = TimeSpan.FromMilliseconds(3)
        };

        reporter.Report(metrics);

        var infoEntry = logger.Entries.First(e => e.Level == LogLevel.Information);
        infoEntry.Message.Should().Contain("200");   // CpuGenomeCount
        infoEntry.Message.Should().Contain("800");   // GpuGenomeCount
        infoEntry.Message.Should().Contain("2000");  // CpuThroughput
        infoEntry.Message.Should().Contain("8000");  // GpuThroughput
        infoEntry.Message.Should().Contain("Overhead");
    }

    // --- Fallback events logged at Warning level ---

    [Fact]
    public void Report_WithFallbackEvent_LogsAtWarningLevel()
    {
        var logger = new RecordingLogger();
        var reporter = new LoggingMetricsReporter(logger);

        var timestamp = new DateTimeOffset(2026, 2, 16, 12, 0, 0, TimeSpan.Zero);
        var metrics = new SchedulingMetrics
        {
            Generation = 7,
            CpuGenomeCount = 300,
            GpuGenomeCount = 700,
            CpuThroughput = 1500.0,
            GpuThroughput = 0.0,
            CpuLatency = TimeSpan.FromMilliseconds(200),
            GpuLatency = TimeSpan.Zero,
            SplitRatio = 0.7,
            ActivePolicy = SplitPolicyType.Static,
            FallbackEvent = new FallbackEventInfo(timestamp, "GPU device lost", 700),
            SchedulerOverhead = TimeSpan.FromMilliseconds(5)
        };

        reporter.Report(metrics);

        logger.Entries.Should().Contain(e => e.Level == LogLevel.Warning);
        var warningEntry = logger.Entries.First(e => e.Level == LogLevel.Warning);
        warningEntry.Message.Should().Contain("GPU fallback");
        warningEntry.Message.Should().Contain("7");              // generation
        warningEntry.Message.Should().Contain("GPU device lost"); // failure reason
        warningEntry.Message.Should().Contain("700");             // genomes rerouted
    }

    // --- Fallback event also logs Information level for metrics ---

    [Fact]
    public void Report_WithFallbackEvent_AlsoLogsInformationForMetrics()
    {
        var logger = new RecordingLogger();
        var reporter = new LoggingMetricsReporter(logger);

        var metrics = new SchedulingMetrics
        {
            Generation = 7,
            CpuGenomeCount = 300,
            GpuGenomeCount = 700,
            CpuThroughput = 1500.0,
            GpuThroughput = 0.0,
            CpuLatency = TimeSpan.FromMilliseconds(200),
            GpuLatency = TimeSpan.Zero,
            SplitRatio = 0.7,
            ActivePolicy = SplitPolicyType.Static,
            FallbackEvent = new FallbackEventInfo(DateTimeOffset.UtcNow, "GPU error", 700),
            SchedulerOverhead = TimeSpan.FromMilliseconds(5)
        };

        reporter.Report(metrics);

        logger.Entries.Should().Contain(e => e.Level == LogLevel.Information);
        logger.Entries.Should().Contain(e => e.Level == LogLevel.Warning);
        logger.Entries.Should().HaveCount(2);
    }

    // --- Null fallback event not logged as warning ---

    [Fact]
    public void Report_NullFallbackEvent_NoWarningLogged()
    {
        var logger = new RecordingLogger();
        var reporter = new LoggingMetricsReporter(logger);

        var metrics = new SchedulingMetrics
        {
            Generation = 5,
            CpuGenomeCount = 300,
            GpuGenomeCount = 700,
            CpuThroughput = 1500.0,
            GpuThroughput = 5000.0,
            CpuLatency = TimeSpan.FromMilliseconds(200),
            GpuLatency = TimeSpan.FromMilliseconds(140),
            SplitRatio = 0.7,
            ActivePolicy = SplitPolicyType.Static,
            SchedulerOverhead = TimeSpan.FromMilliseconds(5)
        };

        reporter.Report(metrics);

        logger.Entries.Should().NotContain(e => e.Level == LogLevel.Warning);
        logger.Entries.Should().ContainSingle(e => e.Level == LogLevel.Information);
    }

    // --- Uses structured logging (ILogger) ---

    [Fact]
    public void Report_StructuredLogging_UsesILogger()
    {
        var logger = new RecordingLogger();
        var reporter = new LoggingMetricsReporter(logger);

        var metrics = new SchedulingMetrics
        {
            Generation = 1,
            CpuGenomeCount = 50,
            GpuGenomeCount = 50,
            CpuThroughput = 500.0,
            GpuThroughput = 1000.0,
            CpuLatency = TimeSpan.FromMilliseconds(100),
            GpuLatency = TimeSpan.FromMilliseconds(50),
            SplitRatio = 0.5,
            ActivePolicy = SplitPolicyType.CostBased,
            SchedulerOverhead = TimeSpan.FromMilliseconds(2)
        };

        reporter.Report(metrics);

        // Structured logging uses ILogger with format string and parameters
        // The RecordingLogger captures the formatted output
        logger.Entries.Should().HaveCount(1);
        logger.Entries[0].Level.Should().Be(LogLevel.Information);
        logger.Entries[0].Message.Should().NotBeNullOrEmpty();
    }

    // --- Constructor null guard ---

    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        var act = () => new LoggingMetricsReporter(null!);

        act.Should().Throw<ArgumentNullException>().WithParameterName("logger");
    }

    /// <summary>
    /// Logger that records all log entries for assertion purposes.
    /// </summary>
    private sealed class RecordingLogger : ILogger<LoggingMetricsReporter>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Entries.Add((logLevel, formatter(state, exception)));
        }
    }
}
