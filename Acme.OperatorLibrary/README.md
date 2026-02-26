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

2. Generated package output:

- `./nupkg/Acme.OperatorLibrary.1.0.2-local.nupkg`

3. Add local source in another project:

```xml
<packageSources>
  <add key="local-operator-library" value="path/to/Acme.OperatorLibrary/nupkg" />
</packageSources>
```

4. Reference package:

```xml
<PackageReference Include="Acme.OperatorLibrary" Version="1.0.2-local" />
```

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

The package now exposes module-level namespace indexes:

- `Acme.OperatorLibrary.ImageProcessing`
- `Acme.OperatorLibrary.Measurement`
- `Acme.OperatorLibrary.Calibration`
- `Acme.OperatorLibrary.Communication`
- `Acme.OperatorLibrary.FlowControl`
- `Acme.OperatorLibrary.AI`

Use `Operators.Types` in each namespace to get the grouped `OperatorType` list.
