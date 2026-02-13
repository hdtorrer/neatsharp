using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NeatSharp.Configuration;
using NeatSharp.Extensions;
using Xunit;

namespace NeatSharp.Tests.Configuration;

public class StoppingCriteriaTests
{
    [Fact]
    public void Validation_NoCriteriaSet_Fails()
    {
        var act = () => BuildAndValidate(o =>
        {
            o.Stopping.MaxGenerations = null;
            o.Stopping.FitnessTarget = null;
            o.Stopping.StagnationThreshold = null;
        });

        act.Should().Throw<OptionsValidationException>()
            .WithMessage("*At least one stopping criterion*");
    }

    [Fact]
    public void Validation_OnlyMaxGenerationsSet_Succeeds()
    {
        var act = () => BuildAndValidate(o => o.Stopping.MaxGenerations = 100);

        act.Should().NotThrow();
    }

    [Fact]
    public void Validation_OnlyFitnessTargetSet_Succeeds()
    {
        var act = () => BuildAndValidate(o => o.Stopping.FitnessTarget = 3.9);

        act.Should().NotThrow();
    }

    [Fact]
    public void Validation_OnlyStagnationThresholdSet_Succeeds()
    {
        var act = () => BuildAndValidate(o => o.Stopping.StagnationThreshold = 50);

        act.Should().NotThrow();
    }

    [Fact]
    public void Validation_MaxGenerationsZero_Fails()
    {
        var act = () => BuildAndValidate(o => o.Stopping.MaxGenerations = 0);

        act.Should().Throw<OptionsValidationException>();
    }

    [Fact]
    public void Validation_MaxGenerationsNegative_Fails()
    {
        var act = () => BuildAndValidate(o => o.Stopping.MaxGenerations = -1);

        act.Should().Throw<OptionsValidationException>();
    }

    [Fact]
    public void Validation_StagnationThresholdZero_Fails()
    {
        var act = () => BuildAndValidate(o => o.Stopping.StagnationThreshold = 0);

        act.Should().Throw<OptionsValidationException>();
    }

    [Fact]
    public void Validation_FitnessTargetInfinity_Fails()
    {
        var act = () => BuildAndValidate(o => o.Stopping.FitnessTarget = double.PositiveInfinity);

        act.Should().Throw<OptionsValidationException>()
            .WithMessage("*FitnessTarget must be a finite value*");
    }

    [Fact]
    public void Validation_FitnessTargetNaN_Fails()
    {
        var act = () => BuildAndValidate(o => o.Stopping.FitnessTarget = double.NaN);

        act.Should().Throw<OptionsValidationException>()
            .WithMessage("*FitnessTarget must be a finite value*");
    }

    [Fact]
    public void Defaults_AllFieldsAreNull()
    {
        var criteria = new StoppingCriteria();

        criteria.MaxGenerations.Should().BeNull();
        criteria.FitnessTarget.Should().BeNull();
        criteria.StagnationThreshold.Should().BeNull();
    }

    private static NeatSharpOptions BuildAndValidate(Action<NeatSharpOptions> configure)
    {
        var services = new ServiceCollection();
        services.AddNeatSharp(configure);
        var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IOptions<NeatSharpOptions>>().Value;
    }
}
