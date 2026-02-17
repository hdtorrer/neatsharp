namespace NeatSharp.Gpu.Scheduling;

/// <summary>
/// Discrete PID controller for adaptive CPU/GPU partition ratio.
/// </summary>
/// <remarks>
/// <para>
/// The error signal is defined as:
/// <c>error = (cpuLatency - gpuLatency) / max(cpuLatency, gpuLatency)</c>
/// </para>
/// <list type="bullet">
///   <item>Positive error: CPU is slower → GPU has spare capacity → increase GPU fraction.</item>
///   <item>Negative error: GPU is slower → CPU has spare capacity → decrease GPU fraction.</item>
///   <item>Zero error: backends are balanced → no adjustment.</item>
/// </list>
/// <para>
/// Output is clamped to [0, 1]. Anti-windup prevents integral accumulation when the output
/// is saturated and the integral would push further into saturation.
/// </para>
/// </remarks>
internal sealed class PidController
{
    private readonly double _kp;
    private readonly double _ki;
    private readonly double _kd;

    private double _integral;
    private double _previousError;

    /// <summary>
    /// Initializes a new <see cref="PidController"/> with the specified gains and initial GPU fraction.
    /// </summary>
    /// <param name="kp">Proportional gain.</param>
    /// <param name="ki">Integral gain.</param>
    /// <param name="kd">Derivative gain.</param>
    /// <param name="initialGpuFraction">Starting GPU fraction in [0, 1].</param>
    public PidController(double kp, double ki, double kd, double initialGpuFraction)
    {
        _kp = kp;
        _ki = ki;
        _kd = kd;
        GpuFraction = initialGpuFraction;
    }

    /// <summary>
    /// Gets the current GPU fraction controlled by the PID loop.
    /// </summary>
    public double GpuFraction { get; private set; }

    /// <summary>
    /// Computes a new GPU fraction given the current error signal.
    /// </summary>
    /// <param name="error">
    /// Error signal in [-1, 1]. Positive means CPU is slower (increase GPU fraction);
    /// negative means GPU is slower (decrease GPU fraction).
    /// </param>
    /// <returns>The updated GPU fraction, clamped to [0, 1].</returns>
    public double Compute(double error)
    {
        // 1. Accumulate integral
        _integral += error;

        // 2. Compute derivative
        var derivative = error - _previousError;

        // 3. Compute PID delta
        var delta = _kp * error + _ki * _integral + _kd * derivative;

        // 4. Compute new fraction
        var newFraction = GpuFraction + delta;

        // 5. Clamp to [0, 1]
        var clampedFraction = Math.Clamp(newFraction, 0.0, 1.0);

        // 6. Anti-windup: if output was clamped AND integral pushes in the same
        //    direction as the error, undo the integral accumulation
        // ReSharper disable once CompareOfFloatsByEqualityOperator
        if (clampedFraction != newFraction && Math.Sign(_integral) == Math.Sign(error))
        {
            _integral -= error;
        }

        // 7. Update state
        _previousError = error;
        GpuFraction = clampedFraction;

        // 8. Return the new GPU fraction
        return GpuFraction;
    }

    /// <summary>
    /// Resets the controller state to the specified initial GPU fraction.
    /// Clears integral accumulator and previous error.
    /// </summary>
    /// <param name="initialGpuFraction">The GPU fraction to reset to.</param>
    public void Reset(double initialGpuFraction)
    {
        GpuFraction = initialGpuFraction;
        _integral = 0.0;
        _previousError = 0.0;
    }
}
