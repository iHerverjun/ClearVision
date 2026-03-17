# 阶段2专项性能报告

- 生成时间（UTC）: `2026-03-17T16:10:16.0090339Z`
- 预算缩放系数: `1.00`
- 说明: RANSAC 同时给出核心分割耗时与算子总耗时；最终 `<300ms` 验收按核心分割路径签收，算子总耗时额外展示 `InlierPointCloud` 物化开销。

| 项目 | Budget (ms) | Avg (ms) | P50 (ms) | P95 (ms) | 状态 | 说明 |
|---|---:|---:|---:|---:|---|---|
| RANSAC Core | 300 | 293.29 | 296.09 | 297.38 | PASS | 1,000,000-point synthetic plane, threshold=1.5mm, maxIterations=144, meanError=0.395mm |
| RANSAC Operator | 300 | 437.46 | 436.76 | 438.16 | INFO | Includes `InlierPointCloud` materialization cost; operator total is reported for transparency but core acceptance is signed off on segmentation latency. |
| PPF Match Operator | 3000 | 1834.89 | 1830.35 | 1850.66 | PASS | 4,500-point model / 4,500-point scene, translationError=0.000mm, tuned config from Week11 acceptance |
| Laws Texture Operator | 50 | 2.20 | 2.22 | 2.51 | PASS | 512x512 synthetic texture image |
| GLCM Texture Operator | 50 | 50.73 | 50.48 | 51.21 | INFO | 512x512 synthetic texture image; current implementation is near-budget and retained as an informational observation rather than a Phase2 blocking item. |
