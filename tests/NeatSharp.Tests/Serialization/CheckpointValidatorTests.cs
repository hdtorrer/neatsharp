using FluentAssertions;
using NeatSharp.Configuration;
using NeatSharp.Genetics;
using NeatSharp.Reporting;
using NeatSharp.Serialization;
using Xunit;

namespace NeatSharp.Tests.Serialization;

public class CheckpointValidatorTests
{
    private readonly CheckpointValidator _validator = new();

    [Fact]
    public void Validate_ValidCheckpoint_ReturnsIsValidTrue()
    {
        var checkpoint = CheckpointTestHelper.CreateFullCheckpoint();

        var result = _validator.Validate(checkpoint);

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_EmptyPopulationCheckpoint_ReturnsIsValidTrue()
    {
        var checkpoint = CheckpointTestHelper.CreateEmptyPopulationCheckpoint();

        var result = _validator.Validate(checkpoint);

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_SpeciesReferencingOutOfRangeIndex_ReturnsError()
    {
        var genome = CheckpointTestHelper.CreateMinimalGenome();
        var checkpoint = new TrainingCheckpoint(
            Population: [genome],
            Species: [new SpeciesCheckpoint(1, RepresentativeIndex: 0, 0.5, 0, MemberIndices: [0, 5], MemberFitnesses: [0.5, 0.3])],
            NextInnovationNumber: 2,
            NextNodeId: 2,
            NextSpeciesId: 2,
            ChampionGenome: genome,
            ChampionFitness: 0.5,
            ChampionGeneration: 0,
            Generation: 1,
            Seed: 42,
            RngState: CreateTestRngState(),
            Configuration: CreateTestConfig(),
            ConfigurationHash: "abc123",
            History: new RunHistory([], 1),
            Metadata: CreateTestMetadata());

        var result = _validator.Validate(checkpoint);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("member index") || e.Contains("out of range") || e.Contains("5"));
    }

    [Fact]
    public void Validate_SpeciesRepresentativeIndexOutOfRange_ReturnsError()
    {
        var genome = CheckpointTestHelper.CreateMinimalGenome();
        var checkpoint = new TrainingCheckpoint(
            Population: [genome],
            Species: [new SpeciesCheckpoint(1, RepresentativeIndex: 10, 0.5, 0, MemberIndices: [0], MemberFitnesses: [0.5])],
            NextInnovationNumber: 2,
            NextNodeId: 2,
            NextSpeciesId: 2,
            ChampionGenome: genome,
            ChampionFitness: 0.5,
            ChampionGeneration: 0,
            Generation: 1,
            Seed: 42,
            RngState: CreateTestRngState(),
            Configuration: CreateTestConfig(),
            ConfigurationHash: "abc123",
            History: new RunHistory([], 1),
            Metadata: CreateTestMetadata());

        var result = _validator.Validate(checkpoint);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("representative") || e.Contains("10"));
    }

