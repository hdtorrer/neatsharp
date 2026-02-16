namespace NeatSharp.Gpu.Evaluation;

/// <summary>
/// Flattens a list of <see cref="GpuFeedForwardNetwork"/> into contiguous
/// Structure-of-Arrays (SoA) layout for efficient GPU buffer upload.
/// </summary>
/// <remarks>
/// <para>
/// All per-genome arrays are concatenated into flat arrays with offset/count
/// pairs for indexing. Node buffer indices are adjusted by each genome's node
/// offset to create globally unique indices in the flattened arrays.
/// </para>
/// <para>
/// This class is constructed once per generation and its arrays are uploaded
/// to GPU memory buffers by <see cref="GpuBatchEvaluator"/>.
/// </para>
/// </remarks>
internal sealed class GpuPopulationData
{
    /// <summary>
    /// Initializes a new instance of <see cref="GpuPopulationData"/> by flattening
    /// the provided genomes into contiguous SoA arrays.
    /// </summary>
    /// <param name="genomes">The GPU feed-forward networks to flatten.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="genomes"/> is empty.</exception>
    internal GpuPopulationData(IReadOnlyList<GpuFeedForwardNetwork> genomes)
    {
        ArgumentNullException.ThrowIfNull(genomes);

        if (genomes.Count == 0)
        {
            throw new ArgumentException("Population must contain at least one genome.", nameof(genomes));
        }

        PopulationSize = genomes.Count;

        // Phase 1: Calculate totals and per-genome offsets
        GenomeNodeOffsets = new int[PopulationSize];
        GenomeNodeCounts = new int[PopulationSize];
        GenomeEvalOrderOffsets = new int[PopulationSize];
        GenomeEvalOrderCounts = new int[PopulationSize];
        GenomeInputOffsets = new int[PopulationSize];
        GenomeInputCounts = new int[PopulationSize];
        GenomeOutputOffsets = new int[PopulationSize];
        GenomeOutputCounts = new int[PopulationSize];
        GenomeBiasOffsets = new int[PopulationSize];
        GenomeBiasCounts = new int[PopulationSize];

        int nodeOffset = 0;
        int evalOrderOffset = 0;
        int incomingOffset = 0;
        int inputOffset = 0;
        int outputOffset = 0;
        int biasOffset = 0;

        for (int i = 0; i < PopulationSize; i++)
        {
            var genome = genomes[i];

            GenomeNodeOffsets[i] = nodeOffset;
            GenomeNodeCounts[i] = genome.NodeCount;

            GenomeEvalOrderOffsets[i] = evalOrderOffset;
            GenomeEvalOrderCounts[i] = genome.EvalOrder.Length;

            GenomeInputOffsets[i] = inputOffset;
            GenomeInputCounts[i] = genome.InputIndices.Length;

            GenomeOutputOffsets[i] = outputOffset;
            GenomeOutputCounts[i] = genome.OutputIndices.Length;

            GenomeBiasOffsets[i] = biasOffset;
            GenomeBiasCounts[i] = genome.BiasIndices.Length;

            // Count incoming connections for this genome
            int genomeIncoming = 0;
            for (int e = 0; e < genome.EvalOrder.Length; e++)
            {
                genomeIncoming += genome.EvalOrder[e].IncomingSources.Length;
            }

            nodeOffset += genome.NodeCount;
            evalOrderOffset += genome.EvalOrder.Length;
            incomingOffset += genomeIncoming;
            inputOffset += genome.InputIndices.Length;
            outputOffset += genome.OutputIndices.Length;
            biasOffset += genome.BiasIndices.Length;
        }

        TotalNodes = nodeOffset;
        TotalEvalNodes = evalOrderOffset;
        TotalIncoming = incomingOffset;
        TotalInputs = inputOffset;
        TotalOutputs = outputOffset;
        TotalBiasNodes = biasOffset;

        // Phase 2: Allocate flat arrays
        NodeActivationTypes = new int[TotalNodes];
        EvalNodeIndices = new int[TotalEvalNodes];
        EvalNodeConnOffsets = new int[TotalEvalNodes];
        EvalNodeConnCounts = new int[TotalEvalNodes];
        IncomingSourceIndices = new int[TotalIncoming];
        IncomingWeights = new float[TotalIncoming];
        InputNodeIndices = new int[TotalInputs];
        OutputNodeIndices = new int[TotalOutputs];
        BiasNodeIndices = new int[TotalBiasNodes];

        // Phase 3: Populate flat arrays with offset-adjusted indices
        int connFillOffset = 0;

        for (int i = 0; i < PopulationSize; i++)
        {
            var genome = genomes[i];
            int genomeNodeOff = GenomeNodeOffsets[i];

            // Copy node activation types (adjusted by genome node offset)
            Array.Copy(genome.NodeActivationTypes, 0, NodeActivationTypes, genomeNodeOff, genome.NodeCount);

            // Copy eval order nodes
            int evalOff = GenomeEvalOrderOffsets[i];
            for (int e = 0; e < genome.EvalOrder.Length; e++)
            {
                var evalNode = genome.EvalOrder[e];
                int globalEvalIdx = evalOff + e;

                // Adjust buffer index by genome's node offset for global uniqueness
                EvalNodeIndices[globalEvalIdx] = evalNode.BufferIndex + genomeNodeOff;
                EvalNodeConnOffsets[globalEvalIdx] = connFillOffset;
                EvalNodeConnCounts[globalEvalIdx] = evalNode.IncomingSources.Length;

                // Copy incoming connections with adjusted source indices
                for (int c = 0; c < evalNode.IncomingSources.Length; c++)
                {
                    IncomingSourceIndices[connFillOffset + c] = evalNode.IncomingSources[c] + genomeNodeOff;
                    IncomingWeights[connFillOffset + c] = evalNode.IncomingWeights[c];
                }

                connFillOffset += evalNode.IncomingSources.Length;
            }

            // Copy input node indices (adjusted by genome node offset)
            int inputOff = GenomeInputOffsets[i];
            for (int j = 0; j < genome.InputIndices.Length; j++)
            {
                InputNodeIndices[inputOff + j] = genome.InputIndices[j] + genomeNodeOff;
            }

            // Copy output node indices (adjusted by genome node offset)
            int outputOff = GenomeOutputOffsets[i];
            for (int j = 0; j < genome.OutputIndices.Length; j++)
            {
                OutputNodeIndices[outputOff + j] = genome.OutputIndices[j] + genomeNodeOff;
            }

            // Copy bias node indices (adjusted by genome node offset)
            int biasOff = GenomeBiasOffsets[i];
            for (int j = 0; j < genome.BiasIndices.Length; j++)
            {
                BiasNodeIndices[biasOff + j] = genome.BiasIndices[j] + genomeNodeOff;
            }
        }
    }

