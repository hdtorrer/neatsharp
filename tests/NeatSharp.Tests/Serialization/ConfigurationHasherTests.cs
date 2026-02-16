using FluentAssertions;
using NeatSharp.Configuration;
using NeatSharp.Serialization;
using Xunit;

namespace NeatSharp.Tests.Serialization;

public class ConfigurationHasherTests
{
    [Fact]
    public void ComputeHash_SameInstance_ReturnsDeterministicHash()
    {
        var options = new NeatSharpOptions
        {
            InputCount = 3,
            OutputCount = 1,
            PopulationSize = 150
        };

        var hash1 = ConfigurationHasher.ComputeHash(options);
        var hash2 = ConfigurationHasher.ComputeHash(options);

        hash1.Should().Be(hash2);
    }

    [Fact]
    public void ComputeHash_SameConfigAcrossCalls_ReturnsSameHash()
    {
        var options1 = new NeatSharpOptions
        {
            InputCount = 2,
            OutputCount = 1,
            PopulationSize = 100,
            Seed = 42
        };

        var options2 = new NeatSharpOptions
        {
            InputCount = 2,
            OutputCount = 1,
            PopulationSize = 100,
            Seed = 42
        };

        var hash1 = ConfigurationHasher.ComputeHash(options1);
        var hash2 = ConfigurationHasher.ComputeHash(options2);

        hash1.Should().Be(hash2);
    }

    [Fact]
    public void ComputeHash_DifferentConfigs_ProduceDifferentHashes()
    {
        var options1 = new NeatSharpOptions
        {
            InputCount = 2,
            OutputCount = 1,
            PopulationSize = 100
        };

        var options2 = new NeatSharpOptions
        {
            InputCount = 3,
            OutputCount = 2,
            PopulationSize = 200
        };

        var hash1 = ConfigurationHasher.ComputeHash(options1);
        var hash2 = ConfigurationHasher.ComputeHash(options2);

        hash1.Should().NotBe(hash2);
    }

    [Fact]
    public void ComputeHash_Returns64CharLowercaseHexSha256()
    {
        var options = new NeatSharpOptions();

        var hash = ConfigurationHasher.ComputeHash(options);

        hash.Should().HaveLength(64);
        hash.Should().MatchRegex("^[0-9a-f]{64}$");
    }

    [Fact]
    public void ComputeHash_DefaultOptions_ReturnsConsistentHash()
    {
        var hash1 = ConfigurationHasher.ComputeHash(new NeatSharpOptions());
        var hash2 = ConfigurationHasher.ComputeHash(new NeatSharpOptions());

        hash1.Should().Be(hash2);
    }
}
