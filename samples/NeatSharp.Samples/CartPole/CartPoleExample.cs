using System.Diagnostics;
using System.Globalization;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NeatSharp.Configuration;
using NeatSharp.Evaluation;
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
/// 2 outputs that determine force magnitude and direction. Output 0 controls magnitude
/// (scaled to [0, ForceMagnitude]), and output 1 controls direction (>0.5 = right, else left).
///
/// Fitness is computed as the fraction of maximum time steps the pole remains balanced.
/// A fitness of 1.0 means the network balanced the pole for the entire episode.
/// </summary>
public static class CartPoleExample
{
    /// <summary>
    /// Runs the Cart-Pole evolution example.
    /// </summary>
    /// <param name="args">
    /// Optional arguments: <c>--parallel [N]</c> to enable parallel fitness evaluation.
    /// <c>N</c> is the optional max degree of parallelism (default: all cores).
    /// </param>
    public static async Task RunAsync(string[]? args = null)
    {
        // Parse --parallel [N] flag
        args ??= [];
        int? maxDegreeOfParallelism = null;
        bool useParallel = false;
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--parallel")
            {
                useParallel = true;
                if (i + 1 < args.Length && int.TryParse(args[i + 1], out int cores) && cores >= 1)
                {
                    maxDegreeOfParallelism = cores;
                    i++;
                }
            }
        }

        Console.WriteLine("--- Cart-Pole Balancing ---");
        Console.WriteLine();
        Console.WriteLine("Goal: Evolve a neural network to balance an inverted pendulum on a cart.");
        Console.WriteLine("Inputs: cart position (x), cart velocity (x_dot), pole angle (theta), pole angular velocity (theta_dot)");
        Console.WriteLine("Outputs: force magnitude (0 to ForceMagnitude) + force direction (>0.5 = right, else left)");
        if (useParallel)
        {
            string coreDesc = maxDegreeOfParallelism.HasValue
                ? $"{maxDegreeOfParallelism} cores"
                : $"all cores ({Environment.ProcessorCount})";
            Console.WriteLine($"Parallel evaluation: {coreDesc}");
        }

        Console.WriteLine();

        // Cart-Pole physics configuration with canonical Barto/Stanley parameters
        var config = new CartPoleConfig();

