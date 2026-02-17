using NeatSharp.Genetics;

namespace NeatSharp.Gpu.Tests.TestDoubles;

internal sealed class StubGenome(int nodeCount, int connectionCount) : IGenome
{
    public int NodeCount => nodeCount;

    public int ConnectionCount => connectionCount;

    public void Activate(ReadOnlySpan<double> inputs, Span<double> outputs) { }
}
