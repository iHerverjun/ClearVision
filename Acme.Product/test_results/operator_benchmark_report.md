# Operator Benchmark Report

Generated (UTC): 2026-02-25T11:54:49.6401190Z

| Operator | Resolution | Iterations | Avg (ms) | P95 (ms) | P99 (ms) | Status |
|---|---:|---:|---:|---:|---:|---|
| Filtering | 1920x1080 | 8 | 0.25 | 1.00 | 1.00 | OK |
| Morphology | 1920x1080 | 8 | 1.62 | 2.00 | 2.00 | OK |
| Thresholding | 1920x1080 | 8 | 1.88 | 2.00 | 2.00 | OK |
| Filtering | 4096x3072 | 5 | 4.20 | 5.00 | 5.00 | OK |
| Thresholding | 4096x3072 | 5 | 5.80 | 6.00 | 6.00 | OK |
| EdgeDetection | 1920x1080 | 8 | 7.75 | 10.00 | 10.00 | OK |
| SharpnessEvaluation | 1920x1080 | 8 | 9.00 | 10.00 | 10.00 | OK |
| Morphology | 4096x3072 | 5 | 11.40 | 12.00 | 12.00 | OK |
| BlobAnalysis | 1920x1080 | 8 | 25.62 | 28.00 | 28.00 | OK |
| EdgeDetection | 4096x3072 | 5 | 31.20 | 41.00 | 41.00 | OK |
| SharpnessEvaluation | 4096x3072 | 5 | 49.40 | 54.00 | 54.00 | OK |
| BlobAnalysis | 4096x3072 | 5 | 110.80 | 123.00 | 123.00 | NeedOptimize |
