# 异常检测 / AnomalyDetection

## 基本信息 / Basic Info
| 项目 | 值 |
|------|------|
| 类名 | `AnomalyDetectionOperator` |
| 枚举值 | `OperatorType.AnomalyDetection` |
| 分类 | `AI检测` |
| 版本 | `1.0.0` |
| 成熟度 | `Experimental` |
| 标签 | `experimental`, `industrial-remediation`, `anomaly-detection` |

## 算法说明 / Algorithm
当前实现是简化版 PatchCore 思路：

1. 在训练模式下，从 `NormalImages` 提取局部 patch 特征并构建 feature bank；
2. 在推理模式下，计算待测图像 patch 与 feature bank 的最近邻距离；
3. 输出异常分数、热力图和二值掩膜。

本轮整改后，算子补上了结构化诊断输出，并为后续真实 embedding 路线预留了参数入口，但当前默认特征提取仍是轻量级 `lab_gradient_stats`。

## 参数 / Parameters
| 名称 | 类型 | 默认值 | 说明 |
|------|------|------|------|
| `Mode` | `enum` | `inference` | `train` 或 `inference` |
| `FeatureBankPath` | `file` | `""` | 推理时显式特征库路径 |
| `SaveFeatureBankPath` | `file` | `""` | 训练后特征库存储路径 |
| `ModelId` | `string` | `""` | 通过模型目录解析特征库 |
| `ModelCatalogPath` | `file` | `""` | 模型目录路径 |
| `Backbone` | `string` | `simple_patchcore` | 当前仅支持 `simple_patchcore` |
| `FeatureExtractorId` | `string` | `lab_gradient_stats` | 当前特征提取器标识 |
| `EmbeddingModelId` | `string` | `""` | 后续 embedding 模型入口 |
| `EmbeddingModelPath` | `file` | `""` | 后续 embedding 模型路径 |
| `PatchSize` | `int` | `32` | patch 大小 |
| `PatchStride` | `int` | `16` | patch 步长 |
| `CoresetRatio` | `double` | `0.2` | 特征库采样比例 |
| `Threshold` | `double` | `0.35` | 异常判定阈值 |

## 输入 / 输出 / Inputs & Outputs
### 输入 / Inputs
| 名称 | 类型 | 必填 | 说明 |
|------|------|------|------|
| `Image` | `Image` | 否 | 推理图像，训练模式下也可作为预览图 |
| `NormalImages` | `Any` | 否 | 训练模式下的正常样本集合 |

### 输出 / Outputs
| 名称 | 类型 | 说明 |
|------|------|------|
| `AnomalyScore` | `Float` | 最大异常分数 |
| `IsAnomaly` | `Boolean` | 是否判为异常 |
| `AnomalyMap` | `Image` | 热力图 |
| `AnomalyMask` | `Image` | 二值掩膜 |
| `FeatureBankPath` | `String` | 实际使用的特征库路径 |
| `PatchCount` | `Integer` | patch 数量 |
| `ThresholdUsed` | `Float` | 实际阈值 |
| `Diagnostics` | `Any` | 特征库来源、特征 schema、训练样本数等诊断信息 |

## 适用场景 / Use Cases
- 缺陷样本很少、正常样本较容易采集的表面异常检测
- 需要热力图辅助人工分析的异常筛查链路
- 作为后续 embedding anomaly 的兼容基线实现

## 已知限制 / Known Limitations
1. 当前默认特征仍是统计型 patch 特征，不是深度 embedding，能力上限有限。
2. 推理复杂度随 feature bank 规模增长，适合中小规模样本库。
3. 若要达到更高精度或跨批次鲁棒性，建议后续切换到真正的 ONNX embedding 路线。
