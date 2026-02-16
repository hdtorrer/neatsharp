# Research: CUDA Evaluator Backend

**Feature**: 006-cuda-evaluator | **Date**: 2026-02-15

## R-001: GPU Interop Library Selection

**Decision**: ILGPU (managed .NET GPU compiler)

**Rationale**: ILGPU allows writing GPU kernels in C# which are compiled to PTX (NVIDIA) at runtime. This eliminates the need for a native CUDA SDK at build time — only the CUDA runtime/driver is needed on the target machine. It integrates naturally with .NET 8/9, supports nullable reference types, and provides a CPU accelerator backend for testing without GPU hardware.

**Alternatives Considered**:
- **ManagedCuda**: Lower-level wrapper around CUDA Driver API. Requires writing CUDA C kernels separately and managing PTX files. Rejected because it violates the "write kernels in C#" goal and adds build-time CUDA SDK dependency.
- **CUDA via P/Invoke (raw interop)**: Maximum control but massive maintenance burden. Requires managing native binaries per platform, manual memory marshalling, and separate CUDA C compilation. Rejected for complexity.
- **ComputeSharp**: GPU compute via DirectX 12 / HLSL. Windows-only, no CUDA. Rejected because it doesn't support Linux (constitution Principle V requires cross-platform).

**Key ILGPU properties**:
- NuGet packages: `ILGPU` 1.5.x, `ILGPU.Algorithms` 1.5.x
- Targets .NET 6+; compatible with .NET 8 and .NET 9
- Supports CUDA (PTX), OpenCL, and CPU accelerator backends
- Kernels written as static C# methods with `Index1D`/`Index2D` parameters
- Kernel limitations: no classes (reference types), no exceptions, no delegates, no recursion, no dynamic allocation — value types and `ArrayView<T>` only
- Math via `ILGPU.Algorithms.XMath` (GPU-compatible IEEE 754 fp32)

---

## R-002: GPU Topology Data Extraction Strategy

**Decision**: `GpuNetworkBuilder` decorator pattern — decorates existing `INetworkBuilder` to produce `GpuFeedForwardNetwork` instances carrying both flat GPU arrays and a CPU-fallback phenotype.

**Rationale**: The existing evaluation flow (`NeatEvolver` → `INetworkBuilder.Build(genome)` → `IGenome[]` → `IBatchEvaluator.EvaluateAsync()`) builds phenotypes before evaluation. The GPU evaluator receives `IGenome` instances but needs raw topology data for GPU upload. By replacing `INetworkBuilder` via DI, we intercept the build step to produce GPU-enriched phenotypes without modifying the evolver or the `IBatchEvaluator` contract.

**Design**:
1. `GpuNetworkBuilder` wraps `FeedForwardNetworkBuilder` (injected via constructor)
2. Calls the inner builder to get the standard CPU phenotype (`IGenome`)
3. Processes the `Genome` to extract flat GPU-friendly arrays (nodes, connections, weights, eval order)
4. Returns `GpuFeedForwardNetwork` wrapping both the CPU phenotype and the flat arrays
5. `GpuBatchEvaluator` type-checks received `IGenome` instances for `GpuFeedForwardNetwork` and extracts the flat data

**Alternatives Considered**:
- **Extract topology from `FeedForwardNetwork` directly**: `FeedForwardNetwork` is internal with private fields. Even with `InternalsVisibleTo`, the internal `EvalNode` record stores `Func<double, double>` delegates which can't be transferred to GPU. Would need to re-derive activation function types anyway. Rejected because it's harder, not easier.
- **Bypass `INetworkBuilder` entirely**: Have the GPU evaluator receive raw `Genome` objects via a separate data pathway. Rejected because it requires modifying `NeatEvolver` and the `IBatchEvaluator` contract, violating the spec's requirement to use the existing batch evaluation contract.
- **Add topology properties to `IGenome` interface**: Would expose internal details in the public API. Rejected as a public API change affecting all consumers.

---

## R-003: GPU Data Layout for Variable-Topology Networks

**Decision**: Struct-of-Arrays (SoA) layout with offset-indexed flat arrays per genome, one GPU thread per genome.

