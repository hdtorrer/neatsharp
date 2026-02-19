namespace NeatSharp.Samples.CartPole;

/// <summary>
/// Mutable state of the cart-pole system.
/// </summary>
public struct CartPoleState
{
    /// <summary>Cart position (meters from center).</summary>
    public double X;

    /// <summary>Cart velocity (m/s).</summary>
    public double XDot;

    /// <summary>Pole angle from vertical (radians; positive = right).</summary>
    public double Theta;

    /// <summary>Pole angular velocity (rad/s).</summary>
    public double ThetaDot;
}

/// <summary>
/// Cart-pole (inverted pendulum) physics simulator using standard Euler integration
/// with canonical Barto/Stanley equations.
/// </summary>
public sealed class CartPoleSimulator
{
    private readonly CartPoleConfig _config;
    private CartPoleState _state;

    public CartPoleSimulator(CartPoleConfig config)
    {
        _config = config;
        _state = default;
        _state.Theta = _config.InitialTheta;
        _state.ThetaDot = _config.InitialThetaDot;
    }

    /// <summary>
    /// Gets the current simulation state.
    /// </summary>
    public CartPoleState State => _state;

    /// <summary>
    /// Advances the simulation by one time step using the given force applied to the cart.
    /// Implements standard Euler integration per Barto/Stanley equations.
    /// </summary>
    /// <param name="force">Force applied to the cart in Newtons (positive = right).</param>
    public void Step(double force)
    {
        double sinTheta = Math.Sin(_state.Theta);
        double cosTheta = Math.Cos(_state.Theta);
        double totalMass = _config.CartMass + _config.PoleMass;

        double temp = (force + _config.PoleMass * _config.PoleHalfLength
            * _state.ThetaDot * _state.ThetaDot * sinTheta) / totalMass;

        double thetaDdot = (_config.Gravity * sinTheta - cosTheta * temp)
            / (_config.PoleHalfLength * (4.0 / 3.0
                - (_config.PoleMass * cosTheta * cosTheta) / totalMass));

        double xDdot = temp - (_config.PoleMass * _config.PoleHalfLength
            * thetaDdot * cosTheta) / totalMass;

        double dt = _config.TimeStep;

        _state.X += dt * _state.XDot;
        _state.XDot += dt * xDdot;
        _state.Theta += dt * _state.ThetaDot;
        _state.ThetaDot += dt * thetaDdot;
    }

    /// <summary>
    /// Checks whether the simulation has entered a failure state.
    /// Failure occurs when the cart leaves the track or the pole angle exceeds the threshold.
    /// </summary>
    /// <returns>True if either failure condition is met.</returns>
    public bool IsFailed()
    {
        return Math.Abs(_state.X) > _config.TrackHalfLength
            || Math.Abs(_state.Theta) > _config.FailureAngle;
    }

    /// <summary>
    /// Resets the simulation state to all zeros.
    /// </summary>
    public void Reset()
    {
        _state = default;
        _state.Theta = _config.InitialTheta;
        _state.ThetaDot = _config.InitialThetaDot;
    }
}
