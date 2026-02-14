using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NeatSharp.Configuration;
using NeatSharp.Evolution;
using NeatSharp.Evolution.Crossover;
using NeatSharp.Evolution.Mutation;
using NeatSharp.Evolution.Selection;
using NeatSharp.Evolution.Speciation;
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

    // --- Evolution services (Spec 003) ---

    private static ServiceProvider BuildProviderWithDefaults()
    {
        var services = new ServiceCollection();
        services.AddNeatSharp(o => o.Stopping.MaxGenerations = 100);
        return services.BuildServiceProvider();
    }

    [Fact]
    public void AddNeatSharp_RegistersMutationOperatorConcreteTypesAsSingletons()
    {
        var provider = BuildProviderWithDefaults();

        var wp1 = provider.GetRequiredService<WeightPerturbationMutation>();
        var wp2 = provider.GetRequiredService<WeightPerturbationMutation>();
        wp1.Should().BeSameAs(wp2);

        var wr1 = provider.GetRequiredService<WeightReplacementMutation>();
        var wr2 = provider.GetRequiredService<WeightReplacementMutation>();
        wr1.Should().BeSameAs(wr2);

        var ac1 = provider.GetRequiredService<AddConnectionMutation>();
        var ac2 = provider.GetRequiredService<AddConnectionMutation>();
        ac1.Should().BeSameAs(ac2);

        var an1 = provider.GetRequiredService<AddNodeMutation>();
        var an2 = provider.GetRequiredService<AddNodeMutation>();
        an1.Should().BeSameAs(an2);

        var te1 = provider.GetRequiredService<ToggleEnableMutation>();
        var te2 = provider.GetRequiredService<ToggleEnableMutation>();
        te1.Should().BeSameAs(te2);
    }

    [Fact]
    public void AddNeatSharp_RegistersAllMutationOperatorsAsIMutationOperator()
    {
        var provider = BuildProviderWithDefaults();

        var operators = provider.GetServices<IMutationOperator>().ToList();

        operators.Should().HaveCount(5);
        operators.Should().ContainSingle(o => o is WeightPerturbationMutation);
        operators.Should().ContainSingle(o => o is WeightReplacementMutation);
        operators.Should().ContainSingle(o => o is AddConnectionMutation);
        operators.Should().ContainSingle(o => o is AddNodeMutation);
        operators.Should().ContainSingle(o => o is ToggleEnableMutation);
    }

    [Fact]
    public void AddNeatSharp_IMutationOperator_ReturnsSameInstancesAsConcreteTypes()
    {
        var provider = BuildProviderWithDefaults();

        var operators = provider.GetServices<IMutationOperator>().ToList();

        operators.OfType<WeightPerturbationMutation>().Single()
            .Should().BeSameAs(provider.GetRequiredService<WeightPerturbationMutation>());
        operators.OfType<WeightReplacementMutation>().Single()
            .Should().BeSameAs(provider.GetRequiredService<WeightReplacementMutation>());
        operators.OfType<AddConnectionMutation>().Single()
            .Should().BeSameAs(provider.GetRequiredService<AddConnectionMutation>());
        operators.OfType<AddNodeMutation>().Single()
            .Should().BeSameAs(provider.GetRequiredService<AddNodeMutation>());
        operators.OfType<ToggleEnableMutation>().Single()
            .Should().BeSameAs(provider.GetRequiredService<ToggleEnableMutation>());
    }

    [Fact]
    public void AddNeatSharp_RegistersCompositeMutationOperatorAsSingleton()
    {
        var provider = BuildProviderWithDefaults();

        var composite1 = provider.GetRequiredService<CompositeMutationOperator>();
        var composite2 = provider.GetRequiredService<CompositeMutationOperator>();

        composite1.Should().NotBeNull();
        composite1.Should().BeSameAs(composite2);
    }

    [Fact]
    public void AddNeatSharp_RegistersICrossoverOperatorAsSingleton()
    {
        var provider = BuildProviderWithDefaults();

        var crossover1 = provider.GetRequiredService<ICrossoverOperator>();
        var crossover2 = provider.GetRequiredService<ICrossoverOperator>();

        crossover1.Should().NotBeNull();
        crossover1.Should().BeOfType<NeatCrossover>();
        crossover1.Should().BeSameAs(crossover2);
    }

    [Fact]
    public void AddNeatSharp_RegistersICompatibilityDistanceAsSingleton()
    {
        var provider = BuildProviderWithDefaults();

        var distance1 = provider.GetRequiredService<ICompatibilityDistance>();
        var distance2 = provider.GetRequiredService<ICompatibilityDistance>();

        distance1.Should().NotBeNull();
        distance1.Should().BeOfType<CompatibilityDistance>();
        distance1.Should().BeSameAs(distance2);
    }

    [Fact]
    public void AddNeatSharp_RegistersISpeciationStrategyAsScoped()
    {
        var provider = BuildProviderWithDefaults();

        using var scope1 = provider.CreateScope();
        using var scope2 = provider.CreateScope();

        var strategy1a = scope1.ServiceProvider.GetRequiredService<ISpeciationStrategy>();
        var strategy1b = scope1.ServiceProvider.GetRequiredService<ISpeciationStrategy>();
        var strategy2 = scope2.ServiceProvider.GetRequiredService<ISpeciationStrategy>();

        strategy1a.Should().NotBeNull();
        strategy1a.Should().BeOfType<CompatibilitySpeciation>();
        // Same scope → same instance
        strategy1a.Should().BeSameAs(strategy1b);
        // Different scope → different instance
        strategy1a.Should().NotBeSameAs(strategy2);
    }

    [Fact]
    public void AddNeatSharp_RegistersDefaultIParentSelectorAsTournamentSelector()
    {
        var provider = BuildProviderWithDefaults();

        var selector = provider.GetRequiredService<IParentSelector>();

        selector.Should().NotBeNull();
        selector.Should().BeOfType<TournamentSelector>();
    }

    [Fact]
    public void AddNeatSharp_RegistersIParentSelectorAsSingleton()
    {
        var provider = BuildProviderWithDefaults();

        var selector1 = provider.GetRequiredService<IParentSelector>();
        var selector2 = provider.GetRequiredService<IParentSelector>();

        selector1.Should().BeSameAs(selector2);
    }

    [Fact]
    public void AddNeatSharp_CustomIParentSelector_OverridesDefault()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IParentSelector, RouletteWheelSelector>();
        services.AddNeatSharp(o => o.Stopping.MaxGenerations = 100);
        var provider = services.BuildServiceProvider();

        var selector = provider.GetRequiredService<IParentSelector>();

        selector.Should().BeOfType<RouletteWheelSelector>();
    }

    [Fact]
    public void AddNeatSharp_CustomIParentSelector_OverridesDefault_WhenRegisteredAfter()
    {
        var services = new ServiceCollection();
        services.AddNeatSharp(o => o.Stopping.MaxGenerations = 100);
        // User registers after AddNeatSharp — last registration wins for non-TryAdd
        // But since we use TryAddSingleton, registering before is the supported way.
        // Registering after with AddSingleton also works because it adds another descriptor.
        services.AddSingleton<IParentSelector, SinglePointerSelector>();
        var provider = services.BuildServiceProvider();

        var selector = provider.GetRequiredService<IParentSelector>();

        // Last registration wins for GetRequiredService
        selector.Should().BeOfType<SinglePointerSelector>();
    }

    [Fact]
    public void AddNeatSharp_RegistersReproductionAllocatorAsSingleton()
    {
        var provider = BuildProviderWithDefaults();

        var allocator1 = provider.GetRequiredService<ReproductionAllocator>();
        var allocator2 = provider.GetRequiredService<ReproductionAllocator>();

        allocator1.Should().NotBeNull();
        allocator1.Should().BeSameAs(allocator2);
    }

    [Fact]
    public void AddNeatSharp_RegistersReproductionOrchestratorAsSingleton()
    {
        var provider = BuildProviderWithDefaults();

        var orchestrator1 = provider.GetRequiredService<ReproductionOrchestrator>();
        var orchestrator2 = provider.GetRequiredService<ReproductionOrchestrator>();

        orchestrator1.Should().NotBeNull();
        orchestrator1.Should().BeSameAs(orchestrator2);
    }
}
