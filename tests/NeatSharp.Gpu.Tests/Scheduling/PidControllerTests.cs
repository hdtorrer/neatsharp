using FluentAssertions;
using NeatSharp.Gpu.Scheduling;
using Xunit;

namespace NeatSharp.Gpu.Tests.Scheduling;

public class PidControllerTests
{
    private const double DefaultKp = 0.5;
    private const double DefaultKi = 0.1;
    private const double DefaultKd = 0.05;
    private const double DefaultInitialFraction = 0.5;

    private static PidController CreateDefault() =>
        new(DefaultKp, DefaultKi, DefaultKd, DefaultInitialFraction);

    // --- Zero error produces no adjustment ---

    [Fact]
    public void Compute_ZeroError_GpuFractionUnchanged()
    {
        var pid = CreateDefault();

        var result = pid.Compute(0.0);

        result.Should().Be(DefaultInitialFraction);
        pid.GpuFraction.Should().Be(DefaultInitialFraction);
    }

    [Fact]
    public void Compute_ZeroErrorMultipleTimes_GpuFractionRemainsStable()
    {
        var pid = CreateDefault();

        for (var i = 0; i < 10; i++)
        {
            pid.Compute(0.0);
        }

        pid.GpuFraction.Should().Be(DefaultInitialFraction);
    }

    // --- Positive error (CPU slower → GPU has spare capacity → increase GPU fraction) ---

    [Fact]
    public void Compute_PositiveError_IncreasesGpuFraction()
    {
        var pid = CreateDefault();

        var result = pid.Compute(0.5);

        result.Should().BeGreaterThan(DefaultInitialFraction);
    }

    [Fact]
    public void Compute_LargePositiveError_IncreasesGpuFractionSignificantly()
    {
        var pid = CreateDefault();

        var result = pid.Compute(1.0);

        // delta = Kp*1.0 + Ki*1.0 + Kd*(1.0-0.0) = 0.5 + 0.1 + 0.05 = 0.65
        // new fraction = 0.5 + 0.65 = 1.15 → clamped to 1.0
        result.Should().Be(1.0);
    }

    // --- Negative error (GPU slower → CPU has spare capacity → decrease GPU fraction) ---

    [Fact]
    public void Compute_NegativeError_DecreasesGpuFraction()
    {
        var pid = CreateDefault();

        var result = pid.Compute(-0.5);

        result.Should().BeLessThan(DefaultInitialFraction);
    }

    [Fact]
    public void Compute_LargeNegativeError_DecreasesGpuFractionSignificantly()
    {
        var pid = CreateDefault();

        var result = pid.Compute(-1.0);

        // delta = Kp*(-1.0) + Ki*(-1.0) + Kd*(-1.0-0.0) = -0.5 + -0.1 + -0.05 = -0.65
        // new fraction = 0.5 + (-0.65) = -0.15 → clamped to 0.0
        result.Should().Be(0.0);
    }

    // --- Output clamped to [0, 1] ---

    [Fact]
    public void Compute_ResultCannotExceedOne_ClampedToUpperBound()
    {
        // Start near the top and push upward
        var pid = new PidController(kp: 1.0, ki: 0.0, kd: 0.0, initialGpuFraction: 0.9);

        var result = pid.Compute(0.5);

        // delta = 1.0*0.5 = 0.5, new = 0.9 + 0.5 = 1.4 → clamped to 1.0
        result.Should().Be(1.0);
        pid.GpuFraction.Should().Be(1.0);
    }

    [Fact]
    public void Compute_ResultCannotGoBelowZero_ClampedToLowerBound()
    {
        // Start near the bottom and push downward
        var pid = new PidController(kp: 1.0, ki: 0.0, kd: 0.0, initialGpuFraction: 0.1);

        var result = pid.Compute(-0.5);

        // delta = 1.0*(-0.5) = -0.5, new = 0.1 + (-0.5) = -0.4 → clamped to 0.0
        result.Should().Be(0.0);
        pid.GpuFraction.Should().Be(0.0);
    }

    // --- Anti-windup: integral stops accumulating when saturated ---

    [Fact]
    public void Compute_SaturatedAtUpperBound_IntegralDoesNotAccumulate()
    {
        // Use only integral gain to isolate anti-windup behavior
        var pid = new PidController(kp: 0.0, ki: 1.0, kd: 0.0, initialGpuFraction: 0.95);

        // First call: error=1.0, integral=1.0, delta=1.0, new=1.95 → clamped to 1.0
        // Anti-windup: sign(integral=1.0) == sign(error=1.0) → undo integral
        pid.Compute(1.0);
        pid.GpuFraction.Should().Be(1.0);

        // Second call with same positive error: if anti-windup works,
        // integral should NOT have been growing, so backing off should respond quickly
        // Now send negative error to come back down
        var result = pid.Compute(-0.5);

        // Without anti-windup, integral would be 2.0 + (-0.5) = 1.5, delta=1.5, new=1.0+1.5=2.5→1.0 (stuck!)
        // With anti-windup, integral stayed ~0 (the first integral was undone),
        // so integral = 0 + (-0.5) = -0.5, delta = -0.5, new = 1.0 + (-0.5) = 0.5
        result.Should().BeLessThan(1.0, "anti-windup should allow recovery from saturation");
    }