    /// <summary>Gets the number of genomes in the population.</summary>
    internal int PopulationSize { get; }

    /// <summary>Gets the total number of nodes across all genomes.</summary>
    internal int TotalNodes { get; }

    /// <summary>Gets the total number of eval order nodes across all genomes.</summary>
    internal int TotalEvalNodes { get; }

    /// <summary>Gets the total number of incoming connections across all eval nodes.</summary>
    internal int TotalIncoming { get; }

    /// <summary>Gets the total number of input nodes across all genomes.</summary>
    internal int TotalInputs { get; }

    /// <summary>Gets the total number of output nodes across all genomes.</summary>
    internal int TotalOutputs { get; }

    /// <summary>Gets the total number of bias nodes across all genomes.</summary>
    internal int TotalBiasNodes { get; }

    /// <summary>Gets the start index per genome in node arrays. Length: PopulationSize.</summary>
    internal int[] GenomeNodeOffsets { get; }

    /// <summary>Gets the node count per genome. Length: PopulationSize.</summary>
    internal int[] GenomeNodeCounts { get; }

    /// <summary>Gets the start index per genome in eval order arrays. Length: PopulationSize.</summary>
    internal int[] GenomeEvalOrderOffsets { get; }

    /// <summary>Gets the eval order length per genome. Length: PopulationSize.</summary>
    internal int[] GenomeEvalOrderCounts { get; }

    /// <summary>Gets the start index per genome in input arrays. Length: PopulationSize.</summary>
    internal int[] GenomeInputOffsets { get; }

    /// <summary>Gets the input count per genome. Length: PopulationSize.</summary>
    internal int[] GenomeInputCounts { get; }

    /// <summary>Gets the start index per genome in output arrays. Length: PopulationSize.</summary>
    internal int[] GenomeOutputOffsets { get; }

    /// <summary>Gets the output count per genome. Length: PopulationSize.</summary>
    internal int[] GenomeOutputCounts { get; }

    /// <summary>Gets the start index per genome in bias arrays. Length: PopulationSize.</summary>
    internal int[] GenomeBiasOffsets { get; }

    /// <summary>Gets the bias count per genome. Length: PopulationSize.</summary>
    internal int[] GenomeBiasCounts { get; }

    /// <summary>Gets the activation type per node. Length: TotalNodes.</summary>
    internal int[] NodeActivationTypes { get; }

    /// <summary>Gets the globally-adjusted node buffer index in eval order. Length: TotalEvalNodes.</summary>
    internal int[] EvalNodeIndices { get; }

    /// <summary>Gets the start index into incoming arrays per eval node. Length: TotalEvalNodes.</summary>
    internal int[] EvalNodeConnOffsets { get; }

    /// <summary>Gets the incoming connection count per eval node. Length: TotalEvalNodes.</summary>
    internal int[] EvalNodeConnCounts { get; }

    /// <summary>Gets the globally-adjusted source node buffer indices. Length: TotalIncoming.</summary>
    internal int[] IncomingSourceIndices { get; }

    /// <summary>Gets the connection weights (fp32). Length: TotalIncoming.</summary>
    internal float[] IncomingWeights { get; }

    /// <summary>Gets the globally-adjusted buffer indices for all input nodes. Length: TotalInputs.</summary>
    internal int[] InputNodeIndices { get; }

    /// <summary>Gets the globally-adjusted buffer indices for all output nodes. Length: TotalOutputs.</summary>
    internal int[] OutputNodeIndices { get; }

    /// <summary>Gets the globally-adjusted buffer indices for all bias nodes. Length: TotalBiasNodes.</summary>
    internal int[] BiasNodeIndices { get; }
}
