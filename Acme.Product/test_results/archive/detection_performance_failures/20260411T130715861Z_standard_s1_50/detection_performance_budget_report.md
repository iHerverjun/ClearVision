# Detection Performance Budget Report

Generated (UTC): 2026-04-11T13:07:15.8613895Z
Gate Profile: standard
Warmup Iterations: 5
Measured Iterations: 24
Budget Scale: 1.50

| Operator | Budget (ms) | Scale | Allowed P95 (ms) | Mean (ms) | P95 (ms) | P99 (ms) | Status | Notes |
|---|---:|---:|---:|---:|---:|---:|---|---|
| AngleMeasurement | 10 | 1.50 | 15 | 0.00 | 0.00 | 0.00 | ERROR | Execution failed: [ImageWrapper] 引用计数为负，检测到双重释放 Bug |
| CaliperTool | 50 | 1.50 | 75 | 0.00 | 0.00 | 0.00 | ERROR | Execution failed: [ImageWrapper] 引用计数为负，检测到双重释放 Bug |
| CircleMeasurement | 30 | 1.50 | 45 | 0.00 | 0.00 | 0.00 | ERROR | Execution failed: [ImageWrapper] 引用计数为负，检测到双重释放 Bug |
| ContourMeasurement | 40 | 1.50 | 60 | 0.00 | 0.00 | 0.00 | ERROR | Execution failed: [ImageWrapper] 引用计数为负，检测到双重释放 Bug |
| GapMeasurement | 30 | 1.50 | 45 | 0.00 | 0.00 | 0.00 | ERROR | Execution failed: [ImageWrapper] 引用计数为负，检测到双重释放 Bug |
| GeoMeasurement | 20 | 1.50 | 30 | 0.01 | 0.02 | 0.02 | PASS | Within budget. |
| GeometricTolerance | 20 | 1.50 | 30 | 0.00 | 0.00 | 0.00 | ERROR | Execution failed: [ImageWrapper] 引用计数为负，检测到双重释放 Bug |
| HistogramAnalysis | 10 | 1.50 | 15 | 0.00 | 0.00 | 0.00 | ERROR | Execution failed: [ImageWrapper] 引用计数为负，检测到双重释放 Bug |
| LineLineDistance | 10 | 1.50 | 15 | 0.00 | 0.00 | 0.01 | PASS | Within budget. |
| LineMeasurement | 20 | 1.50 | 30 | 0.00 | 0.00 | 0.00 | ERROR | Execution failed: [ImageWrapper] 引用计数为负，检测到双重释放 Bug |
| MeasureDistance | 10 | 1.50 | 15 | 0.00 | 0.00 | 0.00 | ERROR | Execution failed: [ImageWrapper] 引用计数为负，检测到双重释放 Bug |
| PixelStatistics | 10 | 1.50 | 15 | 0.00 | 0.00 | 0.00 | ERROR | Execution failed: [ImageWrapper] 引用计数为负，检测到双重释放 Bug |
| PointLineDistance | 10 | 1.50 | 15 | 0.00 | 0.01 | 0.01 | PASS | Within budget. |
| SharpnessEvaluation | 15 | 1.50 | 22.5 | 0.00 | 0.00 | 0.00 | ERROR | Execution failed: [ImageWrapper] 引用计数为负，检测到双重释放 Bug |
| WidthMeasurement | 30 | 1.50 | 45 | 0.00 | 0.00 | 0.00 | ERROR | Execution failed: [ImageWrapper] 引用计数为负，检测到双重释放 Bug |
