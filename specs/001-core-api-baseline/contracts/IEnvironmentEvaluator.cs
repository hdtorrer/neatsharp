// Contract definition — not compilable source code.
// This file defines the user-facing interface for episode-based evaluation.

namespace NeatSharp.Evaluation;

/// <summary>
/// Evaluates a single genome by running it through a multi-step
/// environment (episodes) and returning a fitness score derived
/// from the episode outcomes.
/// </summary>
/// <remarks>
/// Implement this interface when your fitness evaluation requires
/// sequential decision-making — for example, game AI, robotics
/// simulations, or control problems where the genome observes state
/// and produces actions over multiple time steps.
///
/// The implementation manages the episode loop internally:
/// reset the environment, feed observations to the genome via
/// <see cref="IGenome.Activate"/>, apply the genome's outputs
/// as actions, accumulate rewards, and return the final fitness.
///
/// Example: pole balancing, where the genome receives cart position
/// and pole angle as inputs and outputs a force to apply.
/// </remarks>
/// <example>
/// <code>
/// public class PoleBalancingEvaluator : IEnvironmentEvaluator
/// {
///     public Task&lt;double&gt; EvaluateAsync(
///         IGenome genome, CancellationToken cancellationToken)
///     {
///         var env = new PoleBalancingEnvironment();
///         double totalReward = 0;
///
///         while (!env.IsDone)
///         {
///             cancellationToken.ThrowIfCancellationRequested();
///             Span&lt;double&gt; outputs = stackalloc double[1];
///             genome.Activate(env.Observation, outputs);
///             totalReward += env.Step(outputs[0]);
///         }
///
///         return Task.FromResult(totalReward);
///     }
/// }
/// </code>
/// </example>
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
