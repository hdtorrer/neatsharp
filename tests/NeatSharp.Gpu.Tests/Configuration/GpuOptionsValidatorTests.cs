using FluentAssertions;
using Microsoft.Extensions.Options;
using NeatSharp.Gpu.Configuration;
using Xunit;

namespace NeatSharp.Gpu.Tests.Configuration;

public class GpuOptionsValidatorTests
{
    private readonly GpuOptionsValidator _validator = new();

    // --- Default options ---

    [Fact]
    public void Validate_DefaultOptions_ReturnsSuccess()
    {
        var options = new GpuOptions();

        var result = _validator.Validate(null, options);

        result.Succeeded.Should().BeTrue();
    }

    // --- MinComputeCapability ---

    [Fact]
    public void Validate_MinComputeCapabilityAtLowerBound_ReturnsSuccess()
    {
        var options = new GpuOptions { MinComputeCapability = 20 };

        var result = _validator.Validate(null, options);

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public void Validate_MinComputeCapabilityAtUpperBound_ReturnsSuccess()
    {
        var options = new GpuOptions { MinComputeCapability = 100 };

        var result = _validator.Validate(null, options);

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public void Validate_MinComputeCapabilityBelowRange_ReturnsFailure()
    {
        var options = new GpuOptions { MinComputeCapability = 19 };

        var result = _validator.Validate(null, options);

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("MinComputeCapability");
    }

    [Fact]
    public void Validate_MinComputeCapabilityAboveRange_ReturnsFailure()
    {
        var options = new GpuOptions { MinComputeCapability = 101 };

        var result = _validator.Validate(null, options);

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("MinComputeCapability");
    }

    // --- MaxPopulationSize ---

    [Fact]
    public void Validate_MaxPopulationSizeNull_ReturnsSuccess()
    {
        var options = new GpuOptions { MaxPopulationSize = null };

        var result = _validator.Validate(null, options);

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public void Validate_MaxPopulationSizeAtLowerBound_ReturnsSuccess()
    {
        var options = new GpuOptions { MaxPopulationSize = 1 };

        var result = _validator.Validate(null, options);

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public void Validate_MaxPopulationSizeAtUpperBound_ReturnsSuccess()
    {
        var options = new GpuOptions { MaxPopulationSize = 1_000_000 };

        var result = _validator.Validate(null, options);

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public void Validate_MaxPopulationSizeBelowRange_ReturnsFailure()
    {
        var options = new GpuOptions { MaxPopulationSize = 0 };

        var result = _validator.Validate(null, options);

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("MaxPopulationSize");
    }

    [Fact]
    public void Validate_MaxPopulationSizeNegative_ReturnsFailure()
    {
        var options = new GpuOptions { MaxPopulationSize = -1 };

        var result = _validator.Validate(null, options);

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("MaxPopulationSize");
    }

    [Fact]
    public void Validate_MaxPopulationSizeAboveRange_ReturnsFailure()
    {
        var options = new GpuOptions { MaxPopulationSize = 1_000_001 };

        var result = _validator.Validate(null, options);

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("MaxPopulationSize");
    }

    // --- Multiple failures ---

    [Fact]
    public void Validate_MultipleInvalidOptions_ReportsAllFailures()
    {
        var options = new GpuOptions
        {
            MinComputeCapability = 10,
            MaxPopulationSize = 0
        };

        var result = _validator.Validate(null, options);

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("MinComputeCapability");
        result.FailureMessage.Should().Contain("MaxPopulationSize");
    }

    // --- Default values ---

    [Fact]
    public void Defaults_EnableGpu_IsTrue()
    {
        var options = new GpuOptions();

        options.EnableGpu.Should().BeTrue();
    }

    [Fact]
    public void Defaults_MinComputeCapability_Is50()
    {
        var options = new GpuOptions();

        options.MinComputeCapability.Should().Be(50);
    }

    [Fact]
    public void Defaults_BestEffortDeterministic_IsFalse()
    {
        var options = new GpuOptions();

        options.BestEffortDeterministic.Should().BeFalse();
    }

    [Fact]
    public void Defaults_MaxPopulationSize_IsNull()
    {
        var options = new GpuOptions();

        options.MaxPopulationSize.Should().BeNull();
    }

    // --- Valid mid-range values ---

    [Theory]
    [InlineData(20)]
    [InlineData(50)]
    [InlineData(75)]
    [InlineData(100)]
    public void Validate_MinComputeCapabilityInRange_ReturnsSuccess(int value)
    {
        var options = new GpuOptions { MinComputeCapability = value };

        var result = _validator.Validate(null, options);

        result.Succeeded.Should().BeTrue();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(100)]
    [InlineData(10_000)]
    [InlineData(500_000)]
    [InlineData(1_000_000)]
    public void Validate_MaxPopulationSizeInRange_ReturnsSuccess(int value)
    {
        var options = new GpuOptions { MaxPopulationSize = value };

        var result = _validator.Validate(null, options);

        result.Succeeded.Should().BeTrue();
    }

    // --- Failure message includes value ---

    [Fact]
    public void Validate_MinComputeCapabilityBelowRange_FailureMessageIncludesValue()
    {
        var options = new GpuOptions { MinComputeCapability = 5 };

        var result = _validator.Validate(null, options);

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("5");
    }

    [Fact]
    public void Validate_MaxPopulationSizeAboveRange_FailureMessageIncludesValue()
    {
        var options = new GpuOptions { MaxPopulationSize = 2_000_000 };

        var result = _validator.Validate(null, options);

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("2000000");
    }
}
