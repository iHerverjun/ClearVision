# Operator Benchmark Report

Generated (UTC): 2026-02-26T09:31:38.6169981Z

| Operator | Resolution | Iterations | Avg (ms) | P95 (ms) | P99 (ms) | Status |
|---|---:|---:|---:|---:|---:|---|
| Thresholding | 1920x1080 | 8 | 2.12 | 3.00 | 3.00 | OK |
| Filtering | 1920x1080 | 8 | 2.25 | 10.00 | 10.00 | OK |
| Morphology | 1920x1080 | 8 | 2.25 | 5.00 | 5.00 | OK |
| Thresholding | 4096x3072 | 5 | 6.00 | 6.00 | 6.00 | OK |
| Filtering | 4096x3072 | 5 | 7.80 | 20.00 | 20.00 | OK |
| EdgeDetection | 1920x1080 | 8 | 8.25 | 19.00 | 19.00 | OK |
| SharpnessEvaluation | 1920x1080 | 8 | 12.25 | 16.00 | 16.00 | OK |
| Morphology | 4096x3072 | 5 | 26.00 | 29.00 | 29.00 | OK |
| EdgeDetection | 4096x3072 | 5 | 32.80 | 35.00 | 35.00 | OK |
| BlobAnalysis | 1920x1080 | 8 | 39.00 | 47.00 | 47.00 | OK |
| SharpnessEvaluation | 4096x3072 | 5 | 62.40 | 74.00 | 74.00 | OK |
| BlobAnalysis | 4096x3072 | 5 | 166.80 | 188.00 | 188.00 | NeedOptimize |