**Rationale**: NEAT genomes have variable topologies (different node/connection counts). A flat array layout with per-genome offset/count indices allows all genomes to share contiguous GPU buffers while accommodating different sizes. SoA layout provides better GPU memory coalescing than Array-of-Structs (AoS). One thread per genome is the right parallelism level because individual genomes are small (tens of nodes/connections) — the speedup comes from evaluating many genomes simultaneously, not from parallelizing within a single genome.

**GPU Buffer Layout** (uploaded per generation):
```
// Per-genome indexing
int[]   genomeNodeOffsets       [populationSize]     // Start index into node arrays
int[]   genomeNodeCounts        [populationSize]     // Node count per genome
int[]   genomeConnectionOffsets [populationSize]     // Start index into connection arrays
int[]   genomeConnectionCounts  [populationSize]     // Connection count per genome
int[]   genomeEvalOrderOffsets  [populationSize]     // Start index into eval order
int[]   genomeEvalOrderCounts   [populationSize]     // Eval order length per genome
int[]   genomeInputOffsets      [populationSize]     // Start index into input index array
int[]   genomeInputCounts       [populationSize]     // Input count per genome
int[]   genomeOutputOffsets     [populationSize]     // Start index into output index array
int[]   genomeOutputCounts      [populationSize]     // Output count per genome

// Flattened node data (SoA)
float[] nodeActivations         [totalNodes]         // Activation buffer (zeroed per test case)
int[]   nodeActivationTypes     [totalNodes]         // Enum: 0=sigmoid, 1=tanh, 2=relu, 3=step, 4=identity

// Flattened eval order (topologically sorted hidden+output nodes)
int[]   evalNodeIndices         [totalEvalNodes]     // Node buffer index in eval order
int[]   evalNodeConnOffsets     [totalEvalNodes]     // Start index into incoming connections
int[]   evalNodeConnCounts      [totalEvalNodes]     // Incoming connection count

// Flattened incoming connections per eval node
int[]   incomingSourceIndices   [totalIncoming]      // Source node buffer index
float[] incomingWeights         [totalIncoming]      // Connection weight (fp32)

// Input/output node mappings
int[]   inputNodeIndices        [totalInputs]        // Buffer indices for input nodes
int[]   outputNodeIndices       [totalOutputs]       // Buffer indices for output nodes

// Test case inputs (shared across all genomes, uploaded once)
float[] testInputs              [caseCount * inputCount]

// Outputs (downloaded after kernel)
float[] testOutputs             [populationSize * caseCount * outputCount]
```

**Alternatives Considered**:
- **Fixed-size padded networks**: Allocate max-possible-size buffers for all genomes and pad unused slots. Simpler code but wastes GPU memory proportional to (population × maxComplexity), unsuitable for heterogeneous populations where the largest genome is much larger than the median.
- **Binning by size**: Group genomes into buckets by topology complexity and launch separate kernels per bucket. Better GPU utilization for very heterogeneous populations, but adds significant complexity (multiple kernel launches, genome reindexing). Deferred to future optimization if profiling shows divergence issues.
- **Array-of-Structs (AoS)**: Pack all data for each node/connection into a struct. Poor memory coalescing on GPU — adjacent threads would load non-contiguous memory. Rejected for performance reasons.

---

## R-004: GPU Fitness Function Abstraction

**Decision**: `IGpuFitnessFunction` interface — user provides test case inputs and a CPU-side fitness computation callback.

**Rationale**: GPU batch evaluation handles the expensive part (forward propagation of all genomes across all test cases). Fitness computation (e.g., sum of errors, classification accuracy) varies per problem and is cheap relative to forward propagation. Allowing the user to define fitness computation in arbitrary C# (on CPU, after GPU outputs are downloaded) maximizes flexibility while the GPU does the heavy lifting.

**Interface**:
```csharp
public interface IGpuFitnessFunction
{
    int CaseCount { get; }
    ReadOnlyMemory<float> InputCases { get; }  // [caseCount * inputCount], row-major
    double ComputeFitness(ReadOnlySpan<float> outputs);  // [caseCount * outputCount] → fitness
}
```

