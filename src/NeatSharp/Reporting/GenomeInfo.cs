namespace NeatSharp.Reporting;

/// <summary>
/// Lightweight summary of a genome within a snapshot.
/// Avoids exposing full genome internals.
/// </summary>
/// <param name="Fitness">Fitness score.</param>
/// <param name="NodeCount">Number of nodes.</param>
/// <param name="ConnectionCount">Number of connections.</param>
public record GenomeInfo(double Fitness, int NodeCount, int ConnectionCount);
