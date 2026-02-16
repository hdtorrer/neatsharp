using FluentAssertions;
using Microsoft.Extensions.Options;
using NeatSharp.Gpu.Configuration;
using NeatSharp.Gpu.Detection;
using Xunit;

namespace NeatSharp.Gpu.Tests.Detection;

public class GpuDeviceDetectorTests
{
    private static GpuDeviceDetector CreateDetector(GpuOptions? options = null)
    {
        return new GpuDeviceDetector(Options.Create(options ?? new GpuOptions()));
    }

    [Fact]
    public void Detect_WithDefaultOptions_DoesNotThrow()
    {
        var detector = CreateDetector();

        var act = () => detector.Detect();

        act.Should().NotThrow();
    }

    [Fact]
    public void Detect_CalledMultipleTimes_ReturnsSameResult()
    {
        var detector = CreateDetector();

        var first = detector.Detect();
        var second = detector.Detect();

        if (first is not null)
        {
            second.Should().BeSameAs(first);
        }
        else
        {
            second.Should().BeNull();
        }
    }

    [Fact]
    public void Detect_WithHighMinComputeCapability_ReturnsIncompatibleOrNull()
    {
        var options = new GpuOptions { MinComputeCapability = 100 };
        var detector = CreateDetector(options);

        var result = detector.Detect();

        if (result is not null)
        {
            result.IsCompatible.Should().BeFalse();
            result.DiagnosticMessage.Should().NotBeNullOrEmpty();
        }
    }

    [Fact]
    public void Detect_WhenDeviceFound_ReturnsValidDeviceInfo()
    {
        var detector = CreateDetector();

        var result = detector.Detect();

        if (result is not null)
        {
            result.DeviceName.Should().NotBeNullOrEmpty();
            result.ComputeCapability.Should().NotBeNull();
            result.MemoryBytes.Should().BeGreaterThan(0);
        }
    }

    [Fact]
    public void Constructor_NullOptions_ThrowsArgumentNullException()
    {
        var act = () => new GpuDeviceDetector(null!);

        act.Should().Throw<ArgumentNullException>();
    }
}
