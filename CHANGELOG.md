# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.1.0] - Unreleased

### Added

- Core NEAT API with genome, network, and evolution types (001-core-api-baseline)
- Genome encoding and feed-forward phenotype network building (002-genome-phenotype)
- Mutation operators: add node, add connection, weight perturbation, weight replacement, toggle enable (003-evolution-operators)
- Crossover with gene alignment by innovation number (003-evolution-operators)
- Speciation via compatibility distance with configurable thresholds (003-evolution-operators)
- Selection strategies: tournament selection, roulette wheel, reproduction allocation (003-evolution-operators)
- Training loop with NeatEvolver, stopping criteria, and complexity limits (004-training-runner)
- Checkpoint serialization with versioned schema and backward compatibility (005-versioned-serialization)
- GPU-accelerated fitness evaluation via ILGPU/CUDA (006-cuda-evaluator)
- Hybrid CPU+GPU evaluation with adaptive PID-controlled partitioning (007-hybrid-eval-scheduler)
- Static, cost-based, and adaptive partition policies for hybrid evaluation (007-hybrid-eval-scheduler)
- NuGet packages: NeatSharp and NeatSharp.Gpu with complete metadata (008-release-readiness)
- Three runnable examples: XOR, Sine Approximation, Cart-Pole (008-release-readiness)
- BenchmarkDotNet-based benchmark suite for CPU, GPU, and hybrid evaluation (008-release-readiness)
- Benchmark regression comparison tool with configurable threshold (008-release-readiness)
- GitHub Actions CI pipeline: format check, build/test matrix, pack, benchmark trend (008-release-readiness)
- Comprehensive documentation: NEAT basics, parameter tuning, reproducibility, checkpointing, GPU setup, troubleshooting, offline usage (008-release-readiness)
- CONTRIBUTING.md with development setup, code style, and PR process (008-release-readiness)
- .editorconfig with code style enforcement and .gitattributes for line ending normalization (008-release-readiness)
