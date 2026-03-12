# Operator Benchmark Report

Generated (UTC): 2026-03-12T02:30:30.1171350Z

| Operator | Resolution | Iterations | Avg (ms) | P95 (ms) | P99 (ms) | Status |
|---|---:|---:|---:|---:|---:|---|
| Morphology | 1920x1080 | 8 | 4.12 | 12.00 | 12.00 | OK |
| Filtering | 1920x1080 | 8 | 4.25 | 12.00 | 12.00 | OK |
| Filtering | 4096x3072 | 5 | 7.20 | 9.00 | 9.00 | OK |
| EdgeDetection | 1920x1080 | 8 | 7.25 | 8.00 | 8.00 | OK |
| SharpnessEvaluation | 1920x1080 | 8 | 11.50 | 15.00 | 15.00 | OK |
| Thresholding | 4096x3072 | 5 | 13.00 | 15.00 | 15.00 | OK |
| Thresholding | 1920x1080 | 8 | 13.75 | 35.00 | 35.00 | OK |
| Morphology | 4096x3072 | 5 | 22.80 | 28.00 | 28.00 | OK |
| BlobAnalysis | 1920x1080 | 8 | 39.75 | 50.00 | 50.00 | OK |
| EdgeDetection | 4096x3072 | 5 | 44.00 | 48.00 | 48.00 | OK |
| SharpnessEvaluation | 4096x3072 | 5 | 57.40 | 62.00 | 62.00 | OK |
| BlobAnalysis | 4096x3072 | 5 | 201.80 | 232.00 | 232.00 | NeedOptimize |
