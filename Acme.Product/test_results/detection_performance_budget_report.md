# Detection Performance Budget Report

Generated (UTC): 2026-04-11T14:00:44.9659402Z
Gate Profile: standard
Warmup Iterations: 5
Measured Iterations: 24
Budget Scale: 1.50

| Operator | Budget (ms) | Scale | Allowed P95 (ms) | Mean (ms) | P95 (ms) | P99 (ms) | Status | Notes |
|---|---:|---:|---:|---:|---:|---:|---|---|
| AngleMeasurement | 10 | 1.50 | 15 | 0.38 | 0.83 | 0.87 | PASS | Within budget. |
| CaliperTool | 50 | 1.50 | 75 | 0.23 | 0.39 | 0.40 | PASS | Within budget. |
| CircleMeasurement | 30 | 1.50 | 45 | 6.19 | 6.85 | 6.92 | PASS | Within budget. |
| ContourMeasurement | 40 | 1.50 | 60 | 0.58 | 0.72 | 0.73 | PASS | Within budget. |
| GapMeasurement | 30 | 1.50 | 45 | 0.97 | 1.16 | 1.21 | PASS | Within budget. |
| GeoMeasurement | 20 | 1.50 | 30 | 0.01 | 0.01 | 0.01 | PASS | Within budget. |
| GeometricTolerance | 20 | 1.50 | 30 | 0.22 | 0.26 | 0.54 | PASS | Within budget. |
| HistogramAnalysis | 10 | 1.50 | 15 | 0.78 | 0.94 | 0.96 | PASS | Within budget. |
| LineLineDistance | 10 | 1.50 | 15 | 0.00 | 0.00 | 0.01 | PASS | Within budget. |
| LineMeasurement | 20 | 1.50 | 30 | 1.81 | 2.07 | 2.13 | PASS | Within budget. |
| MeasureDistance | 10 | 1.50 | 15 | 0.65 | 0.81 | 0.86 | PASS | Within budget. |
| PixelStatistics | 10 | 1.50 | 15 | 0.60 | 0.73 | 0.84 | PASS | Within budget. |
| PointLineDistance | 10 | 1.50 | 15 | 0.00 | 0.00 | 0.01 | PASS | Within budget. |
| SharpnessEvaluation | 15 | 1.50 | 22.5 | 1.42 | 1.76 | 1.83 | PASS | Within budget. |
| WidthMeasurement | 30 | 1.50 | 45 | 1.06 | 1.46 | 1.65 | PASS | Within budget. |
