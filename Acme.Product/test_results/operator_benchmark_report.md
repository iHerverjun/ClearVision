# Operator Benchmark Report

Generated (UTC): 2026-04-08T01:49:28.8777396Z

| Operator | Resolution | Iterations | Avg (ms) | P95 (ms) | P99 (ms) | Status |
|---|---:|---:|---:|---:|---:|---|
| Filtering | 1920x1080 | 8 | 8.38 | 19.00 | 19.00 | OK |
| Filtering | 4096x3072 | 5 | 11.00 | 14.00 | 14.00 | OK |
| Morphology | 1920x1080 | 8 | 15.50 | 55.00 | 55.00 | OK |
| Thresholding | 1920x1080 | 8 | 16.00 | 34.00 | 34.00 | OK |
| BlobAnalysis | 1920x1080 | 8 | 24.38 | 29.00 | 29.00 | OK |
| SharpnessEvaluation | 1920x1080 | 8 | 30.88 | 53.00 | 53.00 | OK |
| Thresholding | 4096x3072 | 5 | 32.80 | 41.00 | 41.00 | OK |
| Morphology | 4096x3072 | 5 | 33.40 | 39.00 | 39.00 | OK |
| EdgeDetection | 1920x1080 | 8 | 35.75 | 68.00 | 68.00 | OK |
| EdgeDetection | 4096x3072 | 5 | 59.80 | 70.00 | 70.00 | OK |
| SharpnessEvaluation | 4096x3072 | 5 | 93.00 | 105.00 | 105.00 | OK |
| BlobAnalysis | 4096x3072 | 5 | 110.40 | 210.00 | 210.00 | NeedOptimize |
