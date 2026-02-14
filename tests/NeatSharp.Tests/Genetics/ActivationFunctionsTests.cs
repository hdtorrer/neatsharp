using FluentAssertions;
using NeatSharp.Genetics;
using Xunit;

namespace NeatSharp.Tests.Genetics;

public class ActivationFunctionsTests
{
    [Fact]
    public void Sigmoid_Constant_IsSigmoid()
    {
        ActivationFunctions.Sigmoid.Should().Be("sigmoid");
    }

    [Fact]
    public void Tanh_Constant_IsTanh()
    {
        ActivationFunctions.Tanh.Should().Be("tanh");
    }

    [Fact]
    public void ReLU_Constant_IsRelu()
    {
        ActivationFunctions.ReLU.Should().Be("relu");
    }

    [Fact]
    public void Step_Constant_IsStep()
    {
        ActivationFunctions.Step.Should().Be("step");
    }

    [Fact]
    public void Identity_Constant_IsIdentity()
    {
        ActivationFunctions.Identity.Should().Be("identity");
    }

    [Fact]
    public void SigmoidFunction_AtZero_ReturnsPointFive()
    {
        ActivationFunctions.SigmoidFunction(0.0).Should().Be(0.5);
    }

    [Fact]
    public void SigmoidFunction_LargePositive_ApproachesOne()
    {
        ActivationFunctions.SigmoidFunction(10.0).Should().BeApproximately(1.0, 1e-4);
    }

    [Fact]
    public void SigmoidFunction_LargeNegative_ApproachesZero()
    {
        ActivationFunctions.SigmoidFunction(-10.0).Should().BeApproximately(0.0, 1e-4);
    }

    [Fact]
    public void TanhFunction_AtZero_ReturnsZero()
    {
        ActivationFunctions.TanhFunction(0.0).Should().Be(0.0);
    }

    [Fact]
    public void TanhFunction_LargePositive_ApproachesOne()
    {
        ActivationFunctions.TanhFunction(10.0).Should().BeApproximately(1.0, 1e-4);
    }

    [Fact]
    public void TanhFunction_LargeNegative_ApproachesNegativeOne()
    {
        ActivationFunctions.TanhFunction(-10.0).Should().BeApproximately(-1.0, 1e-4);
    }

    [Fact]
    public void ReLUFunction_NegativeInput_ReturnsZero()
    {
        ActivationFunctions.ReLUFunction(-1.0).Should().Be(0.0);
    }

    [Fact]
    public void ReLUFunction_PositiveInput_ReturnsSameValue()
    {
        ActivationFunctions.ReLUFunction(1.0).Should().Be(1.0);
    }

    [Fact]
    public void ReLUFunction_Zero_ReturnsZero()
    {
        ActivationFunctions.ReLUFunction(0.0).Should().Be(0.0);
    }

    [Fact]
    public void StepFunction_NegativeInput_ReturnsZero()
    {
        ActivationFunctions.StepFunction(-0.1).Should().Be(0.0);
    }

    [Fact]
    public void StepFunction_PositiveInput_ReturnsOne()
    {
        ActivationFunctions.StepFunction(0.1).Should().Be(1.0);
    }

    [Fact]
    public void StepFunction_Zero_ReturnsZero()
    {
        ActivationFunctions.StepFunction(0.0).Should().Be(0.0);
    }

    [Fact]
    public void IdentityFunction_ReturnsInputUnchanged()
    {
        ActivationFunctions.IdentityFunction(42.0).Should().Be(42.0);
    }

    [Fact]
    public void IdentityFunction_NegativeValue_ReturnsInputUnchanged()
    {
        ActivationFunctions.IdentityFunction(-3.14).Should().Be(-3.14);
    }
}
