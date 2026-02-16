using ILGPU;
using ILGPU.Runtime;
using ILGPU.Runtime.CPU;
using ILGPU.Runtime.Cuda;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NeatSharp.Evaluation;
using NeatSharp.Genetics;
using NeatSharp.Gpu.Configuration;
using NeatSharp.Gpu.Detection;
using NeatSharp.Gpu.Kernels;

namespace NeatSharp.Gpu.Evaluation;

/// <summary>
/// GPU-accelerated batch evaluator implementing <see cref="IBatchEvaluator"/>.
/// Evaluates entire NEAT populations on the GPU via forward propagation kernels,
/// with transparent CPU fallback on GPU failure.
/// </summary>
/// <remarks>
/// <para>
/// GPU initialization is lazy — the accelerator and context are created on the
/// first call to <see cref="EvaluateAsync"/>. GPU memory buffers use a
/// resize-on-grow pattern to avoid reallocation when population size is stable.
/// </para>
/// <para>
/// If genomes are not <see cref="GpuFeedForwardNetwork"/> instances, or if GPU
/// initialization fails, the evaluator falls back to CPU-based evaluation using
/// <see cref="IGenome.Activate"/>.
/// </para>
/// <para>
/// <strong>Precision (fp32 vs fp64):</strong> The GPU evaluation path uses single-precision
/// (fp32) floating-point arithmetic for all forward propagation computations, while the CPU
/// path uses double-precision (fp64). This introduces small numerical differences between
/// the two paths. The documented tolerance is <strong>1e-4</strong> per output value and
/// per genome fitness score. In practice, differences are typically smaller (1e-5 to 1e-6)
/// for simple topologies, but can approach the tolerance bound for deep networks with
/// steepened sigmoid activation (slope factor 4.9), which amplifies rounding differences
/// in the exponent computation.
/// </para>
/// <para>
/// <strong>Steepened sigmoid sensitivity:</strong> The steepened sigmoid function
/// <c>1 / (1 + exp(-4.9 * x))</c> is the primary source of fp32/fp64 divergence.
/// The 4.9 multiplication amplifies input values before the exponential, causing the
/// <c>exp()</c> result to differ between <see cref="System.MathF.Exp(float)"/> (fp32)
/// and <see cref="System.Math.Exp(double)"/> (fp64), particularly for input magnitudes
/// above ~2.0 where the sigmoid curve is steep.
/// </para>
/// </remarks>
public sealed class GpuBatchEvaluator : IBatchEvaluator, IDisposable
{
    private readonly IGpuDeviceDetector _deviceDetector;
    private readonly IGpuFitnessFunction _fitnessFunction;
    private readonly GpuOptions _options;
    private readonly ILogger<GpuBatchEvaluator> _logger;

    private Context? _context;
    private Accelerator? _accelerator;
    private bool _gpuInitialized;
    private bool _disposed;

