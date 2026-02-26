# 坐标转换 / CoordinateTransform

## 基本信息 / Basic Info
| 项目 (Field) | 值 (Value) |
|------|------|
| 类名 (Class) | `CoordinateTransformOperator` |
| 枚举值 (Enum) | `OperatorType.CoordinateTransform` |
| 分类 (Category) | 标定 |
| 成熟度 (Maturity) | 稳定 Stable |
| 作者 (Author) | 蘅芜君 |

## 算法原理 / Algorithm Principle
> 中文：使用线性标定模型将像素点映射到物理坐标：`PhysicalX = OriginX + PixelX * ScaleX`，`PhysicalY = OriginY + PixelY * ScaleY`。
> English: Applies a linear coordinate mapping: `PhysicalX = OriginX + PixelX * ScaleX`, `PhysicalY = OriginY + PixelY * ScaleY`.

## 实现策略 / Implementation Strategy
> 中文：优先读取输入端口中的 `PixelX/PixelY`，其次回退到参数；比例尺优先来自标定文件（若存在），否则使用 `PixelSize`。当输入图像存在时叠加坐标可视化。
> English: Pixel coordinates are resolved from inputs first, then parameters; scale comes from calibration file when available, otherwise falls back to `PixelSize`. If image exists, visualization overlays are drawn.

## 核心 API 调用链 / Core API Call Chain
- `GetDoubleParam` / 输入端口读取（坐标与像素尺寸）
- `File.ReadAllText` + `JsonSerializer.Deserialize<CalibrationInfo>`（读取原点与比例）
- 线性变换公式计算 `PhysicalX/PhysicalY`
- `Cv2.Circle` / `Cv2.PutText`（可视化标记与文本）

## 参数说明 / Parameters
| 参数名 (Name) | 类型 (Type) | 默认值 (Default) | 范围 (Range) | 说明 (Description) |
|--------|------|--------|------|------|
| `PixelX` | `double` | 0 | - | - |
| `PixelY` | `double` | 0 | - | - |
| `PixelSize` | `double` | 0.01 | [0.0001, 100] | - |
| `CalibrationFile` | `file` | "" | - | - |

## 输入/输出端口 / Input/Output Ports
### 输入 / Inputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 必填 (Required) | 说明 (Description) |
|------|------|------|------|------|
| `Image` | 输入图像 | `Image` | No | - |
| `PixelX` | 像素X | `Float` | No | - |
| `PixelY` | 像素Y | `Float` | No | - |

### 输出 / Outputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 说明 (Description) |
|------|------|------|------|
| `Image` | 结果图像 | `Image` | - |
| `PhysicalX` | 物理X(mm) | `Float` | - |
| `PhysicalY` | 物理Y(mm) | `Float` | - |

## 性能特征 / Performance
| 指标 (Metric) | 值 (Value) |
|------|------|
| 时间复杂度 (Time Complexity) | 纯坐标转换为 `O(1)`；含可视化时为 `O(W*H)`（图像复制+绘制） |
| 典型耗时 (Typical Latency) | 无图模式约 `0.02-0.2 ms`；含图可视化约 `0.2-1.5 ms`（1920x1080） |
| 内存特征 (Memory Profile) | 无图模式近似常数内存；有图模式需一张结果图拷贝 |

## 适用场景 / Use Cases
- 适合 (Suitable)：已完成标定后的单点坐标换算、在线叠加显示像素与物理坐标。
- 不适合 (Not Suitable)：需要处理旋转/透视/非线性畸变的高精度几何变换任务。

## 已知限制 / Known Limitations
1. 当前模型仅支持独立 `ScaleX/ScaleY` 与平移，不支持旋转、剪切和透视项。
2. 标定文件解析异常会静默回退到 `PixelSize`，可能掩盖配置错误。
3. 仅处理单点转换，批量点集转换需要外层流程循环或扩展算子。

## 变更记录 / Changelog
| 版本 (Version) | 日期 (Date) | 变更内容 (Changes) |
|------|------|----------|
| 0.1.0 | 2026-02-26 | 自动生成文档骨架 / Generated skeleton |

| 0.2.0 | 2026-02-26 | 完成 Phase 2.3 P1 文档补全 / Completed Phase 2.3 P1 documentation enrichment |