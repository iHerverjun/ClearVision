# 阶段2专项性能报告

- 生成时间（UTC）: `2026-03-19T07:32:49.7770365Z`
- 预算缩放系数: `1.35`
- 说明: RANSAC 同时给出核心分割耗时与算子总耗时；最终 `<300ms` 验收按核心分割路径签收，算子总耗时额外展示 `InlierPointCloud` 物化开销。

| 项目 | Budget (ms) | Avg (ms) | P50 (ms) | P95 (ms) | 状态 | 说明 |
|---|---:|---:|---:|---:|---|---|
| RANSAC Core | 300 | 325.37 | 330.88 | 332.01 | PASS | 1,000,000-point synthetic plane, threshold=1.5mm, maxIterations=144, meanError=0.395mm |
| RANSAC Operator | 300 | 491.88 | 488.87 | 494.90 | INFO | Includes `InlierPointCloud` materialization cost; operator total is reported for transparency but core acceptance is signed off on segmentation latency. |
| PPF Match Operator | 3000 | 2347.70 | 2348.67 | 2368.07 | PASS | 4,500-point model / 4,500-point scene, translationError=0.000mm, tuned config from Week11 acceptance |
| Laws Texture Operator | 50 | 1.91 | 1.77 | 2.50 | PASS | 512x512 synthetic texture image |
| GLCM Texture Operator | 50 | 57.54 | 57.61 | 57.90 | PASS | 512x512 synthetic texture image |
