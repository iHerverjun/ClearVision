# Operator Benchmark Report

Generated (UTC): 2026-03-03T13:01:03.0715926Z

| Operator | Resolution | Iterations | Avg (ms) | P95 (ms) | P99 (ms) | Status |
|---|---:|---:|---:|---:|---:|---|
| Filtering | 1920x1080 | 8 | 0.75 | 1.00 | 1.00 | OK |
| Thresholding | 1920x1080 | 8 | 1.75 | 2.00 | 2.00 | OK |
| Morphology | 1920x1080 | 8 | 2.50 | 3.00 | 3.00 | OK |
| Filtering | 4096x3072 | 5 | 4.00 | 4.00 | 4.00 | OK |
| Thresholding | 4096x3072 | 5 | 6.20 | 7.00 | 7.00 | OK |
| EdgeDetection | 1920x1080 | 8 | 7.38 | 8.00 | 8.00 | OK |
| SharpnessEvaluation | 1920x1080 | 8 | 9.50 | 10.00 | 10.00 | OK |
| Morphology | 4096x3072 | 5 | 13.00 | 14.00 | 14.00 | OK |
| BlobAnalysis | 1920x1080 | 8 | 27.25 | 29.00 | 29.00 | OK |
| EdgeDetection | 4096x3072 | 5 | 37.40 | 42.00 | 42.00 | OK |
| SharpnessEvaluation | 4096x3072 | 5 | 48.00 | 50.00 | 50.00 | OK |
| BlobAnalysis | 4096x3072 | 5 | 113.80 | 121.00 | 121.00 | NeedOptimize |
