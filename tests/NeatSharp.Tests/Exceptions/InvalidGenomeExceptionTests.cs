using FluentAssertions;
using NeatSharp.Exceptions;
using Xunit;

namespace NeatSharp.Tests.Exceptions;

public class InvalidGenomeExceptionTests
{
    [Fact]
    public void Constructor_WithMessage_SetsMessage()
    {
        var message = "Genome has duplicate node IDs";

        var exception = new InvalidGenomeException(message);

        exception.Message.Should().Be(message);
        exception.InnerException.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithMessageAndInnerException_SetsBothProperties()
    {
        var message = "Invalid genome structure";
        var inner = new InvalidOperationException("Original error");

        var exception = new InvalidGenomeException(message, inner);

        exception.Message.Should().Be(message);
        exception.InnerException.Should().BeSameAs(inner);
    }

    [Fact]
    public void InvalidGenomeException_IsNeatSharpException()
    {
        var exception = new InvalidGenomeException("test");

        exception.Should().BeAssignableTo<NeatSharpException>();
    }

    [Fact]
    public void InvalidGenomeException_IsException()
    {
        var exception = new InvalidGenomeException("test");

        exception.Should().BeAssignableTo<Exception>();
    }
}
