# Operator Benchmark Report

Generated (UTC): 2026-03-15T04:54:21.6356800Z

| Operator | Resolution | Iterations | Avg (ms) | P95 (ms) | P99 (ms) | Status |
|---|---:|---:|---:|---:|---:|---|
| Filtering | 1920x1080 | 8 | 1.50 | 7.00 | 7.00 | OK |
| Morphology | 1920x1080 | 8 | 2.00 | 4.00 | 4.00 | OK |
| Filtering | 4096x3072 | 5 | 2.80 | 3.00 | 3.00 | OK |
| Thresholding | 1920x1080 | 8 | 3.75 | 12.00 | 12.00 | OK |
| Thresholding | 4096x3072 | 5 | 4.00 | 4.00 | 4.00 | OK |
| EdgeDetection | 1920x1080 | 8 | 6.25 | 8.00 | 8.00 | OK |
| SharpnessEvaluation | 1920x1080 | 8 | 10.00 | 12.00 | 12.00 | OK |
| Morphology | 4096x3072 | 5 | 19.80 | 23.00 | 23.00 | OK |
| EdgeDetection | 4096x3072 | 5 | 27.20 | 32.00 | 32.00 | OK |
| BlobAnalysis | 1920x1080 | 8 | 32.38 | 36.00 | 36.00 | OK |
| SharpnessEvaluation | 4096x3072 | 5 | 46.40 | 55.00 | 55.00 | OK |
| BlobAnalysis | 4096x3072 | 5 | 110.00 | 119.00 | 119.00 | NeedOptimize |
