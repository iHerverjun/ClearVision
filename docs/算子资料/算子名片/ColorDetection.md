# 颜色检测 / ColorDetection

## 基本信息 / Basic Info
| 项目 | 值 |
|------|------|
| 类名 | `ColorDetectionOperator` |
| 枚举值 | `OperatorType.ColorDetection` |
| 分类 | `颜色处理` |
| 版本 | `2.0.0` |
| 成熟度 | `Experimental` |
| 标签 | `experimental`, `industrial-remediation`, `color-inspection` |

## 算法说明 / Algorithm
该算子已从原来的“平均色/主色/范围检测”扩展为兼容型颜色检测节点，当前支持两类更适合工业场景的模式：

1. `HsvInspection`
   针对 HSV 阈值检测，支持 hue 环绕区间，例如红色的 `170 -> 10`。
2. `LabDeltaE`
   先计算 ROI 的平均 Lab 颜色，再基于 `CIE76` 或 `CIEDE2000` 与参考色比较，适合做颜色偏差量化。

同时保留 `Average`、`Dominant`、`Range` 三个旧模式，保证现有流程不被直接打断。

## 参数 / Parameters
| 名称 | 类型 | 默认值 | 说明 |
|------|------|------|------|
| `ColorSpace` | `enum` | `HSV` | 兼容模式下的颜色空间 |
| `AnalysisMode` | `enum` | `Average` | 运行模式，支持 `Average` / `Dominant` / `Range` / `HsvInspection` / `LabDeltaE` |
| `HueLow` | `int` | `0` | HSV 检测下限 |
| `HueHigh` | `int` | `180` | HSV 检测上限，可与 `HueLow` 构成环绕区间 |
| `SatLow` | `int` | `50` | 饱和度下限 |
| `SatHigh` | `int` | `255` | 饱和度上限 |
| `ValLow` | `int` | `50` | 明度下限 |
| `ValHigh` | `int` | `255` | 明度上限 |
| `DominantK` | `int` | `3` | 主色聚类数量 |
| `DeltaEMethod` | `enum` | `CIEDE2000` | 色差算法 |
| `RefL` | `double` | `0.0` | 参考 Lab 的 L |
| `RefA` | `double` | `0.0` | 参考 Lab 的 a |
| `RefB` | `double` | `0.0` | 参考 Lab 的 b |
| `RoiX` | `int` | `0` | ROI X |
| `RoiY` | `int` | `0` | ROI Y |
| `RoiW` | `int` | `0` | ROI 宽度，`0` 表示使用剩余宽度 |
| `RoiH` | `int` | `0` | ROI 高度，`0` 表示使用剩余高度 |
| `WhiteBalanceTolerance` | `double` | `12.0` | 白平衡偏差阈值 |

## 输入 / 输出 / Inputs & Outputs
### 输入 / Inputs
| 名称 | 类型 | 必填 | 说明 |
|------|------|------|------|
| `Image` | `Image` | 是 | 输入图像 |
| `ReferenceColor` | `Any` | 否 | 参考 Lab 颜色，可传字典或数组 |

### 输出 / Outputs
| 名称 | 类型 | 说明 |
|------|------|------|
| `Image` | `Image` | 标注后的结果图 |
| `ColorInfo` | `Any` | 统一颜色分析结果结构 |
| `AnalysisMode` | `String` | 实际执行模式 |
| `ColorSpace` | `String` | 实际颜色空间 |
| `DeltaE` | `Float` | `LabDeltaE` 模式下的色差，其他模式为 0 |
| `Coverage` | `Float` | 颜色命中覆盖率 |
| `WhiteBalanceStatus` | `String` | `Balanced` 或 `Suspect` |
| `MeanColor` | `Any` | 平均颜色结果 |
| `DominantColors` | `Any` | 主色聚类结果 |
| `Diagnostics` | `Any` | ROI、灰世界偏差、模式等诊断信息 |

## 适用场景 / Use Cases
- 颜色分选、混料检测、色差验收
- 需要解释性较强的颜色阈值或 DeltaE 判定
- 有明确 ROI 的局部颜色一致性检查

## 已知限制 / Known Limitations
1. `LabDeltaE` 当前仍是 ROI 平均颜色比较，不适合处理复杂纹理或多色混合表面。
2. `WhiteBalanceStatus` 是轻量诊断，不等同于完成了完整色彩标定。
3. 若现场成像链路没有做白平衡和颜色校正，阈值与 DeltaE 的可迁移性会明显下降。
