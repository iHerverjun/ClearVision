# 表面缺陷检测 / SurfaceDefectDetection

## 基本信息 / Basic Info
| 项目 | 值 |
|------|------|
| 类名 | `SurfaceDefectDetectionOperator` |
| 枚举值 | `OperatorType.SurfaceDefectDetection` |
| 分类 | `AI检测` |
| 版本 | `2.0.0` |
| 成熟度 | `Experimental` |
| 标签 | `experimental`, `industrial-remediation`, `surface-defect` |

## 算法说明 / Algorithm
该算子面向传统表面缺陷检测，当前实现支持三条路径：

1. `GradientMagnitude`
   先做局部背景归一化，再计算梯度响应图，适合划痕、边缘类缺陷增强。
2. `ReferenceDiff`
   对参考图做尺寸对齐和可选相位相关平移配准，再与当前图像做差分，适合有稳定基准图的缺陷比对。
3. `LocalContrast`
   通过局部均值背景扣除得到局部对比度响应，适合弱纹理表面上的局部异常。

本轮整改后，算子新增了配准、归一化、自适应阈值和诊断输出，目的是让阈值更稳定、现场更容易调试。

## 参数 / Parameters
| 名称 | 类型 | 默认值 | 说明 |
|------|------|------|------|
| `Method` | `enum` | `GradientMagnitude` | 缺陷响应模式 |
| `Threshold` | `double` | `35.0` | 手动阈值下限 |
| `MinArea` | `int` | `20` | 最小缺陷面积 |
| `MaxArea` | `int` | `1000000` | 最大缺陷面积 |
| `MorphCleanSize` | `int` | `3` | 形态学清理尺寸 |
| `AlignmentMode` | `enum` | `PhaseCorrelation` | 参考图配准模式，当前支持 `None` / `PhaseCorrelation` |
| `NormalizationMode` | `enum` | `LocalMean` | 响应前归一化模式，当前支持 `None` / `LocalMean` |
| `ThresholdMode` | `enum` | `Auto` | 阈值模式，当前支持 `Auto` / `Manual` / `Otsu` / `ReferenceStats` |
| `BackgroundKernelSize` | `int` | `31` | 局部背景核大小 |
| `ReferenceStatsSigma` | `double` | `2.5` | `ReferenceStats` 阈值的均值加权倍数 |

## 输入 / Outputs
### 输入 / Inputs
| 名称 | 类型 | 必填 | 说明 |
|------|------|------|------|
| `Image` | `Image` | 是 | 当前待检图像 |
| `Reference` | `Image` | 否 | 参考图，仅 `ReferenceDiff` 路径使用 |

### 输出 / Outputs
| 名称 | 类型 | 说明 |
|------|------|------|
| `Image` | `Image` | 标注后的结果图 |
| `DefectMask` | `Image` | 通过面积筛选后的缺陷掩膜 |
| `ResponseImage` | `Image` | 阈值前响应图，便于调参 |
| `DefectCount` | `Integer` | 缺陷数量 |
| `DefectArea` | `Float` | 缺陷总面积 |
| `AlignmentScore` | `Float` | 配准响应分数 |
| `RejectedReason` | `String` | 当前帧配准或筛选异常原因 |
| `Diagnostics` | `Any` | 方法、阈值、配准偏移、候选数等诊断信息 |

## 适用场景 / Use Cases
- 有稳定背景或参考图的表面差异检测
- 划痕、污点、异物、局部亮暗异常的初筛
- 作为传统检测链路中的可解释诊断节点

## 已知限制 / Known Limitations
1. 当前配准只做平移级别补偿，不适合明显旋转、透视变形或尺度漂移场景。
2. `GradientMagnitude` 和 `LocalContrast` 仍属于启发式方法，现场效果高度依赖光照、成像和 ROI 质量。
3. 要达到工业级误检/漏检控制，仍需要配套真实样本阈值验证，而不是仅依赖单帧调参。
