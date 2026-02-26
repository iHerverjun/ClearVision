# Operator Benchmark Report

Generated (UTC): 2026-02-26T02:13:56.4607546Z

| Operator | Resolution | Iterations | Avg (ms) | P95 (ms) | P99 (ms) | Status |
|---|---:|---:|---:|---:|---:|---|
| Filtering | 1920x1080 | 8 | 1.00 | 1.00 | 1.00 | OK |
| Thresholding | 1920x1080 | 8 | 1.75 | 2.00 | 2.00 | OK |
| Morphology | 1920x1080 | 8 | 1.88 | 3.00 | 3.00 | OK |
| Filtering | 4096x3072 | 5 | 4.40 | 5.00 | 5.00 | OK |
| EdgeDetection | 1920x1080 | 8 | 7.62 | 9.00 | 9.00 | OK |
| SharpnessEvaluation | 1920x1080 | 8 | 12.62 | 14.00 | 14.00 | OK |
| Thresholding | 4096x3072 | 5 | 20.60 | 73.00 | 73.00 | OK |
| Morphology | 4096x3072 | 5 | 21.00 | 34.00 | 34.00 | OK |
| EdgeDetection | 4096x3072 | 5 | 32.40 | 47.00 | 47.00 | OK |
| BlobAnalysis | 1920x1080 | 8 | 40.12 | 48.00 | 48.00 | OK |
| SharpnessEvaluation | 4096x3072 | 5 | 63.00 | 82.00 | 82.00 | OK |
| BlobAnalysis | 4096x3072 | 5 | 169.80 | 206.00 | 206.00 | NeedOptimize |
