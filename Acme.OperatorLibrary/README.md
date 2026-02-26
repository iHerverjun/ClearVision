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

- `./nupkg/Acme.OperatorLibrary.1.0.1-local.nupkg`

3. Add local source in another project:

```xml
<packageSources>
  <add key="local-operator-library" value="path/to/Acme.OperatorLibrary/nupkg" />
</packageSources>
```

4. Reference package:

```xml
<PackageReference Include="Acme.OperatorLibrary" Version="1.0.1-local" />
```

## Notes

- This project is intentionally isolated from `Acme.Product.sln` default build graph.
- It does not change runtime behavior of the ClearVision main application.
