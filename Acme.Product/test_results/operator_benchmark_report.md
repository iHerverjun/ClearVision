# Operator Benchmark Report

Generated (UTC): 2026-03-04T07:46:42.1608559Z

| Operator | Resolution | Iterations | Avg (ms) | P95 (ms) | P99 (ms) | Status |
|---|---:|---:|---:|---:|---:|---|
| Morphology | 1920x1080 | 8 | 2.38 | 3.00 | 3.00 | OK |
| Thresholding | 1920x1080 | 8 | 3.50 | 7.00 | 7.00 | OK |
| Filtering | 4096x3072 | 5 | 6.60 | 8.00 | 8.00 | OK |
| Filtering | 1920x1080 | 8 | 7.62 | 15.00 | 15.00 | OK |
| EdgeDetection | 1920x1080 | 8 | 7.62 | 10.00 | 10.00 | OK |
| SharpnessEvaluation | 1920x1080 | 8 | 13.75 | 16.00 | 16.00 | OK |
| Thresholding | 4096x3072 | 5 | 20.20 | 68.00 | 68.00 | OK |
| Morphology | 4096x3072 | 5 | 26.40 | 32.00 | 32.00 | OK |
| EdgeDetection | 4096x3072 | 5 | 33.60 | 46.00 | 46.00 | OK |
| BlobAnalysis | 1920x1080 | 8 | 48.50 | 55.00 | 55.00 | OK |
| SharpnessEvaluation | 4096x3072 | 5 | 62.00 | 74.00 | 74.00 | OK |
| BlobAnalysis | 4096x3072 | 5 | 195.20 | 217.00 | 217.00 | NeedOptimize |
