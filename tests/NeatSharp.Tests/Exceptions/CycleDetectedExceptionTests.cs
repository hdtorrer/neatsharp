using FluentAssertions;
using NeatSharp.Exceptions;
using Xunit;

namespace NeatSharp.Tests.Exceptions;

public class CycleDetectedExceptionTests
{
    [Fact]
    public void Constructor_WithMessage_SetsMessage()
    {
        var message = "Cycle detected in genome topology";

        var exception = new CycleDetectedException(message);

        exception.Message.Should().Be(message);
        exception.InnerException.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithMessageAndInnerException_SetsBothProperties()
    {
        var message = "Cycle detected";
        var inner = new InvalidOperationException("Original error");

        var exception = new CycleDetectedException(message, inner);

        exception.Message.Should().Be(message);
        exception.InnerException.Should().BeSameAs(inner);
    }

    [Fact]
    public void CycleDetectedException_IsNeatSharpException()
    {
        var exception = new CycleDetectedException("test");

        exception.Should().BeAssignableTo<NeatSharpException>();
    }

    [Fact]
    public void CycleDetectedException_IsException()
    {
        var exception = new CycleDetectedException("test");

        exception.Should().BeAssignableTo<Exception>();
    }
}
