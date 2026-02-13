using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NeatSharp.Configuration;
using NeatSharp.Extensions;
using Xunit;

namespace NeatSharp.Tests.Configuration;

public class NeatSharpOptionsTests
{
    [Fact]
    public void Defaults_PopulationSize_Is150()
    {
        var options = new NeatSharpOptions();

        options.PopulationSize.Should().Be(150);
    }

    [Fact]
    public void Defaults_Seed_IsNull()
    {
        var options = new NeatSharpOptions();

        options.Seed.Should().BeNull();
    }

    [Fact]
    public void Defaults_EnableMetrics_IsTrue()
    {
        var options = new NeatSharpOptions();

        options.EnableMetrics.Should().BeTrue();
    }

    [Fact]
    public void Defaults_Stopping_IsNotNull()
    {
        var options = new NeatSharpOptions();

        options.Stopping.Should().NotBeNull();
    }

    [Fact]
    public void Defaults_Complexity_IsNotNull()
    {
        var options = new NeatSharpOptions();

        options.Complexity.Should().NotBeNull();
    }

    [Fact]
    public void Validation_PopulationSizeZero_Fails()
    {
        var act = () => BuildAndValidate(o => o.PopulationSize = 0);

        act.Should().Throw<OptionsValidationException>();
    }

    [Fact]
    public void Validation_PopulationSizeNegative_Fails()
    {
        var act = () => BuildAndValidate(o => o.PopulationSize = -1);

        act.Should().Throw<OptionsValidationException>();
    }

    [Fact]
    public void Validation_PopulationSizeOne_Succeeds()
    {
        var act = () => BuildAndValidate(o =>
        {
            o.PopulationSize = 1;
            o.Stopping.MaxGenerations = 100;
        });

        act.Should().NotThrow();
    }

    [Fact]
    public void Validation_PopulationSize100000_Succeeds()
    {
        var act = () => BuildAndValidate(o =>
        {
            o.PopulationSize = 100_000;
            o.Stopping.MaxGenerations = 100;
        });

        act.Should().NotThrow();
    }

    [Fact]
    public void Validation_PopulationSizeExceedsMax_Fails()
    {
        var act = () => BuildAndValidate(o => o.PopulationSize = 100_001);

        act.Should().Throw<OptionsValidationException>();
    }

    [Fact]
    public void Validation_SeedNullable_AcceptsNullAndValues()
    {
        var optionsNull = new NeatSharpOptions();
        optionsNull.Seed.Should().BeNull();

        var optionsSet = new NeatSharpOptions { Seed = 42 };
        optionsSet.Seed.Should().Be(42);
    }

    [Fact]
    public void Seed_NullDefault_PreservedThroughOptionsConfiguration()
    {
        var options = BuildAndValidate(o =>
        {
            o.Stopping.MaxGenerations = 100;
        });

        options.Seed.Should().BeNull("null Seed signals auto-generation at runtime");
    }

    [Fact]
    public void Seed_ExplicitValue_PreservedThroughOptionsConfiguration()
    {
        var options = BuildAndValidate(o =>
        {
            o.Seed = 42;
            o.Stopping.MaxGenerations = 100;
        });

        options.Seed.Should().Be(42);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    [InlineData(int.MaxValue)]
    public void Seed_AnyIntValue_PreservedThroughOptionsConfiguration(int seed)
    {
        var options = BuildAndValidate(o =>
        {
            o.Seed = seed;
            o.Stopping.MaxGenerations = 100;
        });

        options.Seed.Should().Be(seed);
    }

    private static NeatSharpOptions BuildAndValidate(Action<NeatSharpOptions> configure)
    {
        var services = new ServiceCollection();
        services.AddNeatSharp(configure);
        var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IOptions<NeatSharpOptions>>().Value;
    }
}
