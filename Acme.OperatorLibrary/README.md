# Acme.OperatorLibrary

Industrial Vision Operator Library for ClearVision.

## Overview

`Acme.OperatorLibrary` packages the operator implementation layer as a standalone NuGet package while keeping source files in place under `Acme.Product/src/Acme.Product.Infrastructure`.

- Source sharing strategy: MSBuild linked compile items (`<Compile Include=... Link=... />`)
- Package model: single package (`Acme.OperatorLibrary`)
- Scope: image processing, measurement, calibration, communication, flow-control, AI

## Quick Start

1. Pack locally:

```powershell
./pack.ps1
```

2. Generated package output (default):

- `./nupkg/Acme.OperatorLibrary.1.0.2.nupkg`

3. Add local source in another project:

```xml
<packageSources>
  <add key="local-operator-library" value="path/to/Acme.OperatorLibrary/nupkg" />
</packageSources>
```

4. Reference package:

```xml
<PackageReference Include="Acme.OperatorLibrary" Version="1.0.2" />
```

## Industrial Acceptance Scope

Package acceptance tests are not limited to smoke instantiation. The baseline now includes representative operators across all major modules:

- ImageProcessing: mean filter runtime boundary path (kernel clamp + image output contract)
- Measurement: caliper success path and expected-count failure path
- Calibration: parameter validation and missing folder failure path
- Communication: Modbus validation boundary and RTU fail-fast path
- FlowControl: TryCatch passthrough contract
- AI: DeepLearning missing model validation/runtime failure path

Acceptance criteria: each representative operator must cover at least one normal path plus parameter, exception, or boundary behavior.

## Packaging Version & Traceability

The package no longer uses the fixed `*-local` version strategy.

- Default local version: `VersionPrefix` (`1.0.2` currently)
- CI version injection: pass `PackageVersion` (for example `1.0.2-ci.20260419.1`)
- Reproducibility metadata: `SourceRevisionId`, `RepositoryCommit`, `RepositoryBranch`, `PublishRepositoryUrl`, deterministic/CI build flags
- Symbols: `.snupkg` is still generated for debugging compatibility

`pack.ps1` supports explicit metadata injection:

```powershell
./pack.ps1 `
  -PackageVersion "1.0.2-ci.20260419.1" `
  -SourceRevisionId "a1b2c3d4" `
  -RepositoryBranch "main" `
  -RepositoryCommit "a1b2c3d4" `
  -RunSmokeTest
```

It also reads common CI environment variables (`ACME_OPERATORLIB_PACKAGE_VERSION`, `GITHUB_SHA`, `GITHUB_REF_NAME`, `BUILD_SOURCEVERSION`, `BUILD_SOURCEBRANCHNAME`) when parameters are omitted.

## Notes

- This project is intentionally isolated from `Acme.Product.sln` default build graph.
- It does not change runtime behavior of the ClearVision main application.

## Phase 3.3 Compatibility Work

- Build profile constant: `ACME_OPERATORLIB_PACKAGE`
- Host-agnostic contracts/models: `Acme.OperatorLibrary/src/Acme.OperatorLibrary.Abstractions/*`
- Core adapters (guarded by `#if ACME_OPERATORLIB_PACKAGE`): `Acme.OperatorLibrary/src/Acme.OperatorLibrary.Abstractions/Adapters/CoreTypeAdapters.cs`
- Dependency analysis script:

```powershell
./analyze-deps.ps1
```

- Generated reports:
  - `./analysis/dependency-report.md`
  - `./analysis/dependency-report.json`

## Phase 3.4 Module Namespaces

The package exposes module-level namespace indexes:

- `Acme.OperatorLibrary.ImageProcessing`
- `Acme.OperatorLibrary.Measurement`
- `Acme.OperatorLibrary.Calibration`
- `Acme.OperatorLibrary.Communication`
- `Acme.OperatorLibrary.FlowControl`
- `Acme.OperatorLibrary.AI`

Use `Operators.Types` in each namespace to get grouped `OperatorType` lists.
