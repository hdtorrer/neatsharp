using NeatSharp.Genetics;

namespace NeatSharp.Evaluation;

/// <summary>
/// Evaluates a single genome by running it through a multi-step
/// environment (episodes) and returning a fitness score derived
/// from the episode outcomes.
/// </summary>
/// <remarks>
/// <para>
/// Implement this interface when your fitness evaluation requires
/// sequential decision-making — for example, game AI, robotics
/// simulations, or control problems where the genome observes state
/// and produces actions over multiple time steps.
/// </para>
/// <para>
/// The implementation manages the episode loop internally:
/// reset the environment, feed observations to the genome via
/// <see cref="IGenome.Activate"/>, apply the genome's outputs
/// as actions, accumulate rewards, and return the final fitness.
/// </para>
/// </remarks>
public interface IEnvironmentEvaluator
{
    /// <summary>
    /// Runs the genome through an environment and returns its fitness score.
    /// </summary>
    /// <param name="genome">The genome to evaluate.</param>
    /// <param name="cancellationToken">Token to observe for cancellation.</param>
    /// <returns>The fitness score derived from the episode outcomes.</returns>
    Task<double> EvaluateAsync(IGenome genome, CancellationToken cancellationToken);
}
