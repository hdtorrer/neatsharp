using FluentAssertions;
using NeatSharp.Exceptions;
using Xunit;

namespace NeatSharp.Tests.Exceptions;

public class NeatSharpExceptionTests
{
    [Fact]
    public void Constructor_WithMessage_SetsMessage()
    {
        var message = "Something went wrong";

        var exception = new NeatSharpException(message);

        exception.Message.Should().Be(message);
        exception.InnerException.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithMessageAndInnerException_SetsBothProperties()
    {
        var message = "Wrapper error";
        var inner = new InvalidOperationException("Original error");

        var exception = new NeatSharpException(message, inner);

        exception.Message.Should().Be(message);
        exception.InnerException.Should().BeSameAs(inner);
    }

    [Fact]
    public void Constructor_WithMessageAndInnerException_PropagatesInnerExceptionMessage()
    {
        var innerMessage = "Root cause";
        var inner = new InvalidOperationException(innerMessage);

        var exception = new NeatSharpException("Wrapped", inner);

        exception.InnerException!.Message.Should().Be(innerMessage);
    }

    [Fact]
    public void NeatSharpException_IsException()
    {
        var exception = new NeatSharpException("test");

        exception.Should().BeAssignableTo<Exception>();
    }
}
