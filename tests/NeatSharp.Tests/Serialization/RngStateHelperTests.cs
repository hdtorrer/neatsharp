using FluentAssertions;
using NeatSharp.Serialization;
using Xunit;

namespace NeatSharp.Tests.Serialization;

public class RngStateHelperTests
{
    [Fact]
    public void CaptureAndRestore_SeededRandom_ProducesIdenticalNextNValues()
    {
        var rng = new Random(42);

        // Advance the RNG a few steps
        for (int i = 0; i < 10; i++)
        {
            rng.Next();
        }

        // Capture state
        var state = RngStateHelper.Capture(rng);

        // Generate reference values
        var expected = new int[100];
        for (int i = 0; i < expected.Length; i++)
        {
            expected[i] = rng.Next();
        }

        // Restore state
        RngStateHelper.Restore(rng, state);

        // Generate values again — should be identical
        var actual = new int[100];
        for (int i = 0; i < actual.Length; i++)
        {
            actual[i] = rng.Next();
        }

        actual.Should().Equal(expected);
    }

    [Fact]
    public void CaptureAndRestore_WorksWithSeededRandom()
    {
        var rng = new Random(12345);

        var state = RngStateHelper.Capture(rng);

        var value1 = rng.Next();

        RngStateHelper.Restore(rng, state);

        var value2 = rng.Next();

        value2.Should().Be(value1);
    }

    [Fact]
    public void RoundTrip_PreservesSeedArrayInextInextp()
    {
        var rng = new Random(99);

        // Advance to get non-trivial inext/inextp values
        for (int i = 0; i < 50; i++)
        {
            rng.Next();
        }

        var captured = RngStateHelper.Capture(rng);

        captured.SeedArray.Should().HaveCount(56);
        captured.Inext.Should().BeInRange(0, 55);
        captured.Inextp.Should().BeInRange(0, 55);

        // Create a new Random and restore state
        var rng2 = new Random(0);
        RngStateHelper.Restore(rng2, captured);

        var recaptured = RngStateHelper.Capture(rng2);

        recaptured.SeedArray.Should().Equal(captured.SeedArray);
        recaptured.Inext.Should().Be(captured.Inext);
        recaptured.Inextp.Should().Be(captured.Inextp);
    }

    [Fact]
    public void Capture_ReturnsCopyOfSeedArray_NotReference()
    {
        var rng = new Random(42);

        var state1 = RngStateHelper.Capture(rng);
        rng.Next(); // Advance RNG
        var state2 = RngStateHelper.Capture(rng);

        // The arrays should be different since we advanced the RNG
        state1.SeedArray.Should().NotEqual(state2.SeedArray);
    }

    [Fact]
    public void CaptureAndRestore_NextDouble_ProducesIdenticalValues()
    {
        var rng = new Random(42);

        // Advance
        for (int i = 0; i < 25; i++)
        {
            rng.NextDouble();
        }

        var state = RngStateHelper.Capture(rng);

        var expected = new double[50];
        for (int i = 0; i < expected.Length; i++)
        {
            expected[i] = rng.NextDouble();
        }

        RngStateHelper.Restore(rng, state);

        var actual = new double[50];
        for (int i = 0; i < actual.Length; i++)
        {
            actual[i] = rng.NextDouble();
        }

        actual.Should().Equal(expected);
    }
}
