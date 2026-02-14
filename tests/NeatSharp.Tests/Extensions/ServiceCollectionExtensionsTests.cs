using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NeatSharp.Configuration;
using NeatSharp.Evolution;
using NeatSharp.Extensions;
using NeatSharp.Genetics;
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

    [Fact]
    public void AddNeatSharp_RegistersIActivationFunctionRegistryAsSingleton()
    {
        var services = new ServiceCollection();
        services.AddNeatSharp(o => o.Stopping.MaxGenerations = 100);
        var provider = services.BuildServiceProvider();

        var registry1 = provider.GetRequiredService<IActivationFunctionRegistry>();
        var registry2 = provider.GetRequiredService<IActivationFunctionRegistry>();

        registry1.Should().NotBeNull();
        registry1.Should().BeSameAs(registry2);
    }

    [Fact]
    public void AddNeatSharp_IActivationFunctionRegistry_ContainsFiveBuiltInFunctions()
    {
        var services = new ServiceCollection();
        services.AddNeatSharp(o => o.Stopping.MaxGenerations = 100);
        var provider = services.BuildServiceProvider();

        var registry = provider.GetRequiredService<IActivationFunctionRegistry>();

        registry.Contains(ActivationFunctions.Sigmoid).Should().BeTrue();
        registry.Contains(ActivationFunctions.Tanh).Should().BeTrue();
        registry.Contains(ActivationFunctions.ReLU).Should().BeTrue();
        registry.Contains(ActivationFunctions.Step).Should().BeTrue();
        registry.Contains(ActivationFunctions.Identity).Should().BeTrue();
    }

    [Fact]
    public void AddNeatSharp_RegistersINetworkBuilderAsSingleton()
    {
        var services = new ServiceCollection();
        services.AddNeatSharp(o => o.Stopping.MaxGenerations = 100);
        var provider = services.BuildServiceProvider();

        var builder1 = provider.GetRequiredService<INetworkBuilder>();
        var builder2 = provider.GetRequiredService<INetworkBuilder>();

        builder1.Should().NotBeNull();
        builder1.Should().BeSameAs(builder2);
    }

    [Fact]
    public void AddNeatSharp_INetworkBuilder_CanBuildSimpleGenome()
    {
        var services = new ServiceCollection();
        services.AddNeatSharp(o => o.Stopping.MaxGenerations = 100);
        var provider = services.BuildServiceProvider();

        var builder = provider.GetRequiredService<INetworkBuilder>();
        var genome = new Genome(
            [new NodeGene(0, NodeType.Input), new NodeGene(1, NodeType.Output)],
            [new ConnectionGene(1, SourceNodeId: 0, TargetNodeId: 1, Weight: 1.0, IsEnabled: true)]);

        var network = builder.Build(genome);

        network.Should().NotBeNull();
        network.NodeCount.Should().Be(2);
        network.ConnectionCount.Should().Be(1);
    }

    [Fact]
    public void AddNeatSharp_RegistersIInnovationTrackerAsScoped()
    {
        var services = new ServiceCollection();
        services.AddNeatSharp(o => o.Stopping.MaxGenerations = 100);
        var provider = services.BuildServiceProvider();

        using var scope1 = provider.CreateScope();
        using var scope2 = provider.CreateScope();

        var tracker1a = scope1.ServiceProvider.GetRequiredService<IInnovationTracker>();
        var tracker1b = scope1.ServiceProvider.GetRequiredService<IInnovationTracker>();
        var tracker2 = scope2.ServiceProvider.GetRequiredService<IInnovationTracker>();

        // Same scope → same instance
        tracker1a.Should().BeSameAs(tracker1b);
        // Different scope → different instance
        tracker1a.Should().NotBeSameAs(tracker2);
    }

    [Fact]
    public void AddNeatSharp_ExistingRegistrations_StillResolveCorrectly()
    {
        var services = new ServiceCollection();
        services.AddNeatSharp(o => o.Stopping.MaxGenerations = 100);
        var provider = services.BuildServiceProvider();

        provider.GetService<IRunReporter>().Should().NotBeNull();
        provider.GetRequiredService<IOptions<NeatSharpOptions>>().Value.Should().NotBeNull();

        using var scope = provider.CreateScope();
        scope.ServiceProvider.GetService<INeatEvolver>().Should().NotBeNull();
    }
}
