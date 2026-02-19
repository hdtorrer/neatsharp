using System.Diagnostics;
using System.Globalization;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NeatSharp.Evolution;
using NeatSharp.Extensions;
using NeatSharp.Genetics;
using NeatSharp.Reporting;
using NeatSharp.Configuration;

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
            options.Speciation.CompatibilityThreshold = 3.0;
            options.InputCount = 4;    // x, x_dot, theta, theta_dot
            options.OutputCount = 1;   // force direction
            options.PopulationSize = 150;
            options.Seed = 21;
            options.EnableMetrics = true;
            options.Stopping.MaxGenerations = 1000;
            options.Stopping.FitnessTarget = 0.95;
        });
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var evolver = scope.ServiceProvider.GetRequiredService<INeatEvolver>();

        // Evaluate from multiple starting conditions to prevent trivial solutions.
        // A network must handle poles tilting in both directions and with angular velocity.
        (double theta, double thetaDot)[] trials =
        [
            (+config.InitialTheta, 0.0),
            (-config.InitialTheta, 0.0),
            (0.0, +1.0),
            (0.0, -1.0),
        ];

        // Run evolution with the Cart-Pole fitness function
        var sw = Stopwatch.StartNew();
        var result = await evolver.RunAsync(genome =>
        {
            double totalFitness = 0;

            Span<double> output = stackalloc double[1];

            // Test from multiple starting conditions so the network must
            // actually use its inputs rather than applying constant force
            foreach (var (theta, thetaDot) in trials)
            {
                var trialConfig = config with { InitialTheta = theta, InitialThetaDot = thetaDot };
                var simulator = new CartPoleSimulator(trialConfig);
                int stepsBalanced = 0;

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

                totalFitness += (double)stepsBalanced / config.MaxSteps;
            }

            // Average fitness across all trials
            return totalFitness / trials.Length;
        });
        sw.Stop();

        // Print evolution summary
        var reporter = scope.ServiceProvider.GetRequiredService<IRunReporter>();
        Console.WriteLine(reporter.GenerateSummary(result));
        Console.WriteLine($"Elapsed: {sw.Elapsed.TotalMilliseconds:F0} ms");

        // Evaluate champion one more time to show detailed results and record state for visualization
        Console.WriteLine();
        Console.WriteLine("Champion Cart-Pole results:");
        var champion = result.Champion.Genome;
        var evalSim = new CartPoleSimulator(config);
        int championSteps = 0;
        Span<double> evalOutput = stackalloc double[1];
        var stateHistory = new List<(double X, double Theta, double Force)>();

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
            stateHistory.Add((evalSim.State.X, evalSim.State.Theta, force));
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

        // Generate and open HTML visualization of the champion run
        OpenVisualization(stateHistory, config, championSteps);

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

    private static void OpenVisualization(
        List<(double X, double Theta, double Force)> history,
        CartPoleConfig config,
        int stepsBalanced)
    {
        // Sample frames: keep at most ~2000 frames for smooth browser playback
        const int maxFrames = 2000;
        int sampleInterval = Math.Max(1, history.Count / maxFrames);

        var json = new StringBuilder("[");
        for (int i = 0; i < history.Count; i += sampleInterval)
        {
            if (i > 0)
            {
                json.Append(',');
            }
            var (x, theta, force) = history[i];
            json.Append(CultureInfo.InvariantCulture, $"[{x:G6},{theta:G6},{force:G6}]");
        }
        json.Append(']');

        string html = GenerateHtml(json.ToString(), config, stepsBalanced, sampleInterval);

        string path = Path.Combine(Path.GetTempPath(), "neatsharp-cartpole.html");
        File.WriteAllText(path, html);

        Console.WriteLine();
        Console.WriteLine($"  Visualization: {path}");

        try
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch
        {
            Console.WriteLine("  (Could not open browser automatically — open the file manually.)");
        }
    }

    private static string GenerateHtml(
        string framesJson,
        CartPoleConfig config,
        int stepsBalanced,
        int sampleInterval)
    {
        return $$"""
        <!DOCTYPE html>
        <html lang="en">
        <head>
        <meta charset="utf-8">
        <title>NeatSharp — Cart-Pole Visualization</title>
        <style>
          * { margin: 0; padding: 0; box-sizing: border-box; }
          body { background: #1a1a2e; color: #e0e0e0; font-family: 'Segoe UI', system-ui, sans-serif; display: flex; flex-direction: column; align-items: center; padding: 20px; }
          h1 { font-size: 1.4rem; margin-bottom: 4px; color: #a0c4ff; }
          .subtitle { font-size: 0.85rem; color: #888; margin-bottom: 16px; }
          canvas { border: 1px solid #333; border-radius: 6px; background: #0f0f23; }
          .controls { display: flex; align-items: center; gap: 16px; margin-top: 14px; flex-wrap: wrap; justify-content: center; }
          button { background: #2a2a4a; color: #e0e0e0; border: 1px solid #444; border-radius: 4px; padding: 6px 16px; cursor: pointer; font-size: 0.9rem; }
          button:hover { background: #3a3a5a; }
          .info { display: flex; gap: 24px; margin-top: 10px; font-size: 0.85rem; color: #aaa; flex-wrap: wrap; justify-content: center; }
          .info span { white-space: nowrap; }
          label { font-size: 0.85rem; }
          input[type=range] { width: 100px; }
        </style>
        </head>
        <body>
        <h1>Cart-Pole Champion Replay</h1>
        <div class="subtitle">NeatSharp NEAT — {{stepsBalanced}} / {{config.MaxSteps}} steps balanced</div>
        <canvas id="c" width="800" height="400"></canvas>
        <div class="controls">
          <button id="playBtn">Pause</button>
          <button id="restartBtn">Restart</button>
          <label>Speed: <input id="speed" type="range" min="1" max="20" value="3"> <span id="speedLbl">3x</span></label>
        </div>
        <div class="info">
          <span>Frame: <strong id="frameNum">0</strong> / <span id="totalFrames">0</span></span>
          <span>Sim step: <strong id="simStep">0</strong></span>
          <span>Time: <strong id="simTime">0.00</strong>s</span>
          <span>Cart x: <strong id="cartX">0.00</strong></span>
          <span>Pole θ: <strong id="poleTheta">0.00</strong>°</span>
          <span>Force: <strong id="forceDir">—</strong></span>
        </div>
        <script>
        (function() {
          const frames = {{framesJson}};
          const CFG = {
            trackHalf: {{config.TrackHalfLength.ToString(CultureInfo.InvariantCulture)}},
            poleHalf: {{config.PoleHalfLength.ToString(CultureInfo.InvariantCulture)}},
            failAngle: {{config.FailureAngle.ToString(CultureInfo.InvariantCulture)}},
            dt: {{config.TimeStep.ToString(CultureInfo.InvariantCulture)}},
            forceMag: {{config.ForceMagnitude.ToString(CultureInfo.InvariantCulture)}},
            sampleInterval: {{sampleInterval}}
          };
          const total = frames.length;

          const canvas = document.getElementById('c');
          const ctx = canvas.getContext('2d');
          const W = canvas.width, H = canvas.height;

          // Layout constants
          const trackY = H * 0.72;
          const scale = (W - 80) / (2 * CFG.trackHalf);  // px per meter
          const cartW = 50, cartH = 28;
          const poleLen = CFG.poleHalf * 2 * scale;       // full pole in px

          // UI elements
          const playBtn = document.getElementById('playBtn');
          const restartBtn = document.getElementById('restartBtn');
          const speedSlider = document.getElementById('speed');
          const speedLbl = document.getElementById('speedLbl');
          const frameNum = document.getElementById('frameNum');
          const totalFramesEl = document.getElementById('totalFrames');
          const simStepEl = document.getElementById('simStep');
          const simTimeEl = document.getElementById('simTime');
          const cartXEl = document.getElementById('cartX');
          const poleThetaEl = document.getElementById('poleTheta');
          const forceDirEl = document.getElementById('forceDir');

          totalFramesEl.textContent = total;

          let idx = 0;
          let playing = true;
          let accumulator = 0;

          playBtn.onclick = () => { playing = !playing; playBtn.textContent = playing ? 'Pause' : 'Play'; };
          restartBtn.onclick = () => { idx = 0; accumulator = 0; if (!playing) { playing = true; playBtn.textContent = 'Pause'; } };
          speedSlider.oninput = () => { speedLbl.textContent = speedSlider.value + 'x'; };

          function xToCanvas(x) { return W / 2 + x * scale; }

          function draw(frame) {
            const [x, theta, force] = frame;
            const cx = xToCanvas(x);

            ctx.clearRect(0, 0, W, H);

            // Failure angle zones (subtle shading)
            ctx.save();
            ctx.globalAlpha = 0.07;
            ctx.fillStyle = '#ff4444';
            // Left failure zone
            ctx.beginPath();
            ctx.moveTo(0, 0); ctx.lineTo(W, 0); ctx.lineTo(W, H); ctx.lineTo(0, H);
            ctx.closePath();
            // We'll just shade above the failure angles on each side with dashed lines instead
            ctx.restore();

            // Track
            const trackL = xToCanvas(-CFG.trackHalf);
            const trackR = xToCanvas(CFG.trackHalf);
            ctx.strokeStyle = '#555';
            ctx.lineWidth = 3;
            ctx.beginPath();
            ctx.moveTo(trackL, trackY);
            ctx.lineTo(trackR, trackY);
            ctx.stroke();

            // Track end markers
            ctx.strokeStyle = '#ff6666';
            ctx.lineWidth = 2;
            [trackL, trackR].forEach(tx => {
              ctx.beginPath();
              ctx.moveTo(tx, trackY - 20);
              ctx.lineTo(tx, trackY + 10);
              ctx.stroke();
            });

            // Failure angle indicators (dashed lines from cart pivot)
            ctx.save();
            ctx.setLineDash([4, 4]);
            ctx.strokeStyle = 'rgba(255,100,100,0.35)';
            ctx.lineWidth = 1.5;
            const pivotY = trackY - cartH;
            [-CFG.failAngle, CFG.failAngle].forEach(a => {
              const ex = cx + Math.sin(a) * poleLen;
              const ey = pivotY - Math.cos(a) * poleLen;
              ctx.beginPath();
              ctx.moveTo(cx, pivotY);
              ctx.lineTo(ex, ey);
              ctx.stroke();
            });
            ctx.restore();

            // Cart
            ctx.fillStyle = '#4488cc';
            ctx.fillRect(cx - cartW / 2, trackY - cartH, cartW, cartH);
            ctx.strokeStyle = '#6ab0f3';
            ctx.lineWidth = 1.5;
            ctx.strokeRect(cx - cartW / 2, trackY - cartH, cartW, cartH);

            // Wheels
            ctx.fillStyle = '#333';
            const wheelR = 5;
            [-14, 14].forEach(offset => {
              ctx.beginPath();
              ctx.arc(cx + offset, trackY + 2, wheelR, 0, Math.PI * 2);
              ctx.fill();
            });

            // Pole
            const poleEndX = cx + Math.sin(theta) * poleLen;
            const poleEndY = pivotY - Math.cos(theta) * poleLen;
            ctx.strokeStyle = '#cc4444';
            ctx.lineWidth = 5;
            ctx.lineCap = 'round';
            ctx.beginPath();
            ctx.moveTo(cx, pivotY);
            ctx.lineTo(poleEndX, poleEndY);
            ctx.stroke();

            // Pole pivot dot
            ctx.fillStyle = '#ffcc00';
            ctx.beginPath();
            ctx.arc(cx, pivotY, 4, 0, Math.PI * 2);
            ctx.fill();

            // Pole tip dot
            ctx.fillStyle = '#ff6666';
            ctx.beginPath();
            ctx.arc(poleEndX, poleEndY, 3.5, 0, Math.PI * 2);
            ctx.fill();

            // Force arrow
            if (force !== 0) {
              const arrowY = trackY - cartH / 2;
              const dir = Math.sign(force);
              const arrowLen = 30;
              const startX = cx + dir * (cartW / 2 + 4);
              const endX = startX + dir * arrowLen;
              ctx.strokeStyle = dir > 0 ? '#66ff88' : '#ff8866';
              ctx.lineWidth = 2.5;
              ctx.beginPath();
              ctx.moveTo(startX, arrowY);
              ctx.lineTo(endX, arrowY);
              ctx.stroke();
              // Arrowhead
              ctx.fillStyle = ctx.strokeStyle;
              ctx.beginPath();
              ctx.moveTo(endX, arrowY);
              ctx.lineTo(endX - dir * 8, arrowY - 5);
              ctx.lineTo(endX - dir * 8, arrowY + 5);
              ctx.closePath();
              ctx.fill();
            }

            // Update info
            const simStep = idx * CFG.sampleInterval;
            frameNum.textContent = idx;
            simStepEl.textContent = simStep;
            simTimeEl.textContent = (simStep * CFG.dt).toFixed(2);
            cartXEl.textContent = x.toFixed(3);
            poleThetaEl.textContent = (theta * 180 / Math.PI).toFixed(2);
            forceDirEl.textContent = force > 0 ? '→ Right' : '← Left';
            forceDirEl.style.color = force > 0 ? '#66ff88' : '#ff8866';
          }

          let lastTime = 0;
          function loop(ts) {
            if (lastTime === 0) lastTime = ts;
            const dt = (ts - lastTime) / 1000;
            lastTime = ts;

            if (playing && idx < total - 1) {
              const speed = parseInt(speedSlider.value);
              accumulator += dt * speed * 60;  // target frames per second scaled by speed
              while (accumulator >= 1 && idx < total - 1) {
                idx++;
                accumulator -= 1;
              }
            }

            if (idx >= total - 1 && playing) {
              playing = false;
              playBtn.textContent = 'Play';
            }

            draw(frames[idx]);
            requestAnimationFrame(loop);
          }

          draw(frames[0]);
          requestAnimationFrame(loop);
        })();
        </script>
        </body>
        </html>
        """;
    }
}
