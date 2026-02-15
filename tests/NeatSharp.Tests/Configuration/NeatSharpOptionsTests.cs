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
    public void Defaults_InputCount_Is2()
    {
        var options = new NeatSharpOptions();

        options.InputCount.Should().Be(2);
    }

    [Fact]
    public void Defaults_OutputCount_Is1()
    {
        var options = new NeatSharpOptions();

        options.OutputCount.Should().Be(1);
    }

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

    // --- Validation: InputCount ---

    [Fact]
    public void Validation_InputCountZero_Fails()
    {
        var act = () => BuildAndValidate(o => o.InputCount = 0);

        act.Should().Throw<OptionsValidationException>();
    }

    [Fact]
    public void Validation_InputCountNegative_Fails()
    {
        var act = () => BuildAndValidate(o => o.InputCount = -1);

        act.Should().Throw<OptionsValidationException>();
    }

    [Fact]
    public void Validation_InputCountOne_Succeeds()
    {
        var act = () => BuildAndValidate(o =>
        {
            o.InputCount = 1;
            o.Stopping.MaxGenerations = 100;
        });

        act.Should().NotThrow();
    }

    [Fact]
    public void Validation_InputCount10000_Succeeds()
    {
        var act = () => BuildAndValidate(o =>
        {
            o.InputCount = 10_000;
            o.Stopping.MaxGenerations = 100;
        });

        act.Should().NotThrow();
    }

    [Fact]
    public void Validation_InputCountExceedsMax_Fails()
    {
        var act = () => BuildAndValidate(o => o.InputCount = 10_001);

        act.Should().Throw<OptionsValidationException>();
    }

    // --- Validation: OutputCount ---

    [Fact]
    public void Validation_OutputCountZero_Fails()
    {
        var act = () => BuildAndValidate(o => o.OutputCount = 0);

        act.Should().Throw<OptionsValidationException>();
    }

    [Fact]
    public void Validation_OutputCountNegative_Fails()
    {
        var act = () => BuildAndValidate(o => o.OutputCount = -1);

        act.Should().Throw<OptionsValidationException>();
    }

    [Fact]
    public void Validation_OutputCountOne_Succeeds()
    {
        var act = () => BuildAndValidate(o =>
        {
            o.OutputCount = 1;
            o.Stopping.MaxGenerations = 100;
        });

        act.Should().NotThrow();
    }

    [Fact]
    public void Validation_OutputCount10000_Succeeds()
    {
        var act = () => BuildAndValidate(o =>
        {
            o.OutputCount = 10_000;
            o.Stopping.MaxGenerations = 100;
        });

        act.Should().NotThrow();
    }

    [Fact]
    public void Validation_OutputCountExceedsMax_Fails()
    {
        var act = () => BuildAndValidate(o => o.OutputCount = 10_001);

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

    // --- New nested options: defaults ---

    [Fact]
    public void Defaults_Mutation_IsNotNull()
    {
        var options = new NeatSharpOptions();

        options.Mutation.Should().NotBeNull();
    }

    [Fact]
    public void Defaults_Crossover_IsNotNull()
    {
        var options = new NeatSharpOptions();

        options.Crossover.Should().NotBeNull();
    }

    [Fact]
    public void Defaults_Speciation_IsNotNull()
    {
        var options = new NeatSharpOptions();

        options.Speciation.Should().NotBeNull();
    }

    [Fact]
    public void Defaults_Selection_IsNotNull()
    {
        var options = new NeatSharpOptions();

        options.Selection.Should().NotBeNull();
    }

    [Fact]
    public void Defaults_ComplexityPenalty_IsNotNull()
    {
        var options = new NeatSharpOptions();

        options.ComplexityPenalty.Should().NotBeNull();
    }

    // --- MutationOptions defaults ---

    [Fact]
    public void MutationDefaults_MatchNeatPaper()
    {
        var mutation = new MutationOptions();

        mutation.WeightPerturbationRate.Should().Be(0.8);
        mutation.WeightReplacementRate.Should().Be(0.1);
        mutation.AddConnectionRate.Should().Be(0.05);
        mutation.AddNodeRate.Should().Be(0.03);
        mutation.ToggleEnableRate.Should().Be(0.01);
        mutation.PerturbationPower.Should().Be(0.5);
        mutation.PerturbationDistribution.Should().Be(WeightDistributionType.Uniform);
        mutation.WeightMinValue.Should().Be(-4.0);
        mutation.WeightMaxValue.Should().Be(4.0);
        mutation.MaxAddConnectionAttempts.Should().Be(20);
    }

    // --- CrossoverOptions defaults ---

    [Fact]
    public void CrossoverDefaults_MatchNeatPaper()
    {
        var crossover = new CrossoverOptions();

        crossover.CrossoverRate.Should().Be(0.75);
        crossover.InterspeciesCrossoverRate.Should().Be(0.001);
        crossover.DisabledGeneInheritanceProbability.Should().Be(0.75);
    }

    // --- SpeciationOptions defaults ---

    [Fact]
    public void SpeciationDefaults_MatchNeatPaper()
    {
        var speciation = new SpeciationOptions();

        speciation.ExcessCoefficient.Should().Be(1.0);
        speciation.DisjointCoefficient.Should().Be(1.0);
        speciation.WeightDifferenceCoefficient.Should().Be(0.4);
        speciation.CompatibilityThreshold.Should().Be(3.0);
    }

    // --- SelectionOptions defaults ---

    [Fact]
    public void SelectionDefaults_MatchNeatPaper()
    {
        var selection = new SelectionOptions();

        selection.ElitismThreshold.Should().Be(5);
        selection.StagnationThreshold.Should().Be(15);
        selection.SurvivalThreshold.Should().Be(0.2);
        selection.TournamentSize.Should().Be(2);
    }

    // --- ComplexityPenaltyOptions defaults ---

    [Fact]
    public void ComplexityPenaltyDefaults_DisabledByDefault()
    {
        var penalty = new ComplexityPenaltyOptions();

        penalty.Coefficient.Should().Be(0.0);
        penalty.Metric.Should().Be(ComplexityPenaltyMetric.Both);
    }

    // --- Enum values ---

    [Fact]
    public void WeightDistributionType_HasExpectedValues()
    {
        Enum.GetValues<WeightDistributionType>().Should().BeEquivalentTo(
            [WeightDistributionType.Uniform, WeightDistributionType.Gaussian]);
    }

    [Fact]
    public void ComplexityPenaltyMetric_HasExpectedValues()
    {
        Enum.GetValues<ComplexityPenaltyMetric>().Should().BeEquivalentTo(
            [ComplexityPenaltyMetric.NodeCount, ComplexityPenaltyMetric.ConnectionCount, ComplexityPenaltyMetric.Both]);
    }

    // --- Validation: MutationOptions ---

    [Fact]
    public void Validation_DefaultOptions_Succeeds()
    {
        var act = () => BuildAndValidate(o => o.Stopping.MaxGenerations = 100);

        act.Should().NotThrow();
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(1.01)]
    public void Validation_MutationWeightPerturbationRate_OutOfRange_Fails(double rate)
    {
        var act = () => BuildAndValidate(o =>
        {
            o.Stopping.MaxGenerations = 100;
            o.Mutation.WeightPerturbationRate = rate;
        });

        act.Should().Throw<OptionsValidationException>()
            .WithMessage("*Mutation.WeightPerturbationRate*");
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(1.01)]
    public void Validation_MutationWeightReplacementRate_OutOfRange_Fails(double rate)
    {
        var act = () => BuildAndValidate(o =>
        {
            o.Stopping.MaxGenerations = 100;
            o.Mutation.WeightReplacementRate = rate;
        });

        act.Should().Throw<OptionsValidationException>()
            .WithMessage("*Mutation.WeightReplacementRate*");
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(1.01)]
    public void Validation_MutationAddConnectionRate_OutOfRange_Fails(double rate)
    {
        var act = () => BuildAndValidate(o =>
        {
            o.Stopping.MaxGenerations = 100;
            o.Mutation.AddConnectionRate = rate;
        });

        act.Should().Throw<OptionsValidationException>()
            .WithMessage("*Mutation.AddConnectionRate*");
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(1.01)]
    public void Validation_MutationAddNodeRate_OutOfRange_Fails(double rate)
    {
        var act = () => BuildAndValidate(o =>
        {
            o.Stopping.MaxGenerations = 100;
            o.Mutation.AddNodeRate = rate;
        });

        act.Should().Throw<OptionsValidationException>()
            .WithMessage("*Mutation.AddNodeRate*");
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(1.01)]
    public void Validation_MutationToggleEnableRate_OutOfRange_Fails(double rate)
    {
        var act = () => BuildAndValidate(o =>
        {
            o.Stopping.MaxGenerations = 100;
            o.Mutation.ToggleEnableRate = rate;
        });

        act.Should().Throw<OptionsValidationException>()
            .WithMessage("*Mutation.ToggleEnableRate*");
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-1.0)]
    public void Validation_MutationPerturbationPower_NotPositive_Fails(double power)
    {
        var act = () => BuildAndValidate(o =>
        {
            o.Stopping.MaxGenerations = 100;
            o.Mutation.PerturbationPower = power;
        });

        act.Should().Throw<OptionsValidationException>()
            .WithMessage("*Mutation.PerturbationPower*");
    }

    [Fact]
    public void Validation_MutationWeightMinNotLessThanMax_Fails()
    {
        var act = () => BuildAndValidate(o =>
        {
            o.Stopping.MaxGenerations = 100;
            o.Mutation.WeightMinValue = 5.0;
            o.Mutation.WeightMaxValue = 5.0;
        });

        act.Should().Throw<OptionsValidationException>()
            .WithMessage("*Mutation.WeightMinValue*");
    }

    [Fact]
    public void Validation_MutationWeightMinGreaterThanMax_Fails()
    {
        var act = () => BuildAndValidate(o =>
        {
            o.Stopping.MaxGenerations = 100;
            o.Mutation.WeightMinValue = 5.0;
            o.Mutation.WeightMaxValue = 2.0;
        });

        act.Should().Throw<OptionsValidationException>()
            .WithMessage("*Mutation.WeightMinValue*");
    }

    [Fact]
    public void Validation_MutationMaxAddConnectionAttempts_Zero_Fails()
    {
        var act = () => BuildAndValidate(o =>
        {
            o.Stopping.MaxGenerations = 100;
            o.Mutation.MaxAddConnectionAttempts = 0;
        });

        act.Should().Throw<OptionsValidationException>()
            .WithMessage("*Mutation.MaxAddConnectionAttempts*");
    }

    // --- Validation: Boundary values for mutation rates ---

    [Fact]
    public void Validation_MutationRates_BoundaryValues_Succeed()
    {
        var act = () => BuildAndValidate(o =>
        {
            o.Stopping.MaxGenerations = 100;
            o.Mutation.WeightPerturbationRate = 0.0;
            o.Mutation.WeightReplacementRate = 1.0;
            o.Mutation.AddConnectionRate = 0.0;
            o.Mutation.AddNodeRate = 1.0;
            o.Mutation.ToggleEnableRate = 0.0;
        });

        act.Should().NotThrow();
    }

    // --- Validation: CrossoverOptions ---

    [Theory]
    [InlineData(-0.01)]
    [InlineData(1.01)]
    public void Validation_CrossoverRate_OutOfRange_Fails(double rate)
    {
        var act = () => BuildAndValidate(o =>
        {
            o.Stopping.MaxGenerations = 100;
            o.Crossover.CrossoverRate = rate;
        });

        act.Should().Throw<OptionsValidationException>()
            .WithMessage("*Crossover.CrossoverRate*");
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(1.01)]
    public void Validation_InterspeciesCrossoverRate_OutOfRange_Fails(double rate)
    {
        var act = () => BuildAndValidate(o =>
        {
            o.Stopping.MaxGenerations = 100;
            o.Crossover.InterspeciesCrossoverRate = rate;
        });

        act.Should().Throw<OptionsValidationException>()
            .WithMessage("*Crossover.InterspeciesCrossoverRate*");
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(1.01)]
    public void Validation_DisabledGeneInheritanceProbability_OutOfRange_Fails(double rate)
    {
        var act = () => BuildAndValidate(o =>
        {
            o.Stopping.MaxGenerations = 100;
            o.Crossover.DisabledGeneInheritanceProbability = rate;
        });

        act.Should().Throw<OptionsValidationException>()
            .WithMessage("*Crossover.DisabledGeneInheritanceProbability*");
    }

    // --- Validation: SpeciationOptions ---

    [Fact]
    public void Validation_SpeciationExcessCoefficient_Negative_Fails()
    {
        var act = () => BuildAndValidate(o =>
        {
            o.Stopping.MaxGenerations = 100;
            o.Speciation.ExcessCoefficient = -0.1;
        });

        act.Should().Throw<OptionsValidationException>()
            .WithMessage("*Speciation.ExcessCoefficient*");
    }

    [Fact]
    public void Validation_SpeciationDisjointCoefficient_Negative_Fails()
    {
        var act = () => BuildAndValidate(o =>
        {
            o.Stopping.MaxGenerations = 100;
            o.Speciation.DisjointCoefficient = -0.1;
        });

        act.Should().Throw<OptionsValidationException>()
            .WithMessage("*Speciation.DisjointCoefficient*");
    }

    [Fact]
    public void Validation_SpeciationWeightDifferenceCoefficient_Negative_Fails()
    {
        var act = () => BuildAndValidate(o =>
        {
            o.Stopping.MaxGenerations = 100;
            o.Speciation.WeightDifferenceCoefficient = -0.1;
        });

        act.Should().Throw<OptionsValidationException>()
            .WithMessage("*Speciation.WeightDifferenceCoefficient*");
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-1.0)]
    public void Validation_SpeciationCompatibilityThreshold_NotPositive_Fails(double threshold)
    {
        var act = () => BuildAndValidate(o =>
        {
            o.Stopping.MaxGenerations = 100;
            o.Speciation.CompatibilityThreshold = threshold;
        });

        act.Should().Throw<OptionsValidationException>()
            .WithMessage("*Speciation.CompatibilityThreshold*");
    }

    [Fact]
    public void Validation_SpeciationCoefficients_ZeroAllowed_Succeeds()
    {
        var act = () => BuildAndValidate(o =>
        {
            o.Stopping.MaxGenerations = 100;
            o.Speciation.ExcessCoefficient = 0.0;
            o.Speciation.DisjointCoefficient = 0.0;
            o.Speciation.WeightDifferenceCoefficient = 0.0;
        });

        act.Should().NotThrow();
    }

    // --- Validation: SelectionOptions ---

    [Fact]
    public void Validation_SelectionElitismThreshold_Zero_Fails()
    {
        var act = () => BuildAndValidate(o =>
        {
            o.Stopping.MaxGenerations = 100;
            o.Selection.ElitismThreshold = 0;
        });

        act.Should().Throw<OptionsValidationException>()
            .WithMessage("*Selection.ElitismThreshold*");
    }

    [Fact]
    public void Validation_SelectionStagnationThreshold_Zero_Fails()
    {
        var act = () => BuildAndValidate(o =>
        {
            o.Stopping.MaxGenerations = 100;
            o.Selection.StagnationThreshold = 0;
        });

        act.Should().Throw<OptionsValidationException>()
            .WithMessage("*Selection.StagnationThreshold*");
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-0.1)]
    [InlineData(1.01)]
    public void Validation_SelectionSurvivalThreshold_OutOfRange_Fails(double threshold)
    {
        var act = () => BuildAndValidate(o =>
        {
            o.Stopping.MaxGenerations = 100;
            o.Selection.SurvivalThreshold = threshold;
        });

        act.Should().Throw<OptionsValidationException>()
            .WithMessage("*Selection.SurvivalThreshold*");
    }

    [Fact]
    public void Validation_SelectionSurvivalThreshold_One_Succeeds()
    {
        var act = () => BuildAndValidate(o =>
        {
            o.Stopping.MaxGenerations = 100;
            o.Selection.SurvivalThreshold = 1.0;
        });

        act.Should().NotThrow();
    }

    [Fact]
    public void Validation_SelectionTournamentSize_Zero_Fails()
    {
        var act = () => BuildAndValidate(o =>
        {
            o.Stopping.MaxGenerations = 100;
            o.Selection.TournamentSize = 0;
        });

        act.Should().Throw<OptionsValidationException>()
            .WithMessage("*Selection.TournamentSize*");
    }

    // --- Validation: ComplexityPenaltyOptions ---

    [Fact]
    public void Validation_ComplexityPenaltyCoefficient_Negative_Fails()
    {
        var act = () => BuildAndValidate(o =>
        {
            o.Stopping.MaxGenerations = 100;
            o.ComplexityPenalty.Coefficient = -0.1;
        });

        act.Should().Throw<OptionsValidationException>()
            .WithMessage("*ComplexityPenalty.Coefficient*");
    }

    [Fact]
    public void Validation_ComplexityPenaltyCoefficient_Zero_Succeeds()
    {
        var act = () => BuildAndValidate(o =>
        {
            o.Stopping.MaxGenerations = 100;
            o.ComplexityPenalty.Coefficient = 0.0;
        });

        act.Should().NotThrow();
    }

    // --- Validation: Multiple failures reported ---

    [Fact]
    public void Validation_MultipleInvalidOptions_ReportsAllFailures()
    {
        var act = () => BuildAndValidate(o =>
        {
            o.Stopping.MaxGenerations = 100;
            o.Mutation.WeightPerturbationRate = -1.0;
            o.Mutation.PerturbationPower = 0.0;
            o.Crossover.CrossoverRate = 2.0;
            o.Selection.ElitismThreshold = 0;
        });

        act.Should().Throw<OptionsValidationException>()
            .WithMessage("*Mutation.WeightPerturbationRate*")
            .WithMessage("*Mutation.PerturbationPower*")
            .WithMessage("*Crossover.CrossoverRate*")
            .WithMessage("*Selection.ElitismThreshold*");
    }

    private static NeatSharpOptions BuildAndValidate(Action<NeatSharpOptions> configure)
    {
        var services = new ServiceCollection();
        services.AddNeatSharp(configure);
        var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IOptions<NeatSharpOptions>>().Value;
    }
}
