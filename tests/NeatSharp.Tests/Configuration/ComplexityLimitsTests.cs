using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NeatSharp.Configuration;
using NeatSharp.Extensions;
using Xunit;

namespace NeatSharp.Tests.Configuration;

public class ComplexityLimitsTests
{
    [Fact]
    public void Defaults_MaxNodesIsNull()
    {
        var limits = new ComplexityLimits();

        limits.MaxNodes.Should().BeNull();
    }

    [Fact]
    public void Defaults_MaxConnectionsIsNull()
    {
        var limits = new ComplexityLimits();

        limits.MaxConnections.Should().BeNull();
    }

    [Fact]
    public void Validation_NullMaxNodes_Succeeds()
    {
        var act = () => BuildAndValidate(o =>
        {
            o.Stopping.MaxGenerations = 100;
            o.Complexity.MaxNodes = null;
        });

        act.Should().NotThrow();
    }

    [Fact]
    public void Validation_NullMaxConnections_Succeeds()
    {
        var act = () => BuildAndValidate(o =>
        {
            o.Stopping.MaxGenerations = 100;
            o.Complexity.MaxConnections = null;
        });

        act.Should().NotThrow();
    }

    [Fact]
    public void Validation_MaxNodesPositive_Succeeds()
    {
        var act = () => BuildAndValidate(o =>
        {
            o.Stopping.MaxGenerations = 100;
            o.Complexity.MaxNodes = 500;
        });

        act.Should().NotThrow();
    }

    [Fact]
    public void Validation_MaxConnectionsPositive_Succeeds()
    {
        var act = () => BuildAndValidate(o =>
        {
            o.Stopping.MaxGenerations = 100;
            o.Complexity.MaxConnections = 1000;
        });

        act.Should().NotThrow();
    }

    [Fact]
    public void Validation_MaxNodesZero_Fails()
    {
        var act = () => BuildAndValidate(o =>
        {
            o.Stopping.MaxGenerations = 100;
            o.Complexity.MaxNodes = 0;
        });

        act.Should().Throw<OptionsValidationException>();
    }

    [Fact]
    public void Validation_MaxConnectionsZero_Fails()
    {
        var act = () => BuildAndValidate(o =>
        {
            o.Stopping.MaxGenerations = 100;
            o.Complexity.MaxConnections = 0;
        });

        act.Should().Throw<OptionsValidationException>();
    }

    [Fact]
    public void Validation_MaxNodesNegative_Fails()
    {
        var act = () => BuildAndValidate(o =>
        {
            o.Stopping.MaxGenerations = 100;
            o.Complexity.MaxNodes = -1;
        });

        act.Should().Throw<OptionsValidationException>();
    }

    [Fact]
    public void Validation_MaxConnectionsNegative_Fails()
    {
        var act = () => BuildAndValidate(o =>
        {
            o.Stopping.MaxGenerations = 100;
            o.Complexity.MaxConnections = -1;
        });

        act.Should().Throw<OptionsValidationException>();
    }

    private static NeatSharpOptions BuildAndValidate(Action<NeatSharpOptions> configure)
    {
        var services = new ServiceCollection();
        services.AddNeatSharp(configure);
        var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IOptions<NeatSharpOptions>>().Value;
    }
}
