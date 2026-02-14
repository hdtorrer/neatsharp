using FluentAssertions;
using NeatSharp.Genetics;
using Xunit;

namespace NeatSharp.Tests.Genetics;

public class ActivationFunctionRegistryTests
{
    private readonly ActivationFunctionRegistry _registry = new();

    [Theory]
    [InlineData("sigmoid")]
    [InlineData("tanh")]
    [InlineData("relu")]
    [InlineData("step")]
    [InlineData("identity")]
    public void Get_BuiltInName_ReturnsFunction(string name)
    {
        var function = _registry.Get(name);

        function.Should().NotBeNull();
    }

    [Fact]
    public void Get_Sigmoid_ReturnsSigmoidFunction()
    {
        var function = _registry.Get("sigmoid");

        function(0.0).Should().Be(0.5);
    }

    [Fact]
    public void Get_Tanh_ReturnsTanhFunction()
    {
        var function = _registry.Get("tanh");

        function(0.0).Should().Be(0.0);
    }

    [Fact]
    public void Get_ReLU_ReturnsReLUFunction()
    {
        var function = _registry.Get("relu");

        function(-1.0).Should().Be(0.0);
        function(1.0).Should().Be(1.0);
    }

    [Fact]
    public void Get_Step_ReturnsStepFunction()
    {
        var function = _registry.Get("step");

        function(-0.1).Should().Be(0.0);
        function(0.1).Should().Be(1.0);
    }

    [Fact]
    public void Get_Identity_ReturnsIdentityFunction()
    {
        var function = _registry.Get("identity");

        function(42.0).Should().Be(42.0);
    }

    [Theory]
    [InlineData("Sigmoid")]
    [InlineData("SIGMOID")]
    [InlineData("SiGmOiD")]
    [InlineData("TANH")]
    [InlineData("Relu")]
    public void Get_CaseInsensitiveLookup_ReturnsFunction(string name)
    {
        var function = _registry.Get(name);

        function.Should().NotBeNull();
    }

    [Theory]
    [InlineData("sigmoid")]
    [InlineData("tanh")]
    [InlineData("relu")]
    [InlineData("step")]
    [InlineData("identity")]
    public void Contains_BuiltInName_ReturnsTrue(string name)
    {
        _registry.Contains(name).Should().BeTrue();
    }

    [Fact]
    public void Contains_UnknownName_ReturnsFalse()
    {
        _registry.Contains("nonexistent").Should().BeFalse();
    }

    [Fact]
    public void Contains_CaseInsensitive_ReturnsTrue()
    {
        _registry.Contains("SIGMOID").Should().BeTrue();
    }

    [Fact]
    public void Get_UnknownName_ThrowsArgumentException()
    {
        var act = () => _registry.Get("nonexistent");

        act.Should().Throw<ArgumentException>()
            .And.Message.Should().Contain("nonexistent");
    }

    [Fact]
    public void Register_DuplicateName_ThrowsArgumentException()
    {
        var act = () => _registry.Register("sigmoid", x => x);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Register_NovelName_Succeeds()
    {
        _registry.Register("leaky_relu", x => x > 0 ? x : 0.01 * x);

        _registry.Contains("leaky_relu").Should().BeTrue();
    }

    [Fact]
    public void Register_NovelName_GetRetrievesIt()
    {
        Func<double, double> leakyRelu = x => x > 0 ? x : 0.01 * x;
        _registry.Register("leaky_relu", leakyRelu);

        var retrieved = _registry.Get("leaky_relu");

        retrieved(-10.0).Should().BeApproximately(-0.1, 1e-10);
        retrieved(5.0).Should().Be(5.0);
    }
}
