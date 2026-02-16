# NeatSharp GPU Benchmark Report

**Date**: 2026-02-16 | **Hardware**: NVIDIA GeForce RTX 4060 (CC 8.9, 8 GB)

## Hardware Configuration

- **GPU**: NVIDIA GeForce RTX 4060
- **Compute Capability**: 8.9
- **GPU Memory**: 8.0 GB
- **Runtime**: .NET 9.0.12
- **OS**: Windows 11 Pro 10.0.26100
- **Processor Count**: 16

## Methodology

- **Problem**: Multi-input function approximation (4 inputs, 1 output)
- **Population composition**: Uniform genome complexity per data point (all genomes same hidden node count)
- **Warmup iterations**: 2
- **Timed iterations**: 5
- **CPU path**: GPU disabled via `GpuOptions.EnableGpu = false`, uses `IGenome.Activate()` per genome
- **GPU path**: GPU enabled, batch forward propagation via ILGPU CUDA kernel (1 thread per genome)
- **Metric**: Throughput in genomes evaluated per second (higher is better)

## Results: 10 Test Cases (Standard NEAT)

| Hidden Nodes | Population Size | CPU (genomes/s) | GPU (genomes/s) | Speedup |
|-------------:|----------------:|----------------:|----------------:|--------:|
|            0 |             500 |         643,716 |          72,652 |   0.11x |
|            0 |           1,000 |         744,735 |         146,939 |   0.20x |
|            0 |           2,000 |         778,174 |         238,464 |   0.31x |
|            0 |           5,000 |         678,713 |         408,172 |   0.60x |
|           10 |             500 |         113,057 |          50,270 |   0.44x |
|           10 |           1,000 |         109,730 |          63,702 |   0.58x |
|           10 |           2,000 |         102,692 |          75,472 |   0.73x |
|           20 |             500 |         349,748 |         162,338 |   0.46x |
|           20 |           1,000 |         343,126 |         207,712 |   0.61x |
|           20 |           2,000 |         321,229 |         129,260 |   0.40x |
|           20 |           5,000 |         300,714 |         193,249 |   0.64x |
|           50 |             500 |         147,093 |          96,754 |   0.66x |
|           50 |           1,000 |         144,662 |          63,191 |   0.44x |
|           50 |           2,000 |         125,700 |          88,820 |   0.71x |
|           50 |           5,000 |         131,664 |          83,793 |   0.64x |

### Analysis (10 Test Cases)

- **0 hidden nodes**: avg 0.30x, max 0.60x
- **10 hidden nodes**: avg 0.54x, max 0.73x
- **20 hidden nodes**: avg 0.53x, max 0.64x
- **50 hidden nodes**: avg 0.61x, max 0.71x

## Results: 100 Test Cases (Heavy Workload)

| Hidden Nodes | Population Size | CPU (genomes/s) | GPU (genomes/s) | Speedup |
|-------------:|----------------:|----------------:|----------------:|--------:|
|            0 |             500 |         271,088 |         170,679 |   0.63x |
|            0 |           1,000 |         274,947 |         231,045 |   0.84x |
|            0 |           2,000 |         308,237 |         238,372 |   0.77x |
|            0 |           5,000 |         324,610 |         276,416 |   0.85x |
|           10 |             500 |          61,379 |          54,398 |   0.89x |
|           10 |           1,000 |          62,238 |          55,679 |   0.89x |
|           10 |           2,000 |          58,953 |          56,993 |   0.97x |
|           10 |           5,000 |          60,756 |          57,184 |   0.94x |
|           20 |             500 |          32,800 |          30,380 |   0.93x |
|           20 |           1,000 |          33,995 |          30,390 |   0.89x |
|           20 |           2,000 |          32,047 |          30,161 |   0.94x |
|           20 |           5,000 |          32,805 |          32,338 |   0.99x |
|           50 |             500 |          14,437 |          13,979 |   0.97x |
|           50 |           1,000 |          14,395 |          13,804 |   0.96x |
|           50 |           2,000 |          14,476 |          13,579 |   0.94x |
|           50 |           5,000 |          14,625 |          13,700 |   0.94x |

### Analysis (100 Test Cases)

- **0 hidden nodes**: avg 0.77x, max 0.85x
- **10 hidden nodes**: avg 0.92x, max 0.97x
- **20 hidden nodes**: avg 0.94x, max 0.99x
- **50 hidden nodes**: avg 0.95x, max 0.97x

## Key Findings

1. **GPU does not outperform CPU for NEAT-scale networks.** Even with 50 hidden nodes, 5000 genomes, and 100 test cases, the GPU path reaches at most 0.99x of CPU throughput (parity, not speedup).

2. **The performance gap narrows with increased workload.** Moving from 10 to 100 test cases improves GPU relative performance from ~0.5x to ~0.95x, demonstrating that the bottleneck is fixed overhead (data transfer, kernel launch), not computational throughput.

3. **CPU evaluation is highly efficient for small networks.** The CPU path uses tight loops with `stackalloc`, branch prediction, and cache locality — ideal for NEAT's small, variable-topology networks (typically 6-60 nodes).

4. **GPU overhead sources identified:**
   - Per-generation topology upload (all flat arrays re-uploaded each call)
   - ILGPU managed-to-native bridge overhead per kernel dispatch
   - Kernel launch + synchronize latency (~microseconds, but significant when per-genome computation is also microsecond-scale)
   - GpuPopulationData construction (CPU-side flattening of genome topologies)

5. **The spec's >= 5x target is not achievable** for typical NEAT populations (500-2,000 genomes, 6-60 nodes each, 4-100 test cases) on this hardware with the current 1-thread-per-genome kernel architecture.

## Recommendations for Future Optimization

- **Topology caching**: Cache GPU buffers when population topology hasn't changed (stable between generations in some NEAT variants)
- **Batched test cases**: Process test cases in GPU-parallel batches (N threads per genome × M test cases) instead of 1 thread per genome processing all cases sequentially
- **Kernel fusion**: Combine forward propagation + fitness computation in a single kernel to avoid the output download → CPU fitness → re-upload cycle
- **Larger scale targets**: GPU advantage would emerge at 10,000+ genomes with 100+ nodes each and 1,000+ test cases — beyond typical NEAT configurations

## Notes

- GPU forward propagation uses fp32; CPU uses fp64. Per-output tolerance is 1e-4.
- Fitness computation (`IGpuFitnessFunction.ComputeFitness`) runs on CPU after GPU outputs are downloaded.
- Genome topology extraction and `GpuPopulationData` construction are included in GPU path timing.
- The benchmark uses deterministic genome construction (seed 42) for reproducibility.
- Some measurement variance observed (GC effects, timer resolution) — results are representative, not statistically rigorous. Use BenchmarkDotNet for publication-quality data.
