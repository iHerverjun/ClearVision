# N点标定 / NPointCalibration

## 基本信息 / Basic Info
| 项目 (Field) | 值 (Value) |
|------|------|
| 类名 (Class) | `NPointCalibrationOperator` |
| 枚举值 (Enum) | `OperatorType.NPointCalibration` |
| 分类 (Category) | 标定 |
| 成熟度 (Maturity) | 稳定 Stable |
| 作者 (Author) | 蘅芜君 |

## 算法原理 / Algorithm Principle
> 中文：根据点对构建像素坐标到物理坐标的映射；仿射模式用 3 对点估计 `2x3` 矩阵，透视模式用 4 对点估计 `3x3` 矩阵，并计算重投影误差与像素尺寸估计值。
> English: Builds pixel-to-world mapping from point pairs: affine mode estimates a 2x3 matrix from 3 pairs, perspective mode estimates a 3x3 homography from 4 pairs, then computes reprojection error and pixel-size estimate.

## 实现策略 / Implementation Strategy
> 中文：输入支持参数或端口传入，兼容多种 JSON/字典字段格式；执行时先校验最小点数，再求解变换矩阵、误差与像素尺寸，最后可选保存标定结果到文件。
> English: Accepts point pairs from parameters or input ports with multiple JSON/dictionary schemas; validates minimum pairs, solves matrix, calculates error/pixel size, and optionally saves payload to disk.

## 核心 API 调用链 / Core API Call Chain
- `JsonDocument.Parse` + 自定义 `TryParsePointPairs`（多格式点对解析）
- `Cv2.GetAffineTransform`（Affine，取前 3 对点）
- `Cv2.GetPerspectiveTransform`（Perspective，取前 4 对点）
- 自定义误差计算（逐点重投影 RMSE）
- 自定义 `EstimatePixelSize`（点对距离比平均）
- `File.WriteAllText`（可选保存标定结果）

## 参数说明 / Parameters
| 参数名 (Name) | 类型 (Type) | 默认值 (Default) | 范围 (Range) | 说明 (Description) |
|--------|------|--------|------|------|
| `CalibrationMode` | `enum` | Affine | - | - |
| `PointPairs` | `string` | "" | - | - |
| `SavePath` | `file` | "" | - | - |

## 输入/输出端口 / Input/Output Ports
### 输入 / Inputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 必填 (Required) | 说明 (Description) |
|------|------|------|------|------|
| `Image` | Image | `Image` | No | - |

### 输出 / Outputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 说明 (Description) |
|------|------|------|------|
| `TransformMatrix` | Transform Matrix | `Any` | - |
| `PixelSize` | Pixel Size | `Float` | - |
| `ReprojectionError` | Reprojection Error | `Float` | - |

## 性能特征 / Performance
| 指标 (Metric) | 值 (Value) |
|------|------|
| 时间复杂度 (Time Complexity) | 矩阵求解近似 `O(1)`，重投影误差 `O(N)`，像素尺寸估计 `O(N^2)` |
| 典型耗时 (Typical Latency) | 常见 `3-20` 点对下约 `0.1-2 ms`（不含磁盘写入） |
| 内存特征 (Memory Profile) | 主要为点对列表与小矩阵，约 `O(N)` |

## 适用场景 / Use Cases
- 适合 (Suitable)：工位快速点对标定、少量特征点完成像素到物理空间映射、需要输出可存档变换矩阵。
- 不适合 (Not Suitable)：点对包含明显外点且需自动鲁棒估计、强透视畸变但点数极少、控制点共线或几何退化。

## 已知限制 / Known Limitations
1. 透视模式只使用前 4 对点，仿射模式只使用前 3 对点，额外点不会参与矩阵求解。
2. 未集成 RANSAC 等外点剔除机制，点对质量直接决定误差与稳定性。
3. `PixelSize` 由点对距离比平均估计，仅适用于近似各向同性尺度场景。

## 变更记录 / Changelog
| 版本 (Version) | 日期 (Date) | 变更内容 (Changes) |
|------|------|----------|
| 0.1.0 | 2026-02-26 | 自动生成文档骨架 / Generated skeleton |

| 0.2.0 | 2026-02-26 | 完成 Phase 2.3 P1 文档补全 / Completed Phase 2.3 P1 documentation enrichment |