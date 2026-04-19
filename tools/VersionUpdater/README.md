# ArchiTEK Dependency Version Updater

A C# rewrite of `versions.sh` with enhanced build metrics and detailed output.

## Overview

This .NET console application fetches the latest versions of the Docker engine,
[buildx](https://github.com/docker/buildx), and
[docker-compose](https://github.com/docker/compose), probes every supported
architecture for binary availability, and writes an updated `versions.json` at
the repository root.

It is a drop-in equivalent of the original `versions.sh` + `update.sh` pipeline,
with these additions:

| Feature | Shell (`versions.sh`) | C# (`VersionUpdater`) |
|---|---|---|
| Per-component fetch timing | ✗ | ✅ |
| Dependency change diff | ✗ | ✅ |
| HTTP request health counters | ✗ | ✅ |
| Architecture coverage summary | partial | ✅ |
| ANSI-coloured terminal output | ✗ | ✅ |
| Cancellable via Ctrl-C | ✗ | ✅ |
| Parallel architecture probes | ✗ | ✅ |

## Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download) or later
- `git` on `PATH` (used for `ls-remote` calls)

## Running

```bash
# From the repository root – updates all version keys:
dotnet run --project tools/VersionUpdater

# Update specific version keys only:
dotnet run --project tools/VersionUpdater -- 29 29-rc
```

After the run, `versions.json` is updated in-place (same as `./versions.sh`).
Run `./apply-templates.sh` afterwards to regenerate the Dockerfiles.

## Build metrics output

At the end of every successful run the tool prints a structured metrics block:

```
╔══ ArchiTEK Dependency Update – Build Metrics ══╗
────────────────────────────────────────────────────────
⏱  Timing breakdown
  dind commit fetch        :    1.23s
  Docker versions fetch    :    2.10s
  Buildx fetch             :    0.87s
  Compose fetch            :    0.91s
  Arch URL probes          :    3.45s
  Total elapsed            :    8.56s
────────────────────────────────────────────────────────
📦  Dependency versions
  Docker  : 29.3.0 → 29.3.1  ★ updated
  Buildx  : 0.32.1  (unchanged)
  Compose : 5.1.0   (unchanged)
  dind commit: 8d9e3502… → a1b2c3d4…  ★ updated
────────────────────────────────────────────────────────
🏗️  Architecture coverage
  Probed   : 8
  Available : 8 – amd64, arm32v6, arm32v7, arm64v8, …
────────────────────────────────────────────────────────
🌐  HTTP requests
  Total   : 34
  Success : 31
  404/Err : 3
────────────────────────────────────────────────────────
📄  versions.json summary
  Versions processed : 1
  Versions skipped   : 1
  Variants generated : 5
────────────────────────────────────────────────────────
✅  Status: UPDATED (run finished at 2026-04-18 14:45:00Z)
```

## Project structure

```
tools/VersionUpdater/
├── Program.cs                        # Entry point & orchestration
├── VersionUpdater.csproj
├── Models/
│   ├── BuildMetrics.cs               # Timing & change tracking model
│   └── VersionModels.cs              # versions.json data model
└── Services/
    ├── BuildMetricsReporter.cs       # ANSI metrics reporter
    ├── VersionFetcherService.cs      # HTTP + git ls-remote fetching
    └── VersionStringComparer.cs      # Semantic version sorting
```
