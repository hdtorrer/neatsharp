using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NeatSharp.Configuration;
using NeatSharp.Evolution;
using NeatSharp.Extensions;
using NeatSharp.Reporting;
using Xunit;

namespace NeatSharp.Tests.Extensions;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddNeatSharp_RegistersINeatEvolver()
    {
        var services = new ServiceCollection();
        services.AddNeatSharp(o => o.Stopping.MaxGenerations = 100);
        var provider = services.BuildServiceProvider();

        using var scope = provider.CreateScope();
        var evolver = scope.ServiceProvider.GetService<INeatEvolver>();

        evolver.Should().NotBeNull();
    }

    [Fact]
    public void AddNeatSharp_ConfiguresOptionsViaAction()
    {
        var services = new ServiceCollection();
        services.AddNeatSharp(o =>
        {
            o.PopulationSize = 200;
            o.Seed = 42;
            o.Stopping.MaxGenerations = 500;
        });
        var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<NeatSharpOptions>>().Value;

        options.PopulationSize.Should().Be(200);
        options.Seed.Should().Be(42);
        options.Stopping.MaxGenerations.Should().Be(500);
    }

    [Fact]
    public void AddNeatSharp_WithoutConfigure_UsesDefaults()
    {
        var services = new ServiceCollection();
        services.AddNeatSharp();

        // Should not throw during registration
        services.Should().NotBeEmpty();
    }

    [Fact]
    public void AddNeatSharp_ValidateOnStart_RejectsInvalidOptions()
    {
        var services = new ServiceCollection();
        services.AddNeatSharp(o =>
        {
            o.PopulationSize = 0; // Invalid: must be >= 1
        });
        var provider = services.BuildServiceProvider();

        var act = () => provider.GetRequiredService<IOptions<NeatSharpOptions>>().Value;

        act.Should().Throw<OptionsValidationException>();
    }

    [Fact]
    public void AddNeatSharp_ValidateOnStart_RejectsNoStoppingCriteria()
    {
        var services = new ServiceCollection();
        services.AddNeatSharp(o =>
        {
            // All stopping criteria are null by default — should fail
        });
        var provider = services.BuildServiceProvider();

        var act = () => provider.GetRequiredService<IOptions<NeatSharpOptions>>().Value;

        act.Should().Throw<OptionsValidationException>()
            .WithMessage("*At least one stopping criterion*");
    }

    [Fact]
    public void AddNeatSharp_ReturnsServiceCollectionForChaining()
    {
        var services = new ServiceCollection();

        var result = services.AddNeatSharp(o => o.Stopping.MaxGenerations = 100);

        result.Should().BeSameAs(services);
    }

    [Fact]
    public void AddNeatSharp_RegistersIRunReporter()
    {
        var services = new ServiceCollection();
        services.AddNeatSharp(o => o.Stopping.MaxGenerations = 100);
        var provider = services.BuildServiceProvider();

        var reporter = provider.GetService<IRunReporter>();

        reporter.Should().NotBeNull();
        reporter.Should().BeOfType<RunReporter>();
    }

    [Fact]
    public void AddNeatSharp_RegistersIRunReporterAsSingleton()
    {
        var services = new ServiceCollection();
        services.AddNeatSharp(o => o.Stopping.MaxGenerations = 100);
        var provider = services.BuildServiceProvider();

        var reporter1 = provider.GetRequiredService<IRunReporter>();
        var reporter2 = provider.GetRequiredService<IRunReporter>();

        reporter1.Should().BeSameAs(reporter2);
    }
}
