using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NeatSharp.Gpu.Configuration;
using NeatSharp.Gpu.Extensions;
using NeatSharp.Gpu.Scheduling;
using Xunit;

namespace NeatSharp.Gpu.Tests.Integration;

/// <summary>
/// Validates that all code samples in specs/007-hybrid-eval-scheduler/quickstart.md
/// compile and run correctly against the implemented API.
/// Tests verify API surface and configuration without requiring GPU hardware.
/// </summary>
public class HybridQuickstartValidationTests
{
    // --- Test 1: Basic usage DI registration pattern compiles ---

    [Fact]
    public void BasicUsage_AddNeatSharpHybrid_RegistersHybridOptions()
    {
        // From quickstart.md: services.AddNeatSharpHybrid()
        var services = new ServiceCollection();
        services.AddNeatSharpHybrid();

        using var sp = services.BuildServiceProvider();
        var options = sp.GetRequiredService<IOptions<HybridOptions>>().Value;

        options.Should().NotBeNull();
        options.EnableHybrid.Should().BeTrue("default EnableHybrid should be true");
        options.SplitPolicy.Should().Be(SplitPolicyType.Adaptive, "default policy should be Adaptive");
    }

    // --- Test 2: Static split configuration compiles ---

    [Fact]
    public void StaticSplitConfiguration_Compiles()
    {
        // From quickstart.md: "Static Split (Fixed 70/30 GPU/CPU)"
        var services = new ServiceCollection();
        services.AddNeatSharpHybrid(hybrid =>
        {
            hybrid.SplitPolicy = SplitPolicyType.Static;
            hybrid.StaticGpuFraction = 0.7;
        });

        using var sp = services.BuildServiceProvider();
        var options = sp.GetRequiredService<IOptions<HybridOptions>>().Value;

        options.SplitPolicy.Should().Be(SplitPolicyType.Static);
        options.StaticGpuFraction.Should().Be(0.7);
    }

    // --- Test 3: Cost-based configuration compiles ---

    [Fact]
    public void CostBasedConfiguration_Compiles()
    {
        // From quickstart.md: "Cost-Based Partitioning (Complexity-Driven)"
        var services = new ServiceCollection();
        services.AddNeatSharpHybrid(hybrid =>
        {
            hybrid.SplitPolicy = SplitPolicyType.CostBased;
            hybrid.CostModel.NodeWeight = 1.0;
            hybrid.CostModel.ConnectionWeight = 1.0;
        });

        using var sp = services.BuildServiceProvider();
        var options = sp.GetRequiredService<IOptions<HybridOptions>>().Value;

        options.SplitPolicy.Should().Be(SplitPolicyType.CostBased);
        options.CostModel.NodeWeight.Should().Be(1.0);
        options.CostModel.ConnectionWeight.Should().Be(1.0);
    }

    // --- Test 4: Adaptive configuration with custom PID gains compiles ---

    [Fact]
    public void AdaptiveConfiguration_WithCustomPidGains_Compiles()
    {
        // From quickstart.md: "Adaptive Partitioning with Custom PID Gains"
        var services = new ServiceCollection();
        services.AddNeatSharpHybrid(hybrid =>
        {
            hybrid.SplitPolicy = SplitPolicyType.Adaptive;
            hybrid.Adaptive.Kp = 0.5;
            hybrid.Adaptive.Ki = 0.1;
            hybrid.Adaptive.Kd = 0.05;
            hybrid.Adaptive.InitialGpuFraction = 0.5;
        });

        using var sp = services.BuildServiceProvider();
        var options = sp.GetRequiredService<IOptions<HybridOptions>>().Value;

        options.SplitPolicy.Should().Be(SplitPolicyType.Adaptive);
        options.Adaptive.Kp.Should().Be(0.5);
        options.Adaptive.Ki.Should().Be(0.1);
        options.Adaptive.Kd.Should().Be(0.05);
        options.Adaptive.InitialGpuFraction.Should().Be(0.5);
    }

    // --- Test 5: Disable hybrid (passthrough) compiles ---

    [Fact]
    public void DisableHybrid_PassthroughConfiguration_Compiles()
    {
        // From quickstart.md: "Disable Hybrid (Passthrough to Inner Evaluator)"
        var services = new ServiceCollection();
        services.AddNeatSharpHybrid(hybrid =>
        {
            hybrid.EnableHybrid = false;
        });

        using var sp = services.BuildServiceProvider();
        var options = sp.GetRequiredService<IOptions<HybridOptions>>().Value;

        options.EnableHybrid.Should().BeFalse();
    }

    // --- Test 6: Custom GPU failure recovery configuration compiles ---

    [Fact]
    public void CustomGpuFailureRecovery_Configuration_Compiles()
    {
        // From quickstart.md: "Custom GPU Failure Recovery"
        var services = new ServiceCollection();
        services.AddNeatSharpHybrid(hybrid =>
        {
            hybrid.GpuReprobeInterval = 20;
            hybrid.MinPopulationForSplit = 100;
        });

        using var sp = services.BuildServiceProvider();
        var options = sp.GetRequiredService<IOptions<HybridOptions>>().Value;

        options.GpuReprobeInterval.Should().Be(20);
        options.MinPopulationForSplit.Should().Be(100);
    }

    // --- Test 7: Custom ISchedulingMetricsReporter implementation works ---

    [Fact]
    public void CustomMetricsReporter_RegisteredBeforeHybrid_IsResolved()
    {
        // From quickstart.md: "Accessing Scheduling Metrics Programmatically"
        var collector = new MyMetricsCollector();
        var services = new ServiceCollection();
        services.AddSingleton<ISchedulingMetricsReporter>(collector);
        services.AddNeatSharpHybrid();

        using var sp = services.BuildServiceProvider();
        var reporter = sp.GetRequiredService<ISchedulingMetricsReporter>();

        reporter.Should().BeSameAs(collector,
            "custom reporter registered before AddNeatSharpHybrid should be used");
    }

    [Fact]
    public void DefaultMetricsReporter_WhenNoCustomRegistered_IsLogingReporter()
    {
        // When no custom reporter is registered, AddNeatSharpHybrid registers the default
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddNeatSharpHybrid();

        using var sp = services.BuildServiceProvider();
        var reporter = sp.GetRequiredService<ISchedulingMetricsReporter>();

        reporter.Should().NotBeNull();
        reporter.Should().BeOfType<LoggingMetricsReporter>();
    }

    /// <summary>
    /// Custom metrics collector matching the quickstart.md code sample.
    /// </summary>
    private sealed class MyMetricsCollector : ISchedulingMetricsReporter
    {
        public List<SchedulingMetrics> History { get; } = [];

        public void Report(SchedulingMetrics metrics) => History.Add(metrics);
    }
}