    [Fact]
    public void Compute_SaturatedAtLowerBound_IntegralDoesNotAccumulate()
    {
        // Use only integral gain to isolate anti-windup behavior
        var pid = new PidController(kp: 0.0, ki: 1.0, kd: 0.0, initialGpuFraction: 0.05);

        // First call: error=-1.0, integral=-1.0, delta=-1.0, new=0.05-1.0=-0.95 → clamped to 0.0
        // Anti-windup: sign(integral=-1.0) == sign(error=-1.0) → undo integral
        pid.Compute(-1.0);
        pid.GpuFraction.Should().Be(0.0);

        // Now send positive error to recover
        var result = pid.Compute(0.5);

        // With anti-windup, integral was reset so we can recover
        result.Should().BeGreaterThan(0.0, "anti-windup should allow recovery from saturation");
    }

    [Fact]
    public void Compute_SaturatedButOppositeSignError_IntegralStillAccumulates()
    {
        // When saturated at upper bound but error is negative, integral should still accumulate
        // because sign(integral) != sign(error) — this allows recovery
        var pid = new PidController(kp: 0.0, ki: 0.5, kd: 0.0, initialGpuFraction: 1.0);

        // Negative error while at upper bound: integral should accumulate
        // because this helps move away from saturation
        var result = pid.Compute(-0.4);

        // integral = -0.4, delta = 0.5*(-0.4) = -0.2, new = 1.0 - 0.2 = 0.8
        result.Should().BeLessThan(1.0);
    }

    // --- Convergence within 10 steps ---

    [Fact]
    public void Compute_SteadyStateCpuTwiceSlower_ConvergesWithin10Steps()
    {
        // Simulate: CPU always takes 2x longer than GPU
        // This means error = (cpuLatency - gpuLatency) / max(cpuLatency, gpuLatency)
        // If cpu=2s, gpu=1s: error = (2-1)/2 = 0.5 (positive → increase GPU fraction)
        // The PID should converge to a fraction where the workloads balance

        var pid = CreateDefault();

        // We simulate a fixed error of 0.5 representing CPU being consistently slower.
        // In reality the error would decrease as GPU fraction increases (taking load off CPU),
        // but for convergence testing we use a diminishing error model.
        var previousFraction = pid.GpuFraction;
        var convergedAtStep = -1;

        for (var step = 0; step < 10; step++)
        {
            // Simulate error decreasing as GPU fraction increases (negative feedback)
            // Target fraction is ~0.67 (2/3 GPU for a 2:1 speed ratio)
            var targetFraction = 2.0 / 3.0;
            var error = (targetFraction - pid.GpuFraction) * 2.0; // Scale to make error signal meaningful
            error = Math.Clamp(error, -1.0, 1.0);

            pid.Compute(error);

            // Check convergence: fraction within 5 percentage points of target
            if (Math.Abs(pid.GpuFraction - targetFraction) < 0.05 && convergedAtStep < 0)
            {
                convergedAtStep = step;
            }

            previousFraction = pid.GpuFraction;
        }

        convergedAtStep.Should().BeGreaterThanOrEqualTo(0, "PID should converge");
        convergedAtStep.Should().BeLessThan(10, "PID should converge within 10 steps");
    }

    [Fact]
    public void Compute_SteadyStateError_FractionStabilizesOverLastFiveSteps()
    {
        var pid = CreateDefault();
        var fractions = new double[20];

        for (var step = 0; step < 20; step++)
        {
            // Simulate a workload where GPU is 1.5x faster → target ~0.6 GPU fraction
            var targetFraction = 0.6;
            var error = (targetFraction - pid.GpuFraction) * 2.0;
            error = Math.Clamp(error, -1.0, 1.0);

            pid.Compute(error);
            fractions[step] = pid.GpuFraction;
        }

        // Last 5 fractions should have < 5 percentage points variance
        var last5 = fractions[15..20];
        var maxVariance = last5.Max() - last5.Min();
        maxVariance.Should().BeLessThan(0.05, "fraction should stabilize over the last 5 steps");
    }

    // --- Derivative brakes response on decreasing error ---

    [Fact]
    public void Compute_ErrorDecreasedFromPrevious_DerivativeReducesDelta()
    {
        // When error decreases between steps, the derivative term (error - prevError)
        // is negative, which reduces the delta compared to P-only control.
        var pidPD = new PidController(kp: 0.5, ki: 0.0, kd: 0.3, initialGpuFraction: 0.5);
        var pidPOnly = new PidController(kp: 0.5, ki: 0.0, kd: 0.0, initialGpuFraction: 0.5);

        // First step: establish baseline with same error for both
        pidPD.Compute(0.4);
        pidPOnly.Compute(0.4);

        var fractionBeforePD = pidPD.GpuFraction;
        var fractionBeforeP = pidPOnly.GpuFraction;

        // Second step: decreased error (0.1 < 0.4)
        pidPD.Compute(0.1);
        pidPOnly.Compute(0.1);

        var deltaPD = pidPD.GpuFraction - fractionBeforePD;
        var deltaP = pidPOnly.GpuFraction - fractionBeforeP;

        // Derivative = (0.1 - 0.4) = -0.3, so PD delta should be smaller than P delta
        deltaPD.Should().BeLessThan(deltaP,
            "derivative should reduce the response when error is decreasing");
    }

