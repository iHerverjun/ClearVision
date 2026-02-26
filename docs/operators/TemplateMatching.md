# 模板匹配 / TemplateMatch

## 基本信息 / Basic Info
| 项目 (Field) | 值 (Value) |
|------|------|
| 类名 (Class) | `TemplateMatchOperator` |
| 枚举值 (Enum) | `OperatorType.TemplateMatching` |
| 分类 (Category) | 匹配定位 |
| 成熟度 (Maturity) | 稳定 Stable |
| 作者 (Author) | 蘅芜君 |

## 算法原理 / Algorithm Principle
> 中文：读取模板图后执行 OpenCV `MatchTemplate`，通过 `MinMaxLoc` 获取最优位置；对 `SQDiff` 类方法做分数反向归一化，再与阈值比较输出是否匹配。
> English: Decode template image, run OpenCV `MatchTemplate`, locate best response via `MinMaxLoc`, normalize score (including inverse handling for SQDiff), and compare with threshold to determine match.

## 实现策略 / Implementation Strategy
> 中文：统一封装多种模板匹配模式并输出一致化分数，便于在流程中用同一阈值语义做存在性判定与定位。
> English: Multiple template-matching modes are wrapped with a unified score semantics for consistent pass/fail and localization logic.

## 核心 API 调用链 / Core API Call Chain
- `Cv2.ImDecode`（模板解码）
- `Cv2.MatchTemplate`（相关性/差异度计算）
- `Cv2.MinMaxLoc`（最佳候选提取）
- 分数归一化与阈值判定
- `Cv2.Rectangle` / `Cv2.PutText`（命中可视化）

## 参数说明 / Parameters
| 参数名 (Name) | 类型 (Type) | 默认值 (Default) | 范围 (Range) | 说明 (Description) |
|--------|------|--------|------|------|
| `Method` | `enum` | NCC | - | - |
| `Threshold` | `double` | 0.8 | [0.1, 1] | - |
| `MaxMatches` | `int` | 1 | [1, 100] | - |

## 输入/输出端口 / Input/Output Ports
### 输入 / Inputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 必填 (Required) | 说明 (Description) |
|------|------|------|------|------|
| `Image` | 输入图像 | `Image` | Yes | - |
| `Template` | 模板图像 | `Image` | Yes | - |

### 输出 / Outputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 说明 (Description) |
|------|------|------|------|
| `Image` | 结果图像 | `Image` | - |
| `Position` | 匹配位置 | `Point` | - |
| `Score` | 匹配分数 | `Float` | - |
| `IsMatch` | 是否匹配 | `Boolean` | - |

## 性能特征 / Performance
| 指标 (Metric) | 值 (Value) |
|------|------|
| 时间复杂度 (Time Complexity) | O((W-w+1)×(H-h+1)) |
| 典型耗时 (Typical Latency) | ~3-40 ms（与模板尺寸强相关） |
| 内存特征 (Memory Profile) | 结果响应图约 `(W-w+1)×(H-h+1)` 浮点内存 |

## 适用场景 / Use Cases
- 适合 (Suitable)：同尺度、同姿态零件的快速定位与有无检测。
- 不适合 (Not Suitable)：显著旋转/缩放/形变或大光照漂移场景。

## 已知限制 / Known Limitations
1. `MaxMatches` 参数当前未在执行逻辑中展开为多目标返回。
2. 默认只取全局最优点，重复纹理场景需结合 ROI 或后处理。
3. 对尺度变化不鲁棒，需搭配金字塔或形状匹配算子。

## 变更记录 / Changelog
| 版本 (Version) | 日期 (Date) | 变更内容 (Changes) |
|------|------|----------|
| 0.1.0 | 2026-02-26 | 自动生成文档骨架 / Generated skeleton |
| 0.2.0 | 2026-02-26 | 完成 Phase 2.3 P0 文档补全 / Completed Phase 2.3 P0 documentation enrichment |
