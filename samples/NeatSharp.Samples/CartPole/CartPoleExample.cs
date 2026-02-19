using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NeatSharp.Evolution;
using NeatSharp.Extensions;
using NeatSharp.Genetics;
using NeatSharp.Reporting;

namespace NeatSharp.Samples.CartPole;

/// <summary>
/// Demonstrates NEAT solving the Cart-Pole (inverted pendulum) balancing task.
///
/// The Cart-Pole problem is a classic control benchmark: a pole is attached to a cart
/// that moves along a frictionless track. The neural network receives 4 inputs
/// (cart position, cart velocity, pole angle, pole angular velocity) and produces
/// 1 output that determines force direction (+10N right if output > 0.5, else -10N left).
///
/// Fitness is computed as the fraction of maximum time steps the pole remains balanced.
/// A fitness of 1.0 means the network balanced the pole for the entire episode.
/// </summary>
public static class CartPoleExample
{
    /// <summary>
    /// Runs the Cart-Pole evolution example.
    /// </summary>
    public static async Task RunAsync()
    {
        Console.WriteLine("--- Cart-Pole Balancing ---");
        Console.WriteLine();
        Console.WriteLine("Goal: Evolve a neural network to balance an inverted pendulum on a cart.");
        Console.WriteLine("Inputs: cart position (x), cart velocity (x_dot), pole angle (theta), pole angular velocity (theta_dot)");
        Console.WriteLine("Output: force direction (>0.5 = push right +10N, else push left -10N)");
        Console.WriteLine();

        // Cart-Pole physics configuration with canonical Barto/Stanley parameters
        var config = new CartPoleConfig();

        // Configure NEAT evolution:
        // - 4 inputs (cart state) and 1 output (force direction)
        // - Population of 150 genomes
        // - Fixed seed for reproducible results
        // - Stop after 100 generations or when fitness reaches 0.95 (9,500+ steps balanced)
        var services = new ServiceCollection();
        services.AddNeatSharp(options =>
        {
            options.InputCount = 4;    // x, x_dot, theta, theta_dot
            options.OutputCount = 1;   // force direction
            options.PopulationSize = 150;
            options.Seed = 42;
            options.EnableMetrics = true;
            options.Stopping.MaxGenerations = 100;
            options.Stopping.FitnessTarget = 0.95;
        });
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var evolver = scope.ServiceProvider.GetRequiredService<INeatEvolver>();

        // Run evolution with the Cart-Pole fitness function
        var sw = Stopwatch.StartNew();
        var result = await evolver.RunAsync(genome =>
        {
            var simulator = new CartPoleSimulator(config);
            int stepsBalanced = 0;
            Span<double> output = stackalloc double[1];

            // Run one episode: feed network outputs to physics, count balanced steps
            for (int step = 0; step < config.MaxSteps; step++)
            {
                // Feed cart state as network inputs (normalized to reasonable ranges)
                double[] inputs =
                [
                    simulator.State.X / config.TrackHalfLength,       // Normalize position to [-1, 1]
                    simulator.State.XDot / 2.0,                       // Normalize velocity
                    simulator.State.Theta / config.FailureAngle,      // Normalize angle to [-1, 1]
                    simulator.State.ThetaDot / 2.0                    // Normalize angular velocity
                ];

                // Get network output and interpret as force direction
                genome.Activate(inputs, output);

                // Binary force: push right (+ForceMagnitude) if output > 0.5, else push left (-ForceMagnitude)
                double force = output[0] > 0.5 ? config.ForceMagnitude : -config.ForceMagnitude;
                simulator.Step(force);

                if (simulator.IsFailed())
                {
                    break;
                }

                stepsBalanced++;
            }

            // Fitness = fraction of maximum steps survived
            return (double)stepsBalanced / config.MaxSteps;
        });
        sw.Stop();

        // Print evolution summary
        var reporter = scope.ServiceProvider.GetRequiredService<IRunReporter>();
        Console.WriteLine(reporter.GenerateSummary(result));
        Console.WriteLine($"Elapsed: {sw.Elapsed.TotalMilliseconds:F0} ms");

        // Evaluate champion one more time to show detailed results
        Console.WriteLine();
        Console.WriteLine("Champion Cart-Pole results:");
        var champion = result.Champion.Genome;
        var evalSim = new CartPoleSimulator(config);
        int championSteps = 0;
        Span<double> evalOutput = stackalloc double[1];

        for (int step = 0; step < config.MaxSteps; step++)
        {
            double[] inputs =
            [
                evalSim.State.X / config.TrackHalfLength,
                evalSim.State.XDot / 2.0,
                evalSim.State.Theta / config.FailureAngle,
                evalSim.State.ThetaDot / 2.0
            ];

            champion.Activate(inputs, evalOutput);

            double force = evalOutput[0] > 0.5 ? config.ForceMagnitude : -config.ForceMagnitude;
            evalSim.Step(force);

            if (evalSim.IsFailed())
            {
                break;
            }

            championSteps++;
        }

        Console.WriteLine($"  Steps balanced: {championSteps} / {config.MaxSteps}");
        Console.WriteLine($"  Final state: x={evalSim.State.X:F4}, x_dot={evalSim.State.XDot:F4}, theta={evalSim.State.Theta:F4}, theta_dot={evalSim.State.ThetaDot:F4}");
        Console.WriteLine($"  Champion fitness: {result.Champion.Fitness:F4}");

        // Metrics snapshot: show per-generation progress
        if (result.History.Generations.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("Generation progress (every 10th):");
            Console.WriteLine($"  {"Gen",-5} {"Best",8} {"Avg",8} {"Species",8} {"AvgNodes",9} {"AvgConns",9}");
            foreach (var gen in result.History.Generations)
            {
                if (gen.Generation % 10 == 0 || gen.Generation == result.History.TotalGenerations - 1)
                {
                    Console.WriteLine(
                        $"  {gen.Generation,-5} {gen.BestFitness,8:F4} {gen.AverageFitness,8:F4} {gen.SpeciesCount,8} {gen.Complexity.AverageNodes,9:F1} {gen.Complexity.AverageConnections,9:F1}");
                }
            }
        }
    }
}