    // Pooled GPU buffers — resize-on-grow pattern
    private MemoryBuffer1D<int, Stride1D.Dense>? _genomeNodeOffsetsBuffer;
    private MemoryBuffer1D<int, Stride1D.Dense>? _genomeNodeCountsBuffer;
    private MemoryBuffer1D<int, Stride1D.Dense>? _genomeEvalOrderOffsetsBuffer;
    private MemoryBuffer1D<int, Stride1D.Dense>? _genomeEvalOrderCountsBuffer;
    private MemoryBuffer1D<int, Stride1D.Dense>? _genomeInputOffsetsBuffer;
    private MemoryBuffer1D<int, Stride1D.Dense>? _genomeInputCountsBuffer;
    private MemoryBuffer1D<int, Stride1D.Dense>? _genomeOutputOffsetsBuffer;
    private MemoryBuffer1D<int, Stride1D.Dense>? _genomeOutputCountsBuffer;
    private MemoryBuffer1D<int, Stride1D.Dense>? _genomeBiasOffsetsBuffer;
    private MemoryBuffer1D<int, Stride1D.Dense>? _genomeBiasCountsBuffer;
    private MemoryBuffer1D<int, Stride1D.Dense>? _nodeActivationTypesBuffer;
    private MemoryBuffer1D<int, Stride1D.Dense>? _evalNodeIndicesBuffer;
    private MemoryBuffer1D<int, Stride1D.Dense>? _evalNodeConnOffsetsBuffer;
    private MemoryBuffer1D<int, Stride1D.Dense>? _evalNodeConnCountsBuffer;
    private MemoryBuffer1D<int, Stride1D.Dense>? _incomingSourceIndicesBuffer;
    private MemoryBuffer1D<float, Stride1D.Dense>? _incomingWeightsBuffer;
    private MemoryBuffer1D<int, Stride1D.Dense>? _inputNodeIndicesBuffer;
    private MemoryBuffer1D<int, Stride1D.Dense>? _outputNodeIndicesBuffer;
    private MemoryBuffer1D<int, Stride1D.Dense>? _biasNodeIndicesBuffer;
    private MemoryBuffer1D<float, Stride1D.Dense>? _testInputsBuffer;
    private MemoryBuffer1D<float, Stride1D.Dense>? _testOutputsBuffer;
    private MemoryBuffer1D<float, Stride1D.Dense>? _activationBuffer;

    /// <summary>
    /// Initializes a new instance of <see cref="GpuBatchEvaluator"/>.
    /// </summary>
    /// <param name="deviceDetector">Detects available GPU devices.</param>
    /// <param name="fitnessFunction">Defines test cases and fitness computation.</param>
    /// <param name="options">GPU configuration options.</param>
    /// <param name="logger">Logger for GPU diagnostics.</param>
    public GpuBatchEvaluator(
        IGpuDeviceDetector deviceDetector,
        IGpuFitnessFunction fitnessFunction,
        IOptions<GpuOptions> options,
        ILogger<GpuBatchEvaluator> logger)
    {
        ArgumentNullException.ThrowIfNull(deviceDetector);
        ArgumentNullException.ThrowIfNull(fitnessFunction);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _deviceDetector = deviceDetector;
        _fitnessFunction = fitnessFunction;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task EvaluateAsync(
        IReadOnlyList<IGenome> genomes,
        Action<int, double> setFitness,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(genomes);
        ArgumentNullException.ThrowIfNull(setFitness);
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (genomes.Count == 0)
        {
            return Task.CompletedTask;
        }

        cancellationToken.ThrowIfCancellationRequested();

        // Check if genomes are GPU-compatible
        if (genomes[0] is not GpuFeedForwardNetwork)
        {
            _logger.LogDebug("Genomes are not GpuFeedForwardNetwork — using CPU evaluation.");
            EvaluateCpu(genomes, setFitness, _fitnessFunction,
                _fitnessFunction.InputCases.Length / _fitnessFunction.CaseCount,
                GetOutputCount(genomes));
            return Task.CompletedTask;
        }

        // Attempt GPU evaluation
        if (_options.EnableGpu)
        {
            try
            {
                EvaluateGpu(genomes, setFitness, cancellationToken);
                return Task.CompletedTask;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex,
                    "GPU evaluation failed — falling back to CPU for this generation.");
            }
        }

        // CPU fallback
        int inputCount = _fitnessFunction.InputCases.Length / _fitnessFunction.CaseCount;
        int outputCount = GetOutputCount(genomes);
        EvaluateCpu(genomes, setFitness, _fitnessFunction, inputCount, outputCount);
        return Task.CompletedTask;
    }