    [Fact]
    public void Validate_NextInnovationNumberNotGreaterThanMax_ReturnsError()
    {
        var genome = CheckpointTestHelper.CreateMinimalGenome(0, 1, 1);
        // Max innovation number is 1, NextInnovationNumber must be > 1
        var checkpoint = new TrainingCheckpoint(
            Population: [genome],
            Species: [new SpeciesCheckpoint(1, 0, 0.5, 0, [0], [0.5])],
            NextInnovationNumber: 1, // Should be > 1
            NextNodeId: 2,
            NextSpeciesId: 2,
            ChampionGenome: genome,
            ChampionFitness: 0.5,
            ChampionGeneration: 0,
            Generation: 1,
            Seed: 42,
            RngState: CreateTestRngState(),
            Configuration: CreateTestConfig(),
            ConfigurationHash: "abc123",
            History: new RunHistory([], 1),
            Metadata: CreateTestMetadata());

        var result = _validator.Validate(checkpoint);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("NextInnovationNumber") || e.Contains("innovation"));
    }

    [Fact]
    public void Validate_NextNodeIdNotGreaterThanMax_ReturnsError()
    {
        var genome = CheckpointTestHelper.CreateMinimalGenome(0, 1, 1);
        // Max node ID is 1, NextNodeId must be > 1
        var checkpoint = new TrainingCheckpoint(
            Population: [genome],
            Species: [new SpeciesCheckpoint(1, 0, 0.5, 0, [0], [0.5])],
            NextInnovationNumber: 2,
            NextNodeId: 1, // Should be > 1
            NextSpeciesId: 2,
            ChampionGenome: genome,
            ChampionFitness: 0.5,
            ChampionGeneration: 0,
            Generation: 1,
            Seed: 42,
            RngState: CreateTestRngState(),
            Configuration: CreateTestConfig(),
            ConfigurationHash: "abc123",
            History: new RunHistory([], 1),
            Metadata: CreateTestMetadata());

        var result = _validator.Validate(checkpoint);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("NextNodeId") || e.Contains("node"));
    }

    [Fact]
    public void Validate_NextSpeciesIdNotGreaterThanMax_ReturnsError()
    {
        var genome = CheckpointTestHelper.CreateMinimalGenome();
        var checkpoint = new TrainingCheckpoint(
            Population: [genome],
            Species: [new SpeciesCheckpoint(3, 0, 0.5, 0, [0], [0.5])],
            NextInnovationNumber: 2,
            NextNodeId: 2,
            NextSpeciesId: 3, // Should be > 3 (max species ID is 3)
            ChampionGenome: genome,
            ChampionFitness: 0.5,
            ChampionGeneration: 0,
            Generation: 1,
            Seed: 42,
            RngState: CreateTestRngState(),
            Configuration: CreateTestConfig(),
            ConfigurationHash: "abc123",
            History: new RunHistory([], 1),
            Metadata: CreateTestMetadata());

        var result = _validator.Validate(checkpoint);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("NextSpeciesId") || e.Contains("species"));
    }

    [Fact]
    public void Validate_ChampionNotInPopulation_ReturnsError()
    {
        var genome1 = CheckpointTestHelper.CreateMinimalGenome(0, 1, 1);
        var championGenome = CheckpointTestHelper.CreateGenomeWithHiddenNode(0, 1, 2, 1, 2, 3);
        // Champion genome is not in the population
        var checkpoint = new TrainingCheckpoint(
            Population: [genome1],
            Species: [new SpeciesCheckpoint(1, 0, 0.5, 0, [0], [0.5])],
            NextInnovationNumber: 4,
            NextNodeId: 3,
            NextSpeciesId: 2,
            ChampionGenome: championGenome,
            ChampionFitness: 0.95,
            ChampionGeneration: 0,
            Generation: 1,
            Seed: 42,
            RngState: CreateTestRngState(),
            Configuration: CreateTestConfig(),
            ConfigurationHash: "abc123",
            History: new RunHistory([], 1),
            Metadata: CreateTestMetadata());

        var result = _validator.Validate(checkpoint);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("champion") || e.Contains("Champion"));
    }

    [Fact]
    public void Validate_MultipleViolations_CollectsAllErrors()
    {
        var genome = CheckpointTestHelper.CreateMinimalGenome(0, 1, 1);
        // Multiple violations: NextInnovationNumber too low, NextNodeId too low,
        // species references out of range, NextSpeciesId too low
        var checkpoint = new TrainingCheckpoint(
            Population: [genome],
            Species: [new SpeciesCheckpoint(5, RepresentativeIndex: 99, 0.5, 0, MemberIndices: [50], MemberFitnesses: [0.5])],
            NextInnovationNumber: 0, // Should be > 1
            NextNodeId: 0,           // Should be > 1
            NextSpeciesId: 1,        // Should be > 5
            ChampionGenome: CheckpointTestHelper.CreateGenomeWithHiddenNode(), // Not in population
            ChampionFitness: 0.95,
            ChampionGeneration: 0,
            Generation: 1,
            Seed: 42,
            RngState: CreateTestRngState(),
            Configuration: CreateTestConfig(),
            ConfigurationHash: "abc123",
            History: new RunHistory([], 1),
            Metadata: CreateTestMetadata());

        var result = _validator.Validate(checkpoint);

        result.IsValid.Should().BeFalse();
        // Should have at least 4 errors: NextInnovationNumber, NextNodeId, NextSpeciesId, species index, champion
        result.Errors.Count.Should().BeGreaterThanOrEqualTo(4);
    }

    private static RngState CreateTestRngState()
    {
        var rng = new Random(42);
        return RngStateHelper.Capture(rng);
    }

    private static NeatSharpOptions CreateTestConfig()
    {
        return new NeatSharpOptions
        {
            InputCount = 2,
            OutputCount = 1,
            PopulationSize = 150,
            Seed = 42,
            Stopping = new StoppingCriteria { MaxGenerations = 100 }
        };
    }

    private static ArtifactMetadata CreateTestMetadata()
    {
        return new ArtifactMetadata(
            SchemaVersion.Current,
            "1.0.0",
            42,
            "hash",
            "2026-02-15T12:00:00Z",
            new EnvironmentInfo("Windows", ".NET 8", "X64"));
    }
}
