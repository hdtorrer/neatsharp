#if NET9_0_OR_GREATER
using FluentAssertions;
using NeatSharp.Samples.CartPole;
using Xunit;

namespace NeatSharp.Tests.Examples;

public class CartPoleSimulatorTests
{
    private const double Tolerance = 1e-10;

    [Fact]
    public void Step_KnownInput_ProducesCorrectState()
    {
        // Arrange: default config, initial state all zeros, apply +10N force
        var config = new CartPoleConfig();
        var sim = new CartPoleSimulator(config);

        // Act: one step with force = 10.0 (right)
        sim.Step(10.0);

        // Assert: verify Euler integration with analytically computed values
        // With initial state = (0,0,0,0) and force = 10.0:
        //   total_mass = 1.1, temp = 10.0/1.1 = 100/11
        //   theta_ddot = (-100/11) / (0.5 * (4/3 - 0.1/1.1)) = -6600/451
        //   x_ddot = 100/11 + 300/451 = 4400/451
        //   After dt=0.02:
        //     x = 0.0, x_dot = 0.02 * 4400/451, theta = 0.0, theta_dot = 0.02 * (-6600/451)
        sim.State.X.Should().BeApproximately(0.0, Tolerance);
        sim.State.XDot.Should().BeApproximately(0.02 * 4400.0 / 451.0, Tolerance);
        sim.State.Theta.Should().BeApproximately(0.0, Tolerance);
        sim.State.ThetaDot.Should().BeApproximately(0.02 * (-6600.0 / 451.0), Tolerance);
    }

    [Fact]
    public void Step_CartOffTrack_IsFailed()
    {
        // Arrange: use a very large failure angle so only position failure triggers,
        // and a large force magnitude to push cart off track quickly
        var config = new CartPoleConfig
        {
            ForceMagnitude = 1000.0,
            FailureAngle = 100.0 // effectively disable angle failure
        };
        var sim = new CartPoleSimulator(config);

        // Act: apply large rightward force repeatedly until cart is off track
        for (int i = 0; i < 10_000; i++)
        {
            sim.Step(config.ForceMagnitude);
            if (sim.IsFailed())
            {
                break;
            }
        }

        // Assert: cart should have gone off track (|x| > 2.4)
        sim.IsFailed().Should().BeTrue("cart should be off track after repeated large force");
        Math.Abs(sim.State.X).Should().BeGreaterThan(config.TrackHalfLength);
    }

    [Fact]
    public void Step_PoleAngleExceeded_IsFailed()
    {
        // Arrange: default config, apply force to destabilize pole
        var config = new CartPoleConfig();
        var sim = new CartPoleSimulator(config);

        // Act: apply rightward force -- the pole tips backward (negative theta)
        // With default parameters, even a single-direction force will cause angle failure
        // before position failure since the pole falls quickly
        bool angleFailed = false;
        for (int i = 0; i < 10_000; i++)
        {
            sim.Step(config.ForceMagnitude);
            if (Math.Abs(sim.State.Theta) > config.FailureAngle)
            {
                angleFailed = true;
                break;
            }
        }

        // Assert: pole angle should have exceeded failure threshold
        angleFailed.Should().BeTrue("pole angle should exceed 0.2094 radians (~12 degrees)");
        sim.IsFailed().Should().BeTrue();
    }

    [Fact]
    public void Constructor_DefaultConfig_InitialStateIsZeros()
    {
        // Arrange & Act
        var config = new CartPoleConfig();
        var sim = new CartPoleSimulator(config);

        // Assert: initial state should be all zeros
        sim.State.X.Should().Be(0.0);
        sim.State.XDot.Should().Be(0.0);
        sim.State.Theta.Should().Be(0.0);
        sim.State.ThetaDot.Should().Be(0.0);
    }

    [Fact]
    public void Reset_AfterSteps_ClearsState()
    {
        // Arrange
        var config = new CartPoleConfig();
        var sim = new CartPoleSimulator(config);

        // Act: take several steps to change state
        sim.Step(10.0);
        sim.Step(-10.0);
        sim.Step(5.0);

        // Verify state has changed
        var stateAfterSteps = sim.State;
        (stateAfterSteps.X == 0.0 && stateAfterSteps.XDot == 0.0
            && stateAfterSteps.Theta == 0.0 && stateAfterSteps.ThetaDot == 0.0)
            .Should().BeFalse("state should have changed after steps");

        // Reset
        sim.Reset();

        // Assert: state should be back to all zeros
        sim.State.X.Should().Be(0.0);
        sim.State.XDot.Should().Be(0.0);
        sim.State.Theta.Should().Be(0.0);
        sim.State.ThetaDot.Should().Be(0.0);
    }

    [Fact]
    public void Constructor_CustomConfig_ParametersRespected()
    {
        // Arrange: custom config with different gravity and mass
        var config = new CartPoleConfig
        {
            Gravity = 20.0,
            CartMass = 2.0,
            PoleMass = 0.5,
            PoleHalfLength = 1.0,
            TimeStep = 0.01
        };
        var simCustom = new CartPoleSimulator(config);

        var defaultConfig = new CartPoleConfig();
        var simDefault = new CartPoleSimulator(defaultConfig);

        // Act: apply same force to both simulators
        simCustom.Step(10.0);
        simDefault.Step(10.0);

        // Assert: different configs should produce different states
        // The custom config has different gravity, mass, and time step,
        // so the resulting state should differ from default
        simCustom.State.XDot.Should().NotBeApproximately(simDefault.State.XDot, 1e-6,
            "custom config parameters should produce different dynamics");
        simCustom.State.ThetaDot.Should().NotBeApproximately(simDefault.State.ThetaDot, 1e-6,
            "custom config parameters should produce different angular dynamics");

        // Additionally verify the custom config produces expected values:
        // total_mass = 2.0 + 0.5 = 2.5
        // temp = (10.0 + 0.5 * 1.0 * 0^2 * 0) / 2.5 = 4.0
        // theta_ddot = (20.0 * 0 - 1 * 4.0) / (1.0 * (4/3 - (0.5 * 1) / 2.5))
        //            = -4.0 / (4/3 - 0.2) = -4.0 / (17/15) = -60/17
        // x_ddot = 4.0 - (0.5 * 1.0 * (-60/17) * 1) / 2.5
        //        = 4.0 + 30/(17*2.5) = 4.0 + 12/17 = 80/17
        // After dt=0.01:
        //   x_dot = 0.01 * 80/17, theta_dot = 0.01 * (-60/17)
        simCustom.State.X.Should().BeApproximately(0.0, Tolerance);
        simCustom.State.XDot.Should().BeApproximately(0.01 * 80.0 / 17.0, Tolerance);
        simCustom.State.Theta.Should().BeApproximately(0.0, Tolerance);
        simCustom.State.ThetaDot.Should().BeApproximately(0.01 * (-60.0 / 17.0), Tolerance);
    }
}
#endif