    private void EvaluateGpu(
        IReadOnlyList<IGenome> genomes,
        Action<int, double> setFitness,
        CancellationToken cancellationToken)
    {
        // Initialize GPU if not yet done
        if (!_gpuInitialized)
        {
            InitializeGpu();
        }

        // Cast genomes to GpuFeedForwardNetwork
        var gpuGenomes = new List<GpuFeedForwardNetwork>(genomes.Count);
        for (int i = 0; i < genomes.Count; i++)
        {
            gpuGenomes.Add((GpuFeedForwardNetwork)genomes[i]);
        }

        // Build population data
        var populationData = new GpuPopulationData(gpuGenomes);

        int caseCount = _fitnessFunction.CaseCount;
        int inputCount = _fitnessFunction.InputCases.Length / caseCount;
        int outputCount = gpuGenomes[0].OutputIndices.Length;
        int totalOutputs = populationData.PopulationSize * caseCount * outputCount;

        cancellationToken.ThrowIfCancellationRequested();

        // Ensure GPU buffers are allocated (resize-on-grow)
        EnsureBuffers(populationData, caseCount, inputCount, outputCount);

        // Upload topology arrays to GPU
        _genomeNodeOffsetsBuffer!.CopyFromCPU(populationData.GenomeNodeOffsets);
        _genomeNodeCountsBuffer!.CopyFromCPU(populationData.GenomeNodeCounts);
        _genomeEvalOrderOffsetsBuffer!.CopyFromCPU(populationData.GenomeEvalOrderOffsets);
        _genomeEvalOrderCountsBuffer!.CopyFromCPU(populationData.GenomeEvalOrderCounts);
        _genomeInputOffsetsBuffer!.CopyFromCPU(populationData.GenomeInputOffsets);
        _genomeInputCountsBuffer!.CopyFromCPU(populationData.GenomeInputCounts);
        _genomeOutputOffsetsBuffer!.CopyFromCPU(populationData.GenomeOutputOffsets);
        _genomeOutputCountsBuffer!.CopyFromCPU(populationData.GenomeOutputCounts);
        _genomeBiasOffsetsBuffer!.CopyFromCPU(populationData.GenomeBiasOffsets);
        _genomeBiasCountsBuffer!.CopyFromCPU(populationData.GenomeBiasCounts);

        if (populationData.TotalNodes > 0)
        {
            _nodeActivationTypesBuffer!.CopyFromCPU(populationData.NodeActivationTypes);
        }

        if (populationData.TotalEvalNodes > 0)
        {
            _evalNodeIndicesBuffer!.CopyFromCPU(populationData.EvalNodeIndices);
            _evalNodeConnOffsetsBuffer!.CopyFromCPU(populationData.EvalNodeConnOffsets);
            _evalNodeConnCountsBuffer!.CopyFromCPU(populationData.EvalNodeConnCounts);
        }

        if (populationData.TotalIncoming > 0)
        {
            _incomingSourceIndicesBuffer!.CopyFromCPU(populationData.IncomingSourceIndices);
            _incomingWeightsBuffer!.CopyFromCPU(populationData.IncomingWeights);
        }

        if (populationData.TotalInputs > 0)
        {
            _inputNodeIndicesBuffer!.CopyFromCPU(populationData.InputNodeIndices);
        }

        if (populationData.TotalOutputs > 0)
        {
            _outputNodeIndicesBuffer!.CopyFromCPU(populationData.OutputNodeIndices);
        }

        if (populationData.TotalBiasNodes > 0)
        {
            _biasNodeIndicesBuffer!.CopyFromCPU(populationData.BiasNodeIndices);
        }

        // Upload test inputs
        _testInputsBuffer!.CopyFromCPU(
            _fitnessFunction.InputCases.ToArray());

        cancellationToken.ThrowIfCancellationRequested();

        // Build kernel parameter structs
        var genomeData = new GenomeIndexData
        {
            GenomeNodeOffsets = _genomeNodeOffsetsBuffer!.View,
            GenomeNodeCounts = _genomeNodeCountsBuffer!.View,
            GenomeEvalOrderOffsets = _genomeEvalOrderOffsetsBuffer!.View,
            GenomeEvalOrderCounts = _genomeEvalOrderCountsBuffer!.View,
            GenomeInputOffsets = _genomeInputOffsetsBuffer!.View,
            GenomeInputCounts = _genomeInputCountsBuffer!.View,
            GenomeOutputOffsets = _genomeOutputOffsetsBuffer!.View,
            GenomeOutputCounts = _genomeOutputCountsBuffer!.View,
            GenomeBiasOffsets = _genomeBiasOffsetsBuffer!.View,
            GenomeBiasCounts = _genomeBiasCountsBuffer!.View
        };

        var topology = new TopologyData
        {
            NodeActivationTypes = _nodeActivationTypesBuffer!.View,
            EvalNodeIndices = _evalNodeIndicesBuffer!.View,
            EvalNodeConnOffsets = _evalNodeConnOffsetsBuffer!.View,
            EvalNodeConnCounts = _evalNodeConnCountsBuffer!.View,
            IncomingSourceIndices = _incomingSourceIndicesBuffer!.View,
            IncomingWeights = _incomingWeightsBuffer!.View,
            InputNodeIndices = _inputNodeIndicesBuffer!.View,
            OutputNodeIndices = _outputNodeIndicesBuffer!.View,
            BiasNodeIndices = _biasNodeIndicesBuffer!.View
        };

        // Load and launch kernel
        var kernel = _accelerator!.LoadAutoGroupedStreamKernel<
            Index1D,
            GenomeIndexData,
            TopologyData,
            ArrayView1D<float, Stride1D.Dense>,
            int,
            int,
            int,
            ArrayView1D<float, Stride1D.Dense>,
            ArrayView1D<float, Stride1D.Dense>>(
            ForwardPropagationKernel.Execute);

        kernel(
            populationData.PopulationSize,
            genomeData,
            topology,
            _testInputsBuffer!.View,
            caseCount,
            inputCount,
            outputCount,
            _testOutputsBuffer!.View,
            _activationBuffer!.View);

        // Synchronize and download outputs
        _accelerator!.Synchronize();

        cancellationToken.ThrowIfCancellationRequested();

        var hostOutputs = _testOutputsBuffer.GetAsArray1D();

        // Compute fitness for each genome
        int outputSliceLen = caseCount * outputCount;
        for (int i = 0; i < populationData.PopulationSize; i++)
        {
            var genomeOutputs = new ReadOnlySpan<float>(
                hostOutputs, i * outputSliceLen, outputSliceLen);
            double fitness = _fitnessFunction.ComputeFitness(genomeOutputs);
            setFitness(i, fitness);
        }
    }

