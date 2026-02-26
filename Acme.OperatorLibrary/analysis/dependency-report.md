# Operator Dependency Report

> GeneratedAt: `2026-02-26T20:32:17.4600395+08:00`
> OperatorFiles: **123**

## Key Host Dependency Types
| Type | Category | MatchCount | FileCount | Recommendation |
|------|----------|-----------:|----------:|---------------|
| `OperatorBase` | Infrastructure | 118 | 116 | Keep a lightweight base class in package layer. |
| `ImageWrapper` | Infrastructure | 255 | 74 | Keep image abstraction stable; isolate memory strategy by profile. |
| `OperatorExecutionOutput` | Core | 547 | 120 | Expose host-agnostic execution result model for package consumers. |
| `OperatorMetadata` | Core | 0 | 0 | Expose metadata DTO/contract and adapter. |
| `PortDefinition` | Core | 0 | 0 | Expose port DTO/contract and adapter. |
| `Operator` | Core | 265 | 119 | Wrap runtime operator entity behind request contract if host isolation is required. |

## Namespace Usage (Top 20)
| Namespace | RefCount |
|-----------|---------:|
| `Acme.Product.Core.Entities` | 121 |
| `Acme.Product.Core.Enums` | 121 |
| `Acme.Product.Core.Operators` | 121 |
| `Acme.Product.Core.Attributes` | 118 |
| `Acme.Product.Core.ValueObjects` | 30 |
| `Acme.Product.Core.Services` | 6 |
| `Acme.Product.Infrastructure.Memory` | 3 |
| `Acme.Product.Infrastructure.ImageProcessing` | 2 |
| `Acme.Product.Infrastructure.Services` | 2 |
| `Acme.Product.Core.Cameras` | 1 |
| `Acme.Product.Infrastructure.Operators` | 1 |

## Notes
- This report is generated from `Acme.Product.Infrastructure/Operators/*.cs`.
- MatchCount is text-pattern based and intended for migration prioritization, not semantic compilation truth.
- For Phase 3.3, use this report together with abstraction adapters under `Acme.OperatorLibrary/src`.

