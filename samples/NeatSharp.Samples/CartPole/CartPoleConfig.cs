namespace NeatSharp.Samples.CartPole;

/// <summary>
/// Configuration for the Cart-Pole physics simulation using canonical Barto/Stanley parameters.
/// </summary>
public sealed record CartPoleConfig
{
    public double Gravity { get; init; } = 9.8;
    public double CartMass { get; init; } = 1.0;
    public double PoleMass { get; init; } = 0.1;
    public double PoleHalfLength { get; init; } = 0.5;
    public double ForceMagnitude { get; init; } = 10.0;
    public double TimeStep { get; init; } = 0.02;
    public int MaxSteps { get; init; } = 10_000;
    public double TrackHalfLength { get; init; } = 2.4;
    public double FailureAngle { get; init; } = 0.2094;
}