    [Fact]
    public void Compute_DerivativeReactsToErrorChange_NotJustCurrentError()
    {
        var pid = new PidController(kp: 0.0, ki: 0.0, kd: 1.0, initialGpuFraction: 0.5);

        // First call: error=0.3, derivative = 0.3 - 0 = 0.3
        var first = pid.Compute(0.3);
        first.Should().BeApproximately(0.5 + 0.3, 0.001);

        // Second call: same error, derivative = 0.3 - 0.3 = 0.0 → no additional change
        var second = pid.Compute(0.3);
        second.Should().BeApproximately(first, 0.001, "derivative should be zero when error is unchanged");
    }

    // --- Reset clears state ---

    [Fact]
    public void Reset_AfterMultipleComputes_RestoresInitialState()
    {
        var pid = CreateDefault();

        // Drive state away from initial
        pid.Compute(0.5);
        pid.Compute(0.3);
        pid.Compute(-0.2);
        pid.GpuFraction.Should().NotBe(DefaultInitialFraction);

        // Reset to a new initial fraction
        pid.Reset(0.7);

        pid.GpuFraction.Should().Be(0.7);
    }

    [Fact]
    public void Reset_ClearsInternalState_NextComputeActsAsFirst()
    {
        var pid = new PidController(kp: 0.0, ki: 0.0, kd: 1.0, initialGpuFraction: 0.5);

        // First compute: derivative = 0.5 - 0.0 = 0.5
        pid.Compute(0.5);

        // Second compute: derivative = 0.3 - 0.5 = -0.2
        pid.Compute(0.3);

        // Reset
        pid.Reset(0.5);

        // After reset, previous error should be 0, so derivative = 0.4 - 0.0 = 0.4
        var result = pid.Compute(0.4);
        result.Should().BeApproximately(0.5 + 0.4, 0.001,
            "after reset, derivative should treat this as the first computation");
    }

    [Fact]
    public void Reset_ClearsIntegral_NoResidualAccumulation()
    {
        var pid = new PidController(kp: 0.0, ki: 1.0, kd: 0.0, initialGpuFraction: 0.5);

        // Accumulate integral over several steps
        pid.Compute(0.1);
        pid.Compute(0.1);
        pid.Compute(0.1);

        // Reset
        pid.Reset(0.5);

        // After reset, integral should be zero: only this step's error matters
        var result = pid.Compute(0.1);
        // integral = 0 + 0.1 = 0.1, delta = 1.0 * 0.1 = 0.1, new = 0.5 + 0.1 = 0.6
        result.Should().BeApproximately(0.6, 0.001,
            "after reset, integral should not carry over from previous state");
    }

    // --- Initial state ---

    [Fact]
    public void Constructor_SetsInitialGpuFraction()
    {
        var pid = new PidController(DefaultKp, DefaultKi, DefaultKd, initialGpuFraction: 0.3);

        pid.GpuFraction.Should().Be(0.3);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.5)]
    [InlineData(1.0)]
    public void Constructor_VariousInitialFractions_SetsCorrectly(double initial)
    {
        var pid = new PidController(DefaultKp, DefaultKi, DefaultKd, initial);

        pid.GpuFraction.Should().Be(initial);
    }

    // --- PID formula verification ---

    [Fact]
    public void Compute_FirstCall_AppliesFullPidFormula()
    {
        var pid = new PidController(kp: 0.5, ki: 0.1, kd: 0.05, initialGpuFraction: 0.5);

        var result = pid.Compute(0.4);

        // First call: prevError = 0
        // integral = 0 + 0.4 = 0.4
        // derivative = 0.4 - 0.0 = 0.4
        // delta = 0.5*0.4 + 0.1*0.4 + 0.05*0.4 = 0.2 + 0.04 + 0.02 = 0.26
        // new = 0.5 + 0.26 = 0.76
        result.Should().BeApproximately(0.76, 0.001);
    }

    [Fact]
    public void Compute_SecondCall_IncludesDerivativeFromPreviousError()
    {
        var pid = new PidController(kp: 0.5, ki: 0.1, kd: 0.05, initialGpuFraction: 0.5);

        pid.Compute(0.4);
        // After first: fraction=0.76, integral=0.4, prevError=0.4

        var result = pid.Compute(0.2);

        // Second call:
        // integral = 0.4 + 0.2 = 0.6
        // derivative = 0.2 - 0.4 = -0.2
        // delta = 0.5*0.2 + 0.1*0.6 + 0.05*(-0.2) = 0.1 + 0.06 + (-0.01) = 0.15
        // new = 0.76 + 0.15 = 0.91
        result.Should().BeApproximately(0.91, 0.001);
    }
}
