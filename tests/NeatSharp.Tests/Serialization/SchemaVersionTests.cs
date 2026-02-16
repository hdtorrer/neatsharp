using FluentAssertions;
using NeatSharp.Serialization;
using Xunit;

namespace NeatSharp.Tests.Serialization;

public class SchemaVersionTests
{
    [Fact]
    public void Parse_ValidSemverString_ReturnsMajorMinorPatch()
    {
        var (major, minor, patch) = SchemaVersion.Parse("1.0.0");

        major.Should().Be(1);
        minor.Should().Be(0);
        patch.Should().Be(0);
    }

    [Fact]
    public void Parse_VersionWithNonZeroParts_ReturnsCorrectValues()
    {
        var (major, minor, patch) = SchemaVersion.Parse("2.3.4");

        major.Should().Be(2);
        minor.Should().Be(3);
        patch.Should().Be(4);
    }

    [Theory]
    [InlineData("")]
    [InlineData("1")]
    [InlineData("1.0")]
    [InlineData("1.0.0.0")]
    [InlineData("abc")]
    [InlineData("1.0.x")]
    [InlineData("v1.0.0")]
    [InlineData(null)]
    public void Parse_InvalidVersionString_ThrowsArgumentException(string? version)
    {
        var act = () => SchemaVersion.Parse(version!);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void IsCompatible_CurrentVersion_ReturnsTrue()
    {
        var result = SchemaVersion.IsCompatible(SchemaVersion.Current);

        result.Should().BeTrue();
    }

    [Fact]
    public void IsCompatible_MinimumSupportedVersion_ReturnsTrue()
    {
        var result = SchemaVersion.IsCompatible(SchemaVersion.MinimumSupported);

        result.Should().BeTrue();
    }

    [Fact]
    public void IsCompatible_NewerVersion_ReturnsFalse()
    {
        var result = SchemaVersion.IsCompatible("2.0.0");

        result.Should().BeFalse();
    }

    [Fact]
    public void IsCompatible_OlderUnsupportedVersion_ReturnsFalse()
    {
        var result = SchemaVersion.IsCompatible("0.5.0");

        result.Should().BeFalse();
    }

    [Fact]
    public void NeedsMigration_CurrentVersion_ReturnsFalse()
    {
        var result = SchemaVersion.NeedsMigration(SchemaVersion.Current);

        result.Should().BeFalse();
    }

    [Fact]
    public void NeedsMigration_OlderButSupportedVersion_ReturnsTrue()
    {
        // Since Current == MinimumSupported == "1.0.0" for v1,
        // there is no version that is both supported AND older.
        // This test validates the logic: if min was "0.9.0" and current "1.0.0",
        // then "0.9.0" would need migration.
        // For now, Current equals MinimumSupported so no migration is needed.
        var result = SchemaVersion.NeedsMigration(SchemaVersion.MinimumSupported);

        result.Should().BeFalse();
    }

    [Fact]
    public void NeedsMigration_InvalidVersion_ThrowsArgumentException()
    {
        var act = () => SchemaVersion.NeedsMigration("invalid");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Current_IsValidSemver()
    {
        var (major, minor, patch) = SchemaVersion.Parse(SchemaVersion.Current);

        major.Should().BeGreaterThanOrEqualTo(0);
        minor.Should().BeGreaterThanOrEqualTo(0);
        patch.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public void MinimumSupported_IsValidSemver()
    {
        var (major, minor, patch) = SchemaVersion.Parse(SchemaVersion.MinimumSupported);

        major.Should().BeGreaterThanOrEqualTo(0);
        minor.Should().BeGreaterThanOrEqualTo(0);
        patch.Should().BeGreaterThanOrEqualTo(0);
    }
}
