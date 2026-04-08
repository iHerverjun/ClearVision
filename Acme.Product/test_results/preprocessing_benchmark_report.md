# Preprocessing Benchmark Report

Generated (UTC): 2026-04-08T13:17:49.5389910Z

| Operator | Resolution | Iterations | Avg (ms) | P95 (ms) | P99 (ms) | Max (ms) |
|---|---|---:|---:|---:|---:|---:|
| MedianBlur | 1920x1080 | 6 | 1.33 | 2.00 | 2.00 | 2.00 |
| AdaptiveThreshold | 1920x1080 | 6 | 8.50 | 10.00 | 10.00 | 10.00 |
| ClaheEnhancement | 1920x1080 | 6 | 11.50 | 14.00 | 14.00 | 14.00 |
| HistogramEqualization | 1920x1080 | 6 | 12.00 | 14.00 | 14.00 | 14.00 |
| BilateralFilter | 1920x1080 | 6 | 21.50 | 24.00 | 24.00 | 24.00 |
| ShadingCorrection | 1920x1080 | 6 | 26.50 | 29.00 | 29.00 | 29.00 |
| FrameAveraging | 1920x1080 | 6 | 92.83 | 99.00 | 99.00 | 99.00 |
| MedianBlur | 4096x3072 | 3 | 8.00 | 9.00 | 9.00 | 9.00 |
| AdaptiveThreshold | 4096x3072 | 3 | 51.33 | 52.00 | 52.00 | 52.00 |
| HistogramEqualization | 4096x3072 | 3 | 54.67 | 60.00 | 60.00 | 60.00 |
| ClaheEnhancement | 4096x3072 | 3 | 56.67 | 69.00 | 69.00 | 69.00 |
| BilateralFilter | 4096x3072 | 3 | 117.00 | 119.00 | 119.00 | 119.00 |
| ShadingCorrection | 4096x3072 | 3 | 145.00 | 161.00 | 161.00 | 161.00 |
| FrameAveraging | 4096x3072 | 3 | 409.00 | 496.00 | 496.00 | 496.00 |
| MedianBlur | native | 6 | 0.00 | 0.00 | 0.00 | 0.00 |
| AdaptiveThreshold | native | 6 | 0.00 | 0.00 | 0.00 | 0.00 |
| ClaheEnhancement | native | 6 | 1.17 | 2.00 | 2.00 | 2.00 |
| HistogramEqualization | native | 6 | 1.50 | 3.00 | 3.00 | 3.00 |
| ShadingCorrection | native | 6 | 2.67 | 3.00 | 3.00 | 3.00 |
| FrameAveraging | native | 6 | 5.50 | 6.00 | 6.00 | 6.00 |
| BilateralFilter | native | 6 | 15.00 | 23.00 | 23.00 | 23.00 |