**Flow**:
1. GPU evaluator uploads population topology + `IGpuFitnessFunction.InputCases` to GPU
2. GPU kernel runs forward propagation: all genomes × all test cases
3. GPU evaluator downloads output buffer: `[populationSize × caseCount × outputCount]`
4. For each genome, slices its outputs and calls `IGpuFitnessFunction.ComputeFitness()`
5. Reports fitness via `setFitness(index, fitness)` callback

**Alternatives Considered**:
- **GPU-side fitness computation**: Run fitness kernel on GPU for maximum throughput. Rejected for v1 — adds complexity (another kernel, GPU-compatible fitness constraints) for marginal benefit since fitness computation is O(caseCount × outputCount) per genome while forward propagation is O(nodes × connections).
- **Reuse existing `Func<IGenome, double>` pattern**: User calls `genome.Activate()` in their lambda. On GPU, this means each genome would be CPU-evaluated individually — no batch speedup. Rejected because it defeats the purpose.
- **Expression tree compilation**: User writes fitness in a restricted C# expression that gets compiled to a GPU kernel. Powerful but extremely complex to implement. Deferred to post-v1.

---

## R-005: GPU Auto-Detection and Fallback Strategy

**Decision**: Detect GPU at `AddNeatSharpGpu()` registration time (lazy, on first use). Fall back to CPU per-generation on any GPU failure.

**Rationale**: GPU detection should happen once, early, to avoid repeated detection overhead. But detection at DI registration time is too early (container not yet built). Detection on first `EvaluateAsync` call with the result cached is the right balance. Per-generation fallback (not per-genome) matches the spec requirement and avoids the overhead of trying GPU → catching failure → retrying CPU for each genome.

**Detection Flow**:
1. `GpuDeviceDetector.Detect()` creates an ILGPU `Context`, enumerates CUDA devices
2. For each device, checks compute capability >= 5.0 (Maxwell)
3. Returns `GpuDeviceInfo` (name, compute capability, memory, compatibility status) or null
4. If no compatible device found, returns null with diagnostic info
5. Result is cached — subsequent calls return the cached result

**Fallback Flow**:
1. `GpuBatchEvaluator.EvaluateAsync()` checks if GPU is available (cached detection result)
2. If no GPU: delegates to CPU evaluation via the CPU-fallback phenotypes (`GpuFeedForwardNetwork._cpuNetwork.Activate()`)
3. If GPU available but evaluation fails (CUDA error, OOM, device lost):
   a. Logs warning with error details
   b. Falls back to CPU evaluation for the entire generation
   c. Marks GPU as "failed" for the current generation (retries next generation)
4. Diagnostic messages include: device name, compute capability, required CC, memory info, and installation guidance URLs

**Alternatives Considered**:
- **Detect at DI registration**: Too early — `IServiceProvider` not yet built, can't inject logger. Also delays container build on machines without GPU (ILGPU context creation can take ~100ms).
- **Detect at every `EvaluateAsync` call**: Wasteful. GPU availability doesn't change between generations. Rejected for performance.
- **Per-genome fallback**: Try GPU for each genome individually, fall back to CPU on failure. Rejected because GPU evaluation is fundamentally a batch operation — per-genome fallback adds latency overhead and eliminates the batch advantage.

---

## R-006: Activation Function GPU Implementation

**Decision**: Encode activation functions as integer enum, implement as a `switch` in GPU kernel code using `ILGPU.Algorithms.XMath` for fp32 math.

**Rationale**: ILGPU kernels cannot use delegates (`Func<double, double>`) — they're reference types. The 5 built-in activation functions must be re-implemented as GPU-compatible fp32 math using `XMath`. The function type is encoded as an integer per node, and a switch statement dispatches to the correct implementation in the kernel.

**Enum mapping**:
```csharp
internal enum GpuActivationFunction : int
{
    Sigmoid = 0,   // 1 / (1 + exp(-4.9 * x)) — steepened, matching CPU NEAT
    Tanh = 1,      // tanh(x)
    ReLU = 2,      // max(0, x)
    Step = 3,      // x > 0 ? 1 : 0
    Identity = 4   // x
}
```

**GPU implementation**:
```csharp
static float ApplyActivation(float x, int activationType)
{
    return activationType switch
    {
        0 => 1.0f / (1.0f + XMath.Exp(-4.9f * x)),  // Sigmoid
        1 => XMath.Tanh(x),                           // Tanh
        2 => XMath.Max(0.0f, x),                      // ReLU
        3 => x > 0.0f ? 1.0f : 0.0f,                  // Step
        _ => x                                         // Identity (default)
    };
}
```

