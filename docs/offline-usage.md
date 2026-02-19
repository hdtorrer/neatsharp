# Offline Usage Guide

This guide explains which NeatSharp operations require network connectivity and how to configure NeatSharp for air-gapped or offline environments.

## What Works Offline

After the initial `dotnet restore` (which downloads NuGet packages), **all NeatSharp functionality works fully offline**:

- **Evolution and training**: `INeatEvolver.EvolveAsync()` runs entirely in-memory with no network calls.
- **GPU evaluation**: ILGPU communicates directly with the local GPU hardware. No network required.
- **Hybrid CPU+GPU evaluation**: Partitioning and concurrent evaluation are local operations.
- **Checkpointing**: `ICheckpointSerializer.SaveAsync()` and `LoadAsync()` write to and read from `System.IO.Stream` (local files, memory streams, etc.).
- **Device detection**: `IGpuDeviceDetector.Detect()` queries the local CUDA runtime. No network calls.
- **All configuration and validation**: The .NET Options pattern validates locally at startup.

## What Requires Network Connectivity

Only one operation requires network access:

### NuGet Package Restore

The `dotnet restore` command (run implicitly by `dotnet build` and `dotnet run`) downloads NuGet packages from configured package sources. This is needed:

- When first cloning the repository.
- When adding or updating NeatSharp package references.
- When building on a new machine that does not have the packages cached locally.

Once packages are restored and cached in the local NuGet cache (typically `~/.nuget/packages` on Linux/macOS or `%USERPROFILE%\.nuget\packages` on Windows), no further network access is needed for builds or runs.

## Setting Up for Air-Gapped Environments

For environments with no internet access (air-gapped, classified networks, isolated CI servers), you can configure NuGet to use a local package source.

### Step 1: Download Packages on a Connected Machine

On a machine with internet access, restore all packages and identify the cached files:

```bash
# Clone the repository and restore packages
git clone https://github.com/hdtorrer/neatsharp.git
cd neatsharp
dotnet restore NeatSharp.sln

# The packages are now cached in the global NuGet cache
# Linux/macOS: ~/.nuget/packages
# Windows: %USERPROFILE%\.nuget\packages
```

### Step 2: Create a Local Package Source

Copy the relevant packages from the NuGet cache to a directory that will be transferred to the air-gapped machine:

```bash
# Create a directory for the local feed
mkdir -p /path/to/local-feed

# Copy the required packages (example for key dependencies)
# The exact packages and versions can be found in your .csproj files
# and the NuGet cache after restore
```

Alternatively, use `dotnet nuget push` to create a structured local feed:

```bash
# For each .nupkg file:
dotnet nuget push SomePackage.1.0.0.nupkg --source /path/to/local-feed
```

### Step 3: Configure the Local Package Source

On the air-gapped machine, create or edit a `NuGet.config` file at the repository root:

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <!-- Remove the default nuget.org source -->
    <clear />
    <!-- Add local source only -->
    <add key="local" value="/path/to/local-feed" />
  </packageSources>
</configuration>
```

### Step 4: Restore from the Local Source

```bash
dotnet restore NeatSharp.sln
```

This will resolve all packages from the local feed without any network calls.

### Alternative: Copy the Global NuGet Cache

A simpler approach is to copy the entire global NuGet cache from the connected machine to the air-gapped machine:

1. On the connected machine, after `dotnet restore`, locate the cache:
   - Linux/macOS: `~/.nuget/packages`
   - Windows: `%USERPROFILE%\.nuget\packages`

2. Copy the entire `packages` directory to the same location on the air-gapped machine.

3. Run `dotnet build` -- it will find packages in the cache without network access.

## Verifying Offline Operation

To confirm that no network access occurs during normal operation:

```bash
# Build with verbosity to see package resolution
dotnet build NeatSharp.sln --verbosity detailed

# Run without any network access (e.g., disconnect or use firewall rules)
dotnet run --project samples/NeatSharp.Samples
```

If the build and run succeed without network access, your setup is fully offline-capable.

## Summary

| Operation                    | Network Required? | When?                                     |
|------------------------------|-------------------|-------------------------------------------|
| `dotnet restore`             | Yes               | First build or package version changes.   |
| `dotnet build`               | No*               | *Only if restore is needed and not cached. |
| `dotnet run`                 | No*               | *Only if restore is needed and not cached. |
| Evolution / Training         | No                | Never.                                    |
| GPU Evaluation               | No                | Never.                                    |
| Hybrid Evaluation            | No                | Never.                                    |
| Checkpointing (save/load)   | No                | Never.                                    |
| GPU Device Detection         | No                | Never.                                    |

## Further Reading

- [GPU Setup Guide](gpu-setup.md) -- GPU prerequisites and installation
- [Troubleshooting Guide](troubleshooting.md) -- resolving common issues
