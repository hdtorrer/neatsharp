using FluentAssertions;
using Microsoft.Extensions.Options;
using NeatSharp.Gpu.Configuration;
using Xunit;

namespace NeatSharp.Gpu.Tests.Configuration;

public class HybridOptionsValidatorTests
{
    private readonly HybridOptionsValidator _validator = new();

    // --- Default options ---

    [Fact]
    public void Validate_DefaultOptions_ReturnsSuccess()
    {
        var options = new HybridOptions();

        var result = _validator.Validate(null, options);

        result.Succeeded.Should().BeTrue();
    }

    // --- StaticGpuFraction ---

    [Fact]
    public void Validate_StaticGpuFractionAtLowerBound_ReturnsSuccess()
    {
        var options = new HybridOptions { StaticGpuFraction = 0.0 };

        var result = _validator.Validate(null, options);

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public void Validate_StaticGpuFractionAtUpperBound_ReturnsSuccess()
    {
        var options = new HybridOptions { StaticGpuFraction = 1.0 };

        var result = _validator.Validate(null, options);

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public void Validate_StaticGpuFractionBelowRange_ReturnsFailure()
    {
        var options = new HybridOptions { StaticGpuFraction = -0.1 };

        var result = _validator.Validate(null, options);

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("StaticGpuFraction");
    }

    [Fact]
    public void Validate_StaticGpuFractionAboveRange_ReturnsFailure()
    {
        var options = new HybridOptions { StaticGpuFraction = 1.1 };

        var result = _validator.Validate(null, options);

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("StaticGpuFraction");
    }

    // --- MinPopulationForSplit ---

    [Fact]
    public void Validate_MinPopulationForSplitAtLowerBound_ReturnsSuccess()
    {
        var options = new HybridOptions { MinPopulationForSplit = 2 };

        var result = _validator.Validate(null, options);

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public void Validate_MinPopulationForSplitAtUpperBound_ReturnsSuccess()
    {
        var options = new HybridOptions { MinPopulationForSplit = 100_000 };

        var result = _validator.Validate(null, options);

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public void Validate_MinPopulationForSplitBelowRange_ReturnsFailure()
    {
        var options = new HybridOptions { MinPopulationForSplit = 1 };

        var result = _validator.Validate(null, options);

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("MinPopulationForSplit");
    }

    [Fact]
    public void Validate_MinPopulationForSplitAboveRange_ReturnsFailure()
    {
        var options = new HybridOptions { MinPopulationForSplit = 100_001 };

        var result = _validator.Validate(null, options);

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("MinPopulationForSplit");
    }

    // --- GpuReprobeInterval ---

    [Fact]
    public void Validate_GpuReprobeIntervalAtLowerBound_ReturnsSuccess()
    {
        var options = new HybridOptions { GpuReprobeInterval = 1 };

        var result = _validator.Validate(null, options);

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public void Validate_GpuReprobeIntervalAtUpperBound_ReturnsSuccess()
    {
        var options = new HybridOptions { GpuReprobeInterval = 1_000 };

        var result = _validator.Validate(null, options);

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public void Validate_GpuReprobeIntervalBelowRange_ReturnsFailure()
    {
        var options = new HybridOptions { GpuReprobeInterval = 0 };

        var result = _validator.Validate(null, options);

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("GpuReprobeInterval");
    }

    [Fact]
    public void Validate_GpuReprobeIntervalAboveRange_ReturnsFailure()
    {
        var options = new HybridOptions { GpuReprobeInterval = 1_001 };

        var result = _validator.Validate(null, options);

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("GpuReprobeInterval");
    }

    // --- Adaptive.Kp ---

    [Fact]
    public void Validate_KpZero_ReturnsFailure()
    {
        var options = new HybridOptions { Adaptive = new AdaptivePidOptions { Kp = 0.0 } };

        var result = _validator.Validate(null, options);

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("Kp");
    }

    [Fact]
    public void Validate_KpNegative_ReturnsFailure()
    {
        var options = new HybridOptions { Adaptive = new AdaptivePidOptions { Kp = -0.1 } };

        var result = _validator.Validate(null, options);

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("Kp");
    }

    [Fact]
    public void Validate_KpPositive_ReturnsSuccess()
    {
        var options = new HybridOptions { Adaptive = new AdaptivePidOptions { Kp = 0.001 } };

        var result = _validator.Validate(null, options);

        result.Succeeded.Should().BeTrue();
    }

    // --- Adaptive.Ki ---

    [Fact]
    public void Validate_KiAtLowerBound_ReturnsSuccess()
    {
        var options = new HybridOptions { Adaptive = new AdaptivePidOptions { Ki = 0.0 } };

        var result = _validator.Validate(null, options);

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public void Validate_KiAtUpperBound_ReturnsSuccess()
    {
        var options = new HybridOptions { Adaptive = new AdaptivePidOptions { Ki = 10.0 } };

        var result = _validator.Validate(null, options);

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public void Validate_KiBelowRange_ReturnsFailure()
    {
        var options = new HybridOptions { Adaptive = new AdaptivePidOptions { Ki = -0.1 } };

        var result = _validator.Validate(null, options);

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("Ki");
    }

    [Fact]
    public void Validate_KiAboveRange_ReturnsFailure()
    {
        var options = new HybridOptions { Adaptive = new AdaptivePidOptions { Ki = 10.1 } };

        var result = _validator.Validate(null, options);

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("Ki");
    }

    // --- Adaptive.Kd ---

    [Fact]
    public void Validate_KdAtLowerBound_ReturnsSuccess()
    {
        var options = new HybridOptions { Adaptive = new AdaptivePidOptions { Kd = 0.0 } };

        var result = _validator.Validate(null, options);

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public void Validate_KdAtUpperBound_ReturnsSuccess()
    {
        var options = new HybridOptions { Adaptive = new AdaptivePidOptions { Kd = 10.0 } };

        var result = _validator.Validate(null, options);

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public void Validate_KdBelowRange_ReturnsFailure()
    {
        var options = new HybridOptions { Adaptive = new AdaptivePidOptions { Kd = -0.1 } };

        var result = _validator.Validate(null, options);

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("Kd");
    }

    [Fact]
    public void Validate_KdAboveRange_ReturnsFailure()
    {
        var options = new HybridOptions { Adaptive = new AdaptivePidOptions { Kd = 10.1 } };

        var result = _validator.Validate(null, options);

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("Kd");
    }

    // --- Adaptive.InitialGpuFraction ---

    [Fact]
    public void Validate_InitialGpuFractionAtLowerBound_ReturnsSuccess()
    {
        var options = new HybridOptions { Adaptive = new AdaptivePidOptions { InitialGpuFraction = 0.0 } };

        var result = _validator.Validate(null, options);

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public void Validate_InitialGpuFractionAtUpperBound_ReturnsSuccess()
    {
        var options = new HybridOptions { Adaptive = new AdaptivePidOptions { InitialGpuFraction = 1.0 } };

        var result = _validator.Validate(null, options);

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public void Validate_InitialGpuFractionBelowRange_ReturnsFailure()
    {
        var options = new HybridOptions { Adaptive = new AdaptivePidOptions { InitialGpuFraction = -0.1 } };

        var result = _validator.Validate(null, options);

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("InitialGpuFraction");
    }

    [Fact]
    public void Validate_InitialGpuFractionAboveRange_ReturnsFailure()
    {
        var options = new HybridOptions { Adaptive = new AdaptivePidOptions { InitialGpuFraction = 1.1 } };

        var result = _validator.Validate(null, options);

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("InitialGpuFraction");
    }

    // --- CostModel.NodeWeight ---

    [Fact]
    public void Validate_NodeWeightAtLowerBound_ReturnsSuccess()
    {
        var options = new HybridOptions { CostModel = new CostModelOptions { NodeWeight = 0.0 } };

        var result = _validator.Validate(null, options);

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public void Validate_NodeWeightAtUpperBound_ReturnsSuccess()
    {
        var options = new HybridOptions { CostModel = new CostModelOptions { NodeWeight = 1000.0 } };

        var result = _validator.Validate(null, options);

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public void Validate_NodeWeightBelowRange_ReturnsFailure()
    {
        var options = new HybridOptions { CostModel = new CostModelOptions { NodeWeight = -0.1 } };

        var result = _validator.Validate(null, options);

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("NodeWeight");
    }

    [Fact]
    public void Validate_NodeWeightAboveRange_ReturnsFailure()
    {
        var options = new HybridOptions { CostModel = new CostModelOptions { NodeWeight = 1000.1 } };

        var result = _validator.Validate(null, options);

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("NodeWeight");
    }

    // --- CostModel.ConnectionWeight ---

    [Fact]
    public void Validate_ConnectionWeightAtLowerBound_ReturnsSuccess()
    {
        var options = new HybridOptions { CostModel = new CostModelOptions { ConnectionWeight = 0.0 } };

        var result = _validator.Validate(null, options);

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public void Validate_ConnectionWeightAtUpperBound_ReturnsSuccess()
    {
        var options = new HybridOptions { CostModel = new CostModelOptions { ConnectionWeight = 1000.0 } };

        var result = _validator.Validate(null, options);

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public void Validate_ConnectionWeightBelowRange_ReturnsFailure()
    {
        var options = new HybridOptions { CostModel = new CostModelOptions { ConnectionWeight = -0.1 } };

        var result = _validator.Validate(null, options);

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("ConnectionWeight");
    }

    [Fact]
    public void Validate_ConnectionWeightAboveRange_ReturnsFailure()
    {
        var options = new HybridOptions { CostModel = new CostModelOptions { ConnectionWeight = 1000.1 } };

        var result = _validator.Validate(null, options);

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("ConnectionWeight");
    }

    // --- Multiple failures ---

    [Fact]
    public void Validate_MultipleInvalidOptions_ReportsAllFailures()
    {
        var options = new HybridOptions
        {
            StaticGpuFraction = -1.0,
            MinPopulationForSplit = 0,
            GpuReprobeInterval = 0,
            Adaptive = new AdaptivePidOptions { Kp = 0.0 }
        };

        var result = _validator.Validate(null, options);

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("StaticGpuFraction");
        result.FailureMessage.Should().Contain("MinPopulationForSplit");
        result.FailureMessage.Should().Contain("GpuReprobeInterval");
        result.FailureMessage.Should().Contain("Kp");
    }

    // --- Failure message includes value ---

    [Fact]
    public void Validate_StaticGpuFractionBelowRange_FailureMessageIncludesValue()
    {
        var options = new HybridOptions { StaticGpuFraction = -0.5 };

        var result = _validator.Validate(null, options);

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("-0.5");
    }

    [Fact]
    public void Validate_MinPopulationForSplitBelowRange_FailureMessageIncludesValue()
    {
        var options = new HybridOptions { MinPopulationForSplit = 1 };

        var result = _validator.Validate(null, options);

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("1");
    }

    // --- Mid-range values ---

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.3)]
    [InlineData(0.5)]
    [InlineData(0.7)]
    [InlineData(1.0)]
    public void Validate_StaticGpuFractionInRange_ReturnsSuccess(double value)
    {
        var options = new HybridOptions { StaticGpuFraction = value };

        var result = _validator.Validate(null, options);

        result.Succeeded.Should().BeTrue();
    }

    [Theory]
    [InlineData(2)]
    [InlineData(50)]
    [InlineData(1_000)]
    [InlineData(50_000)]
    [InlineData(100_000)]
    public void Validate_MinPopulationForSplitInRange_ReturnsSuccess(int value)
    {
        var options = new HybridOptions { MinPopulationForSplit = value };

        var result = _validator.Validate(null, options);

        result.Succeeded.Should().BeTrue();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(500)]
    [InlineData(1_000)]
    public void Validate_GpuReprobeIntervalInRange_ReturnsSuccess(int value)
    {
        var options = new HybridOptions { GpuReprobeInterval = value };

        var result = _validator.Validate(null, options);

        result.Succeeded.Should().BeTrue();
    }
}
