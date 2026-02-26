# 透视变换 / PerspectiveTransform

## 基本信息 / Basic Info
| 项目 (Field) | 值 (Value) |
|------|------|
| 类名 (Class) | `PerspectiveTransformOperator` |
| 枚举值 (Enum) | `OperatorType.PerspectiveTransform` |
| 分类 (Category) | 预处理 |
| 成熟度 (Maturity) | 稳定 Stable |
| 作者 (Author) | 蘅芜君 |

## 算法原理 / Algorithm Principle
> 中文：通过 4 组源点与目标点求解单应矩阵（Homography），将任意四边形区域重映射到目标平面。
> English: Solves a 3x3 homography from four source and destination points and warps the image to the target plane.

## 实现策略 / Implementation Strategy
> 中文：算子使用显式的 16 个坐标参数构建 `srcPoints/dstPoints`，按固定点序求变换矩阵后执行透视重采样，输出尺寸由 `OutputWidth/OutputHeight` 控制。
> English: Uses explicit coordinate parameters to build source/destination point arrays, computes perspective matrix, and warps to configured output size.

## 核心 API 调用链 / Core API Call Chain
- 参数读取：`SrcX1..SrcY4` 与 `DstX1..DstY4`
- `Cv2.GetPerspectiveTransform`（计算 `3x3` 透视矩阵）
- `Cv2.WarpPerspective`（执行透视变换）

## 参数说明 / Parameters
| 参数名 (Name) | 类型 (Type) | 默认值 (Default) | 范围 (Range) | 说明 (Description) |
|--------|------|--------|------|------|
| `SrcX1` | `double` | 0 | - | - |
| `SrcY1` | `double` | 0 | - | - |
| `SrcX2` | `double` | 100 | - | - |
| `SrcY2` | `double` | 0 | - | - |
| `SrcX3` | `double` | 100 | - | - |
| `SrcY3` | `double` | 100 | - | - |
| `SrcX4` | `double` | 0 | - | - |
| `SrcY4` | `double` | 100 | - | - |
| `DstX1` | `double` | 0 | - | - |
| `DstY1` | `double` | 0 | - | - |
| `DstX2` | `double` | 640 | - | - |
| `DstY2` | `double` | 0 | - | - |
| `DstX3` | `double` | 640 | - | - |
| `DstY3` | `double` | 480 | - | - |
| `DstX4` | `double` | 0 | - | - |
| `DstY4` | `double` | 480 | - | - |
| `OutputWidth` | `int` | 640 | [1, 8192] | - |
| `OutputHeight` | `int` | 480 | [1, 8192] | - |

## 输入/输出端口 / Input/Output Ports
### 输入 / Inputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 必填 (Required) | 说明 (Description) |
|------|------|------|------|------|
| `Image` | 图像 | `Image` | Yes | - |

### 输出 / Outputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 说明 (Description) |
|------|------|------|------|
| `Image` | 图像 | `Image` | - |

## 性能特征 / Performance
| 指标 (Metric) | 值 (Value) |
|------|------|
| 时间复杂度 (Time Complexity) | 矩阵求解 `O(1)`，重采样 `O(W*H)` |
| 典型耗时 (Typical Latency) | 约 `1.5-10 ms`（1920x1080，取决于输出尺寸与插值成本） |
| 内存特征 (Memory Profile) | 一张输出图 + 一个 `3x3` 矩阵 |

## 适用场景 / Use Cases
- 适合 (Suitable)：透视矫正、斜拍工件拉正、文档/标签四点拉伸到标准视角。
- 不适合 (Not Suitable)：点位顺序不稳定、四点几何退化（近共线）或需要自动点排序的场景。

## 已知限制 / Known Limitations
1. 当前不自动校验四点顺序与凸性，点序错误会导致结果扭曲。
2. 边界填充固定黑色常量，未暴露边缘扩展策略参数。
3. 仅支持单次透视变换，不含畸变补偿或多阶段几何优化。

## 变更记录 / Changelog
| 版本 (Version) | 日期 (Date) | 变更内容 (Changes) |
|------|------|----------|
| 0.1.0 | 2026-02-26 | 自动生成文档骨架 / Generated skeleton |

| 0.2.0 | 2026-02-26 | 完成 Phase 2.3 P1 文档补全 / Completed Phase 2.3 P1 documentation enrichment |