**Precision note**: CPU uses `double` (fp64) with `Math.Exp(-4.9 * x)`. GPU uses `float` (fp32) with `XMath.Exp(-4.9f * x)`. The 1e-4 epsilon tolerance accounts for this precision difference. The steepened sigmoid slope (4.9) is preserved exactly.

**Alternatives Considered**:
- **Function pointer array**: ILGPU supports some function pointer patterns, but they're complex and the dispatch overhead for 5 functions is negligible compared to a simple switch. Rejected for simplicity.
- **Separate kernel per activation function**: Launch different kernels based on activation function. Impractical because NEAT genomes mix activation functions within a single network. Rejected.

---

## R-007: GPU Memory Management Strategy

**Decision**: Pool GPU buffers across generations. Allocate maximum-capacity buffers on first use, resize only when population grows.

**Rationale**: Per the constitution's Resource Management rules, "GPU memory allocations MUST be pooled or reused across generations; allocation-per-generation patterns are prohibited in hot paths." ILGPU `MemoryBuffer1D<T>` allocations are relatively expensive. Pooling buffers and only resizing when the population exceeds current capacity minimizes allocation overhead.

**Strategy**:
1. On first `EvaluateAsync` call, allocate all GPU buffers sized for the current population
2. On subsequent calls, check if current buffers are large enough:
   - If yes: reuse (no allocation, just re-upload data)
   - If no: dispose old buffers, allocate new larger ones
3. All buffers disposed in `GpuBatchEvaluator.Dispose()`
4. Track total GPU memory usage; if allocation fails, throw `GpuOutOfMemoryException` with actionable message (current population size, estimated memory requirement, available GPU memory)

**Dispose Pattern**:
```csharp
public sealed class GpuBatchEvaluator : IBatchEvaluator, IDisposable
{
    private bool _disposed;

    public void Dispose()
    {
        if (!_disposed)
        {
            // Dispose GPU buffers, accelerator, context
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }

    ~GpuBatchEvaluator() => Dispose();
}
```

---

## R-008: Best-Effort GPU Determinism

**Decision**: Achieve determinism by assigning one GPU thread per genome and using sequential (not atomic) accumulation within each thread.

**Rationale**: GPU non-determinism typically arises from non-associative floating-point operations executed in variable order (e.g., parallel reductions with `Atomic.Add`). Since our kernel assigns one thread per genome and each thread processes its genome's eval nodes sequentially in a fixed topological order, the forward propagation is deterministic given:
1. Same genome topology
2. Same inputs
3. Same thread assignment (guaranteed by ILGPU's `LoadAutoGroupedStreamKernel` with the same grid size)

**Guarantees**:
- Same machine, same driver, same population → identical GPU results across runs
- Different machines or driver versions → results may vary at the ULP level (due to different GPU hardware fp32 implementations)
- GPU results are NOT bitwise identical to CPU results (fp32 vs fp64)

**Non-determinism sources documented**:
- Driver-level variations between GPU architectures
- fp32 vs fp64 precision differences
- Fused multiply-add (FMA) instructions that GPU hardware may apply automatically

---

## R-009: ILGPU CPU Accelerator for Testing

**Decision**: Use ILGPU's built-in `CPUAccelerator` for unit testing GPU kernel logic without requiring GPU hardware.

**Rationale**: ILGPU provides a `CPUAccelerator` that executes GPU kernels as multi-threaded CPU code using the same API. This allows testing kernel correctness, buffer management, and the full evaluation pipeline on any machine (including CI servers without GPUs). Hardware-specific tests (performance benchmarks, actual CUDA execution) are gated with `[Trait("Category", "GPU")]`.

**Test Strategy**:
- Unit tests: Use `CPUAccelerator` — tests run everywhere, validate logic
- Integration tests: Use `CudaAccelerator` with `[Trait("Category", "GPU")]` — validate actual GPU execution
- Tolerance tests: Run both CPU and GPU paths, compare within epsilon
- CI configuration: Run unit tests always; GPU trait tests only on GPU-enabled runners
