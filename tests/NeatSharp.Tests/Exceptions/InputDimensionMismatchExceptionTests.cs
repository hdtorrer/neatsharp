using FluentAssertions;
using NeatSharp.Exceptions;
using Xunit;

namespace NeatSharp.Tests.Exceptions;

public class InputDimensionMismatchExceptionTests
{
    [Fact]
    public void Constructor_WithExpectedAndActual_SetsProperties()
    {
        var exception = new InputDimensionMismatchException(expected: 3, actual: 5);

        exception.Expected.Should().Be(3);
        exception.Actual.Should().Be(5);
        exception.Message.Should().Contain("3");
        exception.Message.Should().Contain("5");
    }

    [Fact]
    public void Constructor_WithMessage_SetsMessage()
    {
        var message = "Input count mismatch";

        var exception = new InputDimensionMismatchException(message);

        exception.Message.Should().Contain(message);
    }

    [Fact]
    public void Constructor_WithMessageAndInnerException_SetsBothProperties()
    {
        var message = "Input mismatch";
        var inner = new InvalidOperationException("Original error");

        var exception = new InputDimensionMismatchException(message, inner);

        exception.Message.Should().Contain(message);
        exception.InnerException.Should().BeSameAs(inner);
    }

    [Fact]
    public void InputDimensionMismatchException_IsArgumentException()
    {
        var exception = new InputDimensionMismatchException(expected: 2, actual: 1);

        exception.Should().BeAssignableTo<ArgumentException>();
    }

    [Fact]
    public void InputDimensionMismatchException_IsNotNeatSharpException()
    {
        var exception = new InputDimensionMismatchException(expected: 2, actual: 1);

        exception.Should().NotBeAssignableTo<NeatSharpException>();
    }
}