    private void InitializeGpu()
    {
        _gpuInitialized = true;

        if (!_options.EnableGpu)
        {
            _logger.LogInformation(
                "GPU evaluation disabled via configuration -- using CPU evaluation.");
            _context = Context.CreateDefault();
            _accelerator = _context.CreateCPUAccelerator(0);
            return;
        }

        var deviceInfo = _deviceDetector.Detect();

        if (deviceInfo is null)
        {
            // No CUDA GPU detected at all
            _logger.LogInformation(
                "No CUDA GPU detected -- using CPU evaluation.");
            _context = Context.CreateDefault();
            _accelerator = _context.CreateCPUAccelerator(0);
            return;
        }

        if (!deviceInfo.IsCompatible)
        {
            // GPU found but incompatible
            _logger.LogWarning(
                "Incompatible GPU detected: {DiagnosticMessage} -- using CPU evaluation.",
                deviceInfo.DiagnosticMessage);
            _context = Context.CreateDefault();
            _accelerator = _context.CreateCPUAccelerator(0);
            return;
        }

        // Compatible GPU found -- attempt to create CUDA accelerator
        _logger.LogInformation(
            "Compatible CUDA GPU detected: {DeviceName} (CC {ComputeCapability}, {MemoryMB:N0} MB).",
            deviceInfo.DeviceName,
            deviceInfo.ComputeCapability,
            deviceInfo.MemoryBytes / (1024.0 * 1024.0));

        try
        {
            _context = Context.CreateDefault();
            var cudaDevices = _context.GetCudaDevices();
            if (cudaDevices.Count > 0)
            {
                _accelerator = _context.CreateCudaAccelerator(0);
                _logger.LogInformation(
                    "GPU evaluator initialized with CUDA accelerator: {AcceleratorName}.",
                    _accelerator.Name);
            }
            else
            {
                // Unlikely after detection succeeded, but handle gracefully
                _logger.LogWarning(
                    "CUDA devices no longer available after detection -- falling back to CPU evaluation.");
                _accelerator = _context.CreateCPUAccelerator(0);
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            _logger.LogWarning(ex,
                "Failed to create CUDA accelerator -- falling back to CPU evaluation.");

            // Clean up the failed context and create a fresh one for CPU
            _context?.Dispose();
            _context = Context.CreateDefault();
            _accelerator = _context.CreateCPUAccelerator(0);
        }
    }

    private void EnsureBuffers(
        GpuPopulationData populationData,
        int caseCount,
        int inputCount,
        int outputCount)
    {
        int popSize = populationData.PopulationSize;
        int totalNodes = populationData.TotalNodes;
        int totalEvalNodes = populationData.TotalEvalNodes;
        int totalIncoming = populationData.TotalIncoming;
        int totalInputs = populationData.TotalInputs;
        int totalOutputs = populationData.TotalOutputs;
        int totalBiasNodes = populationData.TotalBiasNodes;
        int totalTestInputs = caseCount * inputCount;
        int totalTestOutputs = popSize * caseCount * outputCount;

        // Per-genome index buffers
        EnsureIntBuffer(ref _genomeNodeOffsetsBuffer, popSize);
        EnsureIntBuffer(ref _genomeNodeCountsBuffer, popSize);
        EnsureIntBuffer(ref _genomeEvalOrderOffsetsBuffer, popSize);
        EnsureIntBuffer(ref _genomeEvalOrderCountsBuffer, popSize);
        EnsureIntBuffer(ref _genomeInputOffsetsBuffer, popSize);
        EnsureIntBuffer(ref _genomeInputCountsBuffer, popSize);
        EnsureIntBuffer(ref _genomeOutputOffsetsBuffer, popSize);
        EnsureIntBuffer(ref _genomeOutputCountsBuffer, popSize);
        EnsureIntBuffer(ref _genomeBiasOffsetsBuffer, popSize);
        EnsureIntBuffer(ref _genomeBiasCountsBuffer, popSize);

        // Topology buffers (ensure at least 1 element for ILGPU views)
        EnsureIntBuffer(ref _nodeActivationTypesBuffer, Math.Max(1, totalNodes));
        EnsureIntBuffer(ref _evalNodeIndicesBuffer, Math.Max(1, totalEvalNodes));
        EnsureIntBuffer(ref _evalNodeConnOffsetsBuffer, Math.Max(1, totalEvalNodes));
        EnsureIntBuffer(ref _evalNodeConnCountsBuffer, Math.Max(1, totalEvalNodes));
        EnsureIntBuffer(ref _incomingSourceIndicesBuffer, Math.Max(1, totalIncoming));
        EnsureFloatBuffer(ref _incomingWeightsBuffer, Math.Max(1, totalIncoming));
        EnsureIntBuffer(ref _inputNodeIndicesBuffer, Math.Max(1, totalInputs));
        EnsureIntBuffer(ref _outputNodeIndicesBuffer, Math.Max(1, totalOutputs));
        EnsureIntBuffer(ref _biasNodeIndicesBuffer, Math.Max(1, totalBiasNodes));

        // Test I/O buffers
        EnsureFloatBuffer(ref _testInputsBuffer, Math.Max(1, totalTestInputs));
        EnsureFloatBuffer(ref _testOutputsBuffer, Math.Max(1, totalTestOutputs));

        // Activation buffer (workspace)
        EnsureFloatBuffer(ref _activationBuffer, Math.Max(1, totalNodes));
    }

    private void EnsureIntBuffer(
        ref MemoryBuffer1D<int, Stride1D.Dense>? buffer,
        int requiredLength)
    {
        if (buffer == null || buffer.Length < requiredLength)
        {
            buffer?.Dispose();
            buffer = _accelerator!.Allocate1D<int>(requiredLength);
        }
    }

    private void EnsureFloatBuffer(
        ref MemoryBuffer1D<float, Stride1D.Dense>? buffer,
        int requiredLength)
    {
        if (buffer == null || buffer.Length < requiredLength)
        {
            buffer?.Dispose();
            buffer = _accelerator!.Allocate1D<float>(requiredLength);
        }
    }

    private void EvaluateCpu(
        IReadOnlyList<IGenome> genomes,
        Action<int, double> setFitness,
        IGpuFitnessFunction fitnessFunction,
        int inputCount,
        int outputCount)
    {
        var inputSpan = fitnessFunction.InputCases.Span;
        Span<double> cpuInputs = stackalloc double[inputCount];
        Span<double> cpuOutputs = stackalloc double[outputCount];

        for (int i = 0; i < genomes.Count; i++)
        {
            var genome = genomes[i];
            var allOutputs = new float[fitnessFunction.CaseCount * outputCount];

            for (int c = 0; c < fitnessFunction.CaseCount; c++)
            {
                for (int j = 0; j < inputCount; j++)
                {
                    cpuInputs[j] = inputSpan[c * inputCount + j];
                }

                genome.Activate(cpuInputs, cpuOutputs);

                for (int j = 0; j < outputCount; j++)
                {
                    allOutputs[c * outputCount + j] = (float)cpuOutputs[j];
                }
            }

            setFitness(i, fitnessFunction.ComputeFitness(allOutputs));
        }
    }

    private static int GetOutputCount(IReadOnlyList<IGenome> genomes)
    {
        if (genomes[0] is GpuFeedForwardNetwork gpuGenome)
        {
            return gpuGenome.OutputIndices.Length;
        }

        // For non-GPU genomes, we need to determine output count.
        // The fitness function's output slice per case is totalOutputs / caseCount.
        // We can't determine this from IGenome alone without additional context.
        // Default assumption: 1 output. Callers should ensure proper genome types.
        return 1;
    }

    /// <summary>
    /// Releases all GPU resources including memory buffers, accelerator, and context.
    /// </summary>
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (disposing)
        {
            // Dispose all GPU memory buffers
            _genomeNodeOffsetsBuffer?.Dispose();
            _genomeNodeCountsBuffer?.Dispose();
            _genomeEvalOrderOffsetsBuffer?.Dispose();
            _genomeEvalOrderCountsBuffer?.Dispose();
            _genomeInputOffsetsBuffer?.Dispose();
            _genomeInputCountsBuffer?.Dispose();
            _genomeOutputOffsetsBuffer?.Dispose();
            _genomeOutputCountsBuffer?.Dispose();
            _genomeBiasOffsetsBuffer?.Dispose();
            _genomeBiasCountsBuffer?.Dispose();
            _nodeActivationTypesBuffer?.Dispose();
            _evalNodeIndicesBuffer?.Dispose();
            _evalNodeConnOffsetsBuffer?.Dispose();
            _evalNodeConnCountsBuffer?.Dispose();
            _incomingSourceIndicesBuffer?.Dispose();
            _incomingWeightsBuffer?.Dispose();
            _inputNodeIndicesBuffer?.Dispose();
            _outputNodeIndicesBuffer?.Dispose();
            _biasNodeIndicesBuffer?.Dispose();
            _testInputsBuffer?.Dispose();
            _testOutputsBuffer?.Dispose();
            _activationBuffer?.Dispose();

            // Dispose accelerator and context
            _accelerator?.Dispose();
            _context?.Dispose();
        }

        _disposed = true;
    }

    /// <summary>
    /// Finalizer to ensure GPU resources are released.
    /// </summary>
    ~GpuBatchEvaluator()
    {
        Dispose(disposing: false);
    }
}
