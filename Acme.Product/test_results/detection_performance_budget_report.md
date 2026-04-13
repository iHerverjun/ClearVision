# Detection Performance Budget Report

Generated (UTC): 2026-04-13T13:43:05.7540661Z
Gate Profile: acceptance
Warmup Iterations: 5
Measured Iterations: 24
Budget Scale: 1.20

| Operator | Budget (ms) | Scale | Allowed P95 (ms) | Mean (ms) | P95 (ms) | P99 (ms) | Status | Notes |
|---|---:|---:|---:|---:|---:|---:|---|---|
| AngleMeasurement | 10 | 1.20 | 12 | 0.38 | 0.77 | 0.88 | PASS | Within budget. |
| CaliperTool | 50 | 1.20 | 60 | 0.24 | 0.39 | 0.42 | PASS | Within budget. |
| CircleMeasurement | 30 | 1.20 | 36 | 5.30 | 5.89 | 6.49 | PASS | Within budget. |
| ContourMeasurement | 40 | 1.20 | 48 | 0.58 | 0.83 | 0.85 | PASS | Within budget. |
| GapMeasurement | 30 | 1.20 | 36 | 1.17 | 1.36 | 6.45 | PASS | Within budget. |
| GeoMeasurement | 20 | 1.20 | 24 | 0.01 | 0.02 | 0.07 | PASS | Within budget. |
| GeometricTolerance | 20 | 1.20 | 24 | 0.12 | 0.16 | 0.16 | PASS | Within budget. |
| HistogramAnalysis | 10 | 1.20 | 12 | 0.74 | 0.85 | 1.00 | PASS | Within budget. |
| LineLineDistance | 10 | 1.20 | 12 | 0.00 | 0.00 | 0.01 | PASS | Within budget. |
| LineMeasurement | 20 | 1.20 | 24 | 5.13 | 5.41 | 7.27 | PASS | Within budget. |
| MeasureDistance | 10 | 1.20 | 12 | 0.60 | 0.76 | 0.78 | PASS | Within budget. |
| PixelStatistics | 10 | 1.20 | 12 | 0.56 | 0.72 | 0.78 | PASS | Within budget. |
| PointLineDistance | 10 | 1.20 | 12 | 0.00 | 0.00 | 0.01 | PASS | Within budget. |
| SharpnessEvaluation | 15 | 1.20 | 18 | 1.65 | 2.22 | 2.27 | PASS | Within budget. |
| WidthMeasurement | 30 | 1.20 | 36 | 1.02 | 1.48 | 1.50 | PASS | Within budget. |
