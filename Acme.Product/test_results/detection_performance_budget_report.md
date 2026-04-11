# Detection Performance Budget Report

Generated (UTC): 2026-04-11T16:26:42.1929878Z
Gate Profile: standard
Warmup Iterations: 5
Measured Iterations: 24
Budget Scale: 1.50

| Operator | Budget (ms) | Scale | Allowed P95 (ms) | Mean (ms) | P95 (ms) | P99 (ms) | Status | Notes |
|---|---:|---:|---:|---:|---:|---:|---|---|
| AngleMeasurement | 10 | 1.50 | 15 | 0.34 | 0.64 | 0.69 | PASS | Within budget. |
| CaliperTool | 50 | 1.50 | 75 | 0.18 | 0.26 | 0.27 | PASS | Within budget. |
| CircleMeasurement | 30 | 1.50 | 45 | 5.17 | 5.63 | 5.76 | PASS | Within budget. |
| ContourMeasurement | 40 | 1.50 | 60 | 0.56 | 0.74 | 0.75 | PASS | Within budget. |
| GapMeasurement | 30 | 1.50 | 45 | 0.87 | 1.06 | 1.09 | PASS | Within budget. |
| GeoMeasurement | 20 | 1.50 | 30 | 0.01 | 0.01 | 0.05 | PASS | Within budget. |
| GeometricTolerance | 20 | 1.50 | 30 | 0.32 | 0.46 | 0.55 | PASS | Within budget. |
| HistogramAnalysis | 10 | 1.50 | 15 | 0.66 | 0.72 | 0.80 | PASS | Within budget. |
| LineLineDistance | 10 | 1.50 | 15 | 0.00 | 0.00 | 0.01 | PASS | Within budget. |
| LineMeasurement | 20 | 1.50 | 30 | 1.35 | 1.59 | 1.74 | PASS | Within budget. |
| MeasureDistance | 10 | 1.50 | 15 | 0.68 | 0.92 | 1.01 | PASS | Within budget. |
| PixelStatistics | 10 | 1.50 | 15 | 0.52 | 0.64 | 0.65 | PASS | Within budget. |
| PointLineDistance | 10 | 1.50 | 15 | 0.00 | 0.00 | 0.00 | PASS | Within budget. |
| SharpnessEvaluation | 15 | 1.50 | 22.5 | 1.11 | 1.24 | 3.24 | PASS | Within budget. |
| WidthMeasurement | 30 | 1.50 | 45 | 0.78 | 0.89 | 0.94 | PASS | Within budget. |