        // Configure NEAT evolution:
        // - 4 inputs (cart state) and 2 outputs (force magnitude + direction)
        // - Population of 150 genomes
        // - Fixed seed for reproducible results
        // - Stop after 100 generations or when fitness reaches 0.95 (9,500+ steps balanced)
        var services = new ServiceCollection();
        services.AddNeatSharp(options =>
        {
            options.Speciation.CompatibilityThreshold = 3.0;
            options.InputCount = 4;    // x, x_dot, theta, theta_dot
            options.OutputCount = 2;   // force magnitude + direction
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

        // Define the Cart-Pole fitness function (thread-safe: no shared mutable state)
        Func<IGenome, double> fitnessFunction = genome =>
        {
            double totalFitness = 0;

            Span<double> output = stackalloc double[2];

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

                    // Get network outputs: magnitude + direction
                    genome.Activate(inputs, output);

                    // Output 0: force magnitude scaled to [0, ForceMagnitude]
                    // Output 1: direction (>0.5 = right, else left)
                    double magnitude = output[0] * config.ForceMagnitude;
                    double force = output[1] > 0.5 ? magnitude : -magnitude;
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
        };

        // Run evolution — use parallel evaluation when --parallel is specified
        var sw = Stopwatch.StartNew();
        EvolutionResult result;
        if (useParallel)
        {
            var evalOptions = new EvaluationOptions { MaxDegreeOfParallelism = maxDegreeOfParallelism };
            result = await evolver.RunAsync(fitnessFunction, evalOptions);
        }
        else
        {
            result = await evolver.RunAsync(fitnessFunction);
        }

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
        Span<double> evalOutput = stackalloc double[2];
        var stateHistory = new List<(double X, double Theta, double Force, double XDot, double ThetaDot)>();

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

            double magnitude = evalOutput[0] * config.ForceMagnitude;
            double force = evalOutput[1] > 0.5 ? magnitude : -magnitude;
            stateHistory.Add((evalSim.State.X, evalSim.State.Theta, force, evalSim.State.XDot, evalSim.State.ThetaDot));
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
        OpenVisualization(stateHistory, config, championSteps, result.Champion.Genotype);

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
        List<(double X, double Theta, double Force, double XDot, double ThetaDot)> history,
        CartPoleConfig config,
        int stepsBalanced,
        Genome? genotype)
    {
        // Sample frames: keep at most ~2000 frames for smooth browser playback
        const int maxFrames = 2000;
        int sampleInterval = Math.Max(1, history.Count / maxFrames);

        var framesJson = new StringBuilder("[");
        for (int i = 0; i < history.Count; i += sampleInterval)
        {
            if (i > 0)
            {
                framesJson.Append(',');
            }

            var (x, theta, force, xDot, thetaDot) = history[i];
            framesJson.Append(CultureInfo.InvariantCulture,
                $"[{x:G6},{theta:G6},{force:G6},{xDot:G6},{thetaDot:G6}]");
        }

        framesJson.Append(']');

        string networkJson = SerializeGenome(genotype);
        string html = GenerateHtml(framesJson.ToString(), networkJson, config, stepsBalanced, sampleInterval);

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

    private static string SerializeGenome(Genome? genotype)
    {
        if (genotype is null)
        {
            return "null";
        }

        var sb = new StringBuilder("{\"nodes\":[");
        for (int i = 0; i < genotype.Nodes.Count; i++)
        {
            if (i > 0)
            {
                sb.Append(',');
            }

            var n = genotype.Nodes[i];
            sb.Append(CultureInfo.InvariantCulture,
                $"{{\"id\":{n.Id},\"type\":\"{n.Type}\",\"act\":\"{n.ActivationFunction}\"}}");
        }

        sb.Append("],\"connections\":[");
        for (int i = 0; i < genotype.Connections.Count; i++)
        {
            if (i > 0)
            {
                sb.Append(',');
            }

            var c = genotype.Connections[i];
            sb.Append(CultureInfo.InvariantCulture,
                $"{{\"src\":{c.SourceNodeId},\"tgt\":{c.TargetNodeId},\"w\":{c.Weight:G6},\"on\":{(c.IsEnabled ? "true" : "false")}}}");
        }

        sb.Append("]}");
        return sb.ToString();
    }

    private static string GenerateHtml(
        string framesJson,
        string networkJson,
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
          .canvases { display: flex; gap: 12px; align-items: start; flex-wrap: wrap; justify-content: center; }
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
        <div class="canvases">
          <canvas id="c" width="800" height="400"></canvas>
          <canvas id="nn" width="460" height="400"></canvas>
        </div>
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
          const NET = {{networkJson}};
          const CFG = {
            trackHalf: {{config.TrackHalfLength.ToString(CultureInfo.InvariantCulture)}},
            poleHalf: {{config.PoleHalfLength.ToString(CultureInfo.InvariantCulture)}},
            failAngle: {{config.FailureAngle.ToString(CultureInfo.InvariantCulture)}},
            dt: {{config.TimeStep.ToString(CultureInfo.InvariantCulture)}},
            forceMag: {{config.ForceMagnitude.ToString(CultureInfo.InvariantCulture)}},
            sampleInterval: {{sampleInterval}}
          };
          const total = frames.length;

          // ── Cart-Pole canvas ──
          const canvas = document.getElementById('c');
          const ctx = canvas.getContext('2d');
          const W = canvas.width, H = canvas.height;
          const trackY = H * 0.72;
          const scale = (W - 80) / (2 * CFG.trackHalf);
          const cartW = 50, cartH = 28;
          const poleLen = CFG.poleHalf * 2 * scale;

          // ── Network canvas ──
          const nnCanvas = document.getElementById('nn');
          const nnCtx = nnCanvas.getContext('2d');
          const NW = nnCanvas.width, NH = nnCanvas.height;

          // ── UI elements ──
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

          let idx = 0, playing = true, accumulator = 0;
          playBtn.onclick = () => { playing = !playing; playBtn.textContent = playing ? 'Pause' : 'Play'; };
          restartBtn.onclick = () => { idx = 0; accumulator = 0; if (!playing) { playing = true; playBtn.textContent = 'Pause'; } };
          speedSlider.oninput = () => { speedLbl.textContent = speedSlider.value + 'x'; };

          // ── Activation functions ──
          const actFns = {
            sigmoid: v => 1 / (1 + Math.exp(-v)),
            tanh: v => Math.tanh(v),
            relu: v => Math.max(0, v),
            step: v => v > 0 ? 1 : 0,
            identity: v => v
          };

          // ── Build network layout ──
          const inputLabels = ['x', 'x\u0307', '\u03b8', '\u03b8\u0307'];
          const outputLabels = ['Mag', 'Dir'];

          let nodeMap = {};      // id -> {type, act, col, row, px, py, val}
          let topoOrder = [];    // ids in evaluation order
          let connections = [];   // {src, tgt, w, on}

          if (NET) {
            const inputs = [], outputs = [], hidden = [], biases = [];
            NET.nodes.forEach(n => {
              nodeMap[n.id] = { type: n.type, act: n.act, val: 0 };
              if (n.type === 'Input') inputs.push(n.id);
              else if (n.type === 'Output') outputs.push(n.id);
              else if (n.type === 'Bias') biases.push(n.id);
              else hidden.push(n.id);
            });
            connections = NET.connections;

            // Assign layers for layout: inputs+bias=0, hidden=1, outputs=2
            // For multiple hidden layers we do a simple BFS depth from inputs
            const depth = {};
            inputs.forEach(id => depth[id] = 0);
            biases.forEach(id => depth[id] = 0);

            // Topological sort via Kahn's algorithm on enabled connections
            const inDeg = {};
            const adj = {};
            Object.keys(nodeMap).forEach(id => { inDeg[id] = 0; adj[id] = []; });
            connections.forEach(c => {
              if (!c.on) return;
              adj[c.src].push(c.tgt);
              inDeg[c.tgt]++;
            });
            const queue = Object.keys(nodeMap).filter(id => inDeg[id] === 0).map(Number);
            while (queue.length > 0) {
              const nid = queue.shift();
              topoOrder.push(nid);
              (adj[nid] || []).forEach(tgt => {
                const d = (depth[nid] || 0) + 1;
                if (!depth[tgt] || d > depth[tgt]) depth[tgt] = d;
                inDeg[tgt]--;
                if (inDeg[tgt] === 0) queue.push(tgt);
              });
            }

            // Force output nodes to max depth + 1
            const maxHiddenDepth = Math.max(1, ...Object.values(depth));
            outputs.forEach(id => depth[id] = maxHiddenDepth + 1);

            // Group by depth for column layout
            const columns = {};
            Object.keys(nodeMap).forEach(id => {
              const d = depth[id] || 0;
              if (!columns[d]) columns[d] = [];
              columns[d].push(Number(id));
            });
            const colKeys = Object.keys(columns).map(Number).sort((a, b) => a - b);
            const numCols = colKeys.length;

            // Compute pixel positions
            const padX = 70, padY = 40;
            const usableW = NW - 2 * padX;
            const usableH = NH - 2 * padY;
            colKeys.forEach((ck, ci) => {
              const col = columns[ck];
              const xPos = numCols === 1 ? NW / 2 : padX + ci * (usableW / (numCols - 1));
              col.forEach((id, ri) => {
                const yPos = col.length === 1 ? NH / 2 : padY + ri * (usableH / (col.length - 1));
                nodeMap[id].px = xPos;
                nodeMap[id].py = yPos;
              });
            });
          }

          // ── Forward pass ──
          function forwardPass(normInputs) {
            if (!NET) return;
            // Reset values
            Object.keys(nodeMap).forEach(id => { nodeMap[id].val = 0; });
            // Set input values
            const inputs = Object.keys(nodeMap).filter(id => nodeMap[id].type === 'Input');
            inputs.sort((a, b) => a - b);
            inputs.forEach((id, i) => { nodeMap[id].val = normInputs[i] ?? 0; });
            // Set bias
            Object.keys(nodeMap).filter(id => nodeMap[id].type === 'Bias')
              .forEach(id => { nodeMap[id].val = 1.0; });
            // Evaluate in topological order
            topoOrder.forEach(nid => {
              const node = nodeMap[nid];
              if (node.type === 'Input' || node.type === 'Bias') return;
              let sum = 0;
              connections.forEach(c => {
                if (c.on && c.tgt === nid) sum += c.w * nodeMap[c.src].val;
              });
              const fn = actFns[node.act] || actFns.sigmoid;
              node.val = fn(sum);
            });
          }

          // ── Color helpers ──
          function valToColor(v) {
            // Map activation value to color: 0=cool blue, 1=warm yellow
            const t = Math.max(0, Math.min(1, v));
            const r = Math.round(40 + 215 * t);
            const g = Math.round(60 + 180 * t);
            const b = Math.round(180 - 120 * t);
            return `rgb(${r},${g},${b})`;
          }
          function weightColor(w) {
            return w >= 0 ? 'rgba(100,255,140,' : 'rgba(255,100,100,';
          }

          // ── Draw network ──
          function drawNetwork() {
            if (!NET) return;
            nnCtx.clearRect(0, 0, NW, NH);

            // Title
            nnCtx.fillStyle = '#a0c4ff';
            nnCtx.font = 'bold 13px "Segoe UI", system-ui, sans-serif';
            nnCtx.textAlign = 'center';
            nnCtx.fillText('Champion Neural Network', NW / 2, 18);

            // Draw connections
            connections.forEach(c => {
              const src = nodeMap[c.src], tgt = nodeMap[c.tgt];
              if (!src || !tgt) return;
              const absW = Math.min(Math.abs(c.w), 5);
              const alpha = c.on ? (0.25 + 0.55 * (absW / 5)) : 0.08;
              nnCtx.strokeStyle = weightColor(c.w) + alpha + ')';
              nnCtx.lineWidth = c.on ? (0.8 + 2.2 * (absW / 5)) : 0.5;
              if (!c.on) nnCtx.setLineDash([3, 3]);
              nnCtx.beginPath();
              nnCtx.moveTo(src.px, src.py);
              nnCtx.lineTo(tgt.px, tgt.py);
              nnCtx.stroke();
              nnCtx.setLineDash([]);

              // Weight label at midpoint
              if (c.on) {
                const mx = (src.px + tgt.px) / 2;
                const my = (src.py + tgt.py) / 2;
                nnCtx.fillStyle = 'rgba(200,200,200,0.6)';
                nnCtx.font = '9px "Segoe UI", system-ui, sans-serif';
                nnCtx.textAlign = 'center';
                nnCtx.fillText(c.w.toFixed(2), mx, my - 4);
              }
            });

            // Draw nodes
            const nodeR = 16;
            Object.keys(nodeMap).forEach(id => {
              const n = nodeMap[id];
              // Node circle
              nnCtx.beginPath();
              nnCtx.arc(n.px, n.py, nodeR, 0, Math.PI * 2);
              nnCtx.fillStyle = valToColor(n.val);
              nnCtx.fill();
              nnCtx.strokeStyle = n.type === 'Bias' ? '#bbbb44' :
                                  n.type === 'Input' ? '#4488cc' :
                                  n.type === 'Output' ? '#cc6644' : '#888';
              nnCtx.lineWidth = 2;
              nnCtx.stroke();

              // Value inside node
              nnCtx.fillStyle = n.val > 0.5 ? '#111' : '#eee';
              nnCtx.font = 'bold 10px "Segoe UI", system-ui, sans-serif';
              nnCtx.textAlign = 'center';
              nnCtx.textBaseline = 'middle';
              nnCtx.fillText(n.val.toFixed(2), n.px, n.py);

              // Label below node
              nnCtx.textBaseline = 'top';
              nnCtx.fillStyle = '#999';
              nnCtx.font = '10px "Segoe UI", system-ui, sans-serif';
              let label = '';
              if (n.type === 'Input') {
                const inputs = Object.keys(nodeMap).filter(k => nodeMap[k].type === 'Input').sort((a,b) => a - b);
                const i = inputs.indexOf(id.toString());
                label = inputLabels[i] || 'in' + i;
              } else if (n.type === 'Output') {
                const outputs = Object.keys(nodeMap).filter(k => nodeMap[k].type === 'Output').sort((a,b) => a - b);
                const i = outputs.indexOf(id.toString());
                label = outputLabels[i] || 'out' + i;
              } else if (n.type === 'Bias') {
                label = 'bias';
              } else {
                label = 'H' + id;
              }
              nnCtx.fillText(label, n.px, n.py + nodeR + 3);
            });
          }

          // ── Draw cart-pole ──
          function xToCanvas(x) { return W / 2 + x * scale; }

          function drawCartPole(frame) {
            const [x, theta, force] = frame;
            const cx = xToCanvas(x);
            ctx.clearRect(0, 0, W, H);

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

            // Failure angle indicators
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
            [-14, 14].forEach(offset => {
              ctx.beginPath();
              ctx.arc(cx + offset, trackY + 2, 5, 0, Math.PI * 2);
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
              const arrowLen = 6 + 24 * (Math.abs(force) / CFG.forceMag);
              const startX = cx + dir * (cartW / 2 + 4);
              const endX = startX + dir * arrowLen;
              ctx.strokeStyle = dir > 0 ? '#66ff88' : '#ff8866';
              ctx.lineWidth = 2.5;
              ctx.beginPath();
              ctx.moveTo(startX, arrowY);
              ctx.lineTo(endX, arrowY);
              ctx.stroke();
              ctx.fillStyle = ctx.strokeStyle;
              ctx.beginPath();
              ctx.moveTo(endX, arrowY);
              ctx.lineTo(endX - dir * 8, arrowY - 5);
              ctx.lineTo(endX - dir * 8, arrowY + 5);
              ctx.closePath();
              ctx.fill();
            }

            // Update info bar
            const simStep = idx * CFG.sampleInterval;
            frameNum.textContent = idx;
            simStepEl.textContent = simStep;
            simTimeEl.textContent = (simStep * CFG.dt).toFixed(2);
            cartXEl.textContent = x.toFixed(3);
            poleThetaEl.textContent = (theta * 180 / Math.PI).toFixed(2);
            const absF = Math.abs(force).toFixed(1);
            forceDirEl.textContent = force > 0 ? absF + 'N \u2192' : '\u2190 ' + absF + 'N';
            forceDirEl.style.color = force > 0 ? '#66ff88' : '#ff8866';
          }

          // ── Main draw ──
          function draw(frame) {
            const [x, theta, force, xDot, thetaDot] = frame;
            drawCartPole(frame);
            // Compute normalized inputs for forward pass
            const normInputs = [
              x / CFG.trackHalf,
              xDot / 2.0,
              theta / CFG.failAngle,
              thetaDot / 2.0
            ];
            forwardPass(normInputs);
            drawNetwork();
          }

          // ── Animation loop ──
          let lastTime = 0;
          function loop(ts) {
            if (lastTime === 0) lastTime = ts;
            const dt = (ts - lastTime) / 1000;
            lastTime = ts;

            if (playing && idx < total - 1) {
              const speed = parseInt(speedSlider.value);
              accumulator += dt * speed * 60;
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
