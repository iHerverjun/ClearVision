# 形状匹配 / ShapeMatching

## 基本信息 / Basic Info
| 项目 (Field) | 值 (Value) |
|------|------|
| 类名 (Class) | `ShapeMatchingOperator` |
| 枚举值 (Enum) | `OperatorType.ShapeMatching` |
| 分类 (Category) | Matching |
| 成熟度 (Maturity) | 稳定 Stable |
| 当前版本 (Version) | `1.2.0` |

## 算法原理 / Algorithm Principle
虽然名称叫“形状匹配”，当前实现本质上仍是灰度模板的旋转/尺度搜索，而不是轮廓描述子匹配。

核心流程：
- 把搜索图和模板图转成灰度
- 构建金字塔做 coarse-to-fine 搜索
- 枚举 angle/scale 变换
- 对每个变换后的模板执行 `Cv2.MatchTemplate(..., CCoeffNormed)`
- 从单个变换的响应图中持续提取多个峰值
- 对变换内候选做局部抑制，再对全局候选做 IoU NMS

## 本轮改进 / This Revision
- `MaxMatches` 现在支持同姿态多实例；不再每个 angle/scale 只取一个 `MinMaxLoc` 峰值。
- 单个姿态下若场景中存在多个分离目标，算子会从同一响应图中继续提取次峰值。
- 最终输出排序增加稳定 tie-break 规则，减少多结果场景下的顺序抖动。

## 参数说明 / Parameters
| 参数名 (Name) | 类型 (Type) | 默认值 (Default) | 说明 (Description) |
|--------|------|--------|------|
| `TemplatePath` | `file` | `""` | 未提供 `Template` 输入时，从文件读取模板。 |
| `MinScore` | `double` | `0.7` | 最小匹配分数。 |
| `MaxMatches` | `int` | `1` | 最终最多输出的匹配数量；现在会真正覆盖同姿态多实例场景。 |
| `AngleStart` | `double` | `-30.0` | 搜索起始角度。 |
| `AngleExtent` | `double` | `60.0` | 搜索角度跨度。 |
| `AngleStep` | `double` | `1.0` | 基础角度步长。 |
| `ScaleMin` | `double` | `1.0` | 最小缩放。 |
| `ScaleMax` | `double` | `1.0` | 最大缩放。 |
| `ScaleStep` | `double` | `0.1` | 缩放步长。 |
| `NumLevels` | `int` | `3` | 金字塔层数。 |

## 输入/输出端口 / Input/Output Ports
### 输入 / Inputs
| 名称 (Name) | 数据类型 (DataType) | 必填 (Required) | 说明 (Description) |
|------|------|------|------|
| `Image` | `Image` | Yes | 搜索图像。 |
| `Template` | `Image` | No | 模板图像；未提供时可改用 `TemplatePath`。 |

### 输出 / Outputs
| 名称 (Name) | 数据类型 (DataType) | 说明 (Description) |
|------|------|------|
| `Image` | `Image` | 结果图，会绘制每个最终候选。 |
| `Matches` | `Any` | 匹配结果列表；每项包含 `X`、`Y`、`XSubpixel`、`YSubpixel`、`Angle`、`Scale`、`Score`、`CenterX`、`CenterY`、`Width`、`Height`。 |

### 运行时附加输出 / Runtime Additional Outputs
| 名称 (Name) | 数据类型 (DataType) | 说明 (Description) |
|------|------|------|
| `IsMatch` | `Boolean` | 是否找到候选。 |
| `Score` | `Double` | 最佳候选分数。 |
| `MatchCount` | `Integer` | 最终输出数量。 |
| `NumLevelsUsed` | `Integer` | 实际参与搜索的金字塔层数。 |

## 已知限制 / Known Limitations
1. 这仍然是模板匹配路线，不适用于强遮挡、强非刚体形变或明显透视变化。
2. 匹配核心固定使用 `CCoeffNormed`，没有暴露多种模板匹配方法。
3. 同姿态多实例已经支持，但在强重复纹理场景仍应结合 ROI、先验角度范围或更高阈值使用。

## 变更记录 / Changelog
| 版本 (Version) | 日期 (Date) | 变更内容 (Changes) |
|------|------|----------|
| 1.2.0 | 2026-04-12 | 支持同姿态多实例提峰，补充 `MaxMatches` 的实际行为与稳定排序说明 |
| 1.0.2 | 2026-03-14 | 第二轮基于源码补充粗到细角度搜索、金字塔规则、NMS 与实际输出结构说明 |
| 1.0.1 | 2026-03-14 | 基于源码补充算法原理、调用链、参数语义、适用场景与已知限制 |
| 1.0.0 | 2026-03-03 | 自动生成文档骨架 / Generated skeleton |
