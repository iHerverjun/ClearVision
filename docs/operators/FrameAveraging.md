# 帧平均 / FrameAveraging

## 基本信息 / Basic Info
| 项目 (Field) | 值 (Value) |
|------|------|
| 类名 (Class) | `FrameAveragingOperator` |
| 枚举值 (Enum) | `OperatorType.FrameAveraging` |
| 分类 (Category) | 预处理 / 多帧降噪 |
| 成熟度 (Maturity) | 稳定 Stable |
| 作者 (Author) | 蘅芜君 |

## 算法原理 / Algorithm Principle
该算子做的是**时间域融合**，不是空间域滤波。它会保留最近 `N` 帧图像，在时间轴上对同一像素位置进行统计融合：

- **Mean 模式**：
  `O(x, y) = (1 / N) × Σ I_t(x, y)`
- **Median 模式**：
  `O(x, y) = median(I_1(x, y), I_2(x, y), ..., I_N(x, y))`

两种模式适用的噪声模型不同：

- `Mean` 更适合抑制随机高斯噪声，理论上噪声标准差会随帧数增加而近似按 `1 / sqrt(N)` 下降。
- `Median` 更适合抑制脉冲噪声、偶发亮点、随机闪烁等离群值，但计算与内存开销更大。

需要特别注意：该算子默认**每次执行都返回当前缓存帧的融合结果**，即便缓存尚未积满配置的 `FrameCount`，也会输出“热启动阶段”的部分融合结果。

> English: This operator performs temporal fusion across the latest frames, using either mean or per-pixel temporal median.

## 实现策略 / Implementation Strategy
当前实现不是“无状态的一次性批处理”，而是一个**带内部缓存队列的状态型算子**：

- 使用 `_frames` 队列保存最近若干帧，并通过 `_syncRoot` 加锁保证并发安全。
- 如果新输入与缓存中的参考帧在 `Rows / Cols / Type` 上不一致，会先清空历史帧，再重新开始累计，避免不同尺寸或不同位深混算。
- 队列更新完成后，会复制出一个 `snapshot` 在锁外计算，减少锁持有时间。
- `Mean` 模式使用 `CV_32F` 累加图和 `Cv2.Accumulate`，最后再转回原始图像类型，避免 8 位整型直接求和溢出。
- `Median` 模式并没有在 C# 层逐像素三重循环排序，而是把每帧重排成单行后拼接为时间栈，再用 `Cv2.Sort` 对每一列做排序，最后取中间行并 reshape 回原图尺寸。这是当前实现中最关键的性能设计点。

因此，这个算子的重点不只是“平均”，而是**如何在多帧缓存、线程安全和 OpenCV 向量化计算之间取得平衡**。

> English: The implementation keeps a rolling frame queue, computes outside the lock, uses floating-point accumulation for mean, and uses a vectorized temporal stack plus column-wise sort for median.

## 核心 API 调用链 / Core API Call Chain
1. `TryGetInputImage(inputs)`
2. `GetIntParam("FrameCount")` / `GetStringParam("Mode")`
3. 在锁内维护 `_frames` 队列，并在尺寸/类型变化时清空历史
4. 复制 `snapshot = _frames.Select(f => f.Clone()).ToList()`
5. `ComputeMean(snapshot)`
   - `ConvertTo(..., CV_32F)`
   - `Cv2.Accumulate(...)`
   - `ConvertTo(result, originalType, 1.0 / frameCount)`
6. 或 `ComputeMedian(snapshot)`
   - `Reshape(1, 1)`
   - `Cv2.VConcat(flattened, stacked)`
   - `Cv2.Sort(stacked, sorted, EveryColumn | Ascending)`
   - 取中位行并 `Reshape(channels, rows)`
7. `CreateImageOutput(result, additionalData)`

## 参数说明 / Parameters
| 参数名 (Name) | 类型 (Type) | 默认值 (Default) | 范围 (Range) | 说明 (Description) |
|--------|------|--------|------|------|
| `FrameCount` | `int` | `8` | `[1, 64]` | 时间窗口大小，即最多缓存多少帧参与融合。值越大，降噪更强，但启动更慢、拖影风险更高、内存占用也更大。 |
| `Mode` | `enum` | `Mean` | `Mean` / `Median` | 融合模式。`Mean` 速度更稳、适合随机噪声；`Median` 更抗离群值，但更耗时、更占内存。 |

## 输入/输出端口 / Input/Output Ports
### 输入 / Inputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 必填 (Required) | 说明 (Description) |
|------|------|------|------|------|
| `Image` | `Image` | `Image` | Yes | 连续输入的视频帧或时序图像。所有参与融合的帧必须尺寸和类型一致。 |

### 输出 / Outputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 说明 (Description) |
|------|------|------|------|
| `Image` | `Image` | `Image` | 当前缓存窗口上的融合结果。 |
| `FrameCount` | `Frame Count` | `Integer` | **当前缓存中实际参与融合的帧数**，不是配置参数原值；在热启动阶段会从 `1` 逐步增长到目标值。 |

### 运行时附加输出 / Runtime Additional Outputs
| 名称 (Name) | 数据类型 (DataType) | 说明 (Description) |
|------|------|------|
| `Width` | `Integer` | 输出图像宽度。 |
| `Height` | `Integer` | 输出图像高度。 |

## 性能特征 / Performance
| 指标 (Metric) | 值 (Value) |
|------|------|
| 时间复杂度 (Time Complexity) | `Mean` 近似 `O(N × H × W × C)`；`Median` 近似 `O(H × W × C × N log N)`。 |
| 典型耗时 (Typical Latency) | `Mean` 通常明显快于 `Median`；`Median` 的主要成本来自时间栈构建、列排序和额外内存搬运。 |
| 内存特征 (Memory Profile) | 需要维护最多 `FrameCount` 帧的缓存；计算时还会复制 `snapshot`。`Median` 还会额外构建 `stacked`/`sorted` 矩阵，峰值内存高于 `Mean`。 |

## 适用场景 / Use Cases
- **适合 (Suitable)**：相机固定、目标基本静止、连续采图且主要问题是随机噪声的场景。
- **适合 (Suitable)**：需要提升信噪比、改善弱纹理可见性、抑制传感器噪声的预处理流程。
- **适合 (Suitable)**：`Median` 模式可用于抑制偶发亮点、火花、随机闪烁、热像素等时序离群值。
- **不适合 (Not Suitable)**：目标高速运动或位置变化明显的场景，会导致拖影、重影或轮廓模糊。
- **不适合 (Not Suitable)**：输入分辨率、通道数、位深频繁变化的流程，因为历史帧会被清空，累计效果无法稳定建立。
- **不适合 (Not Suitable)**：对单帧实时性非常敏感且内存预算很紧的任务，尤其不适合使用较大 `FrameCount` 的 `Median` 模式。

## 已知限制 / Known Limitations
1. 这是一个有状态算子，历史帧缓存在算子实例内部；如果同一实例被多个流程复用，需要明确管理生命周期与上下文隔离。
2. 在缓存未积满前，算子也会立即输出结果，因此前几帧的降噪效果会逐渐爬升，而不是一步到位。
3. 当 `FrameCount` 为偶数时，`Median` 当前实现取排序后的“上中位数”（`frames.Count / 2` 对应的行），不是两个中间值的平均。
4. 所有帧必须尺寸和类型完全一致，否则内部会清空缓存重新开始。
5. `Median` 是**时间域中值**，并不会像 `MedianBlur` 那样平滑单帧空间噪点；它更适合跨帧离群值抑制。
6. 当前实现没有提供“窗口已满再输出”的开关，也没有时间戳或触发同步机制，默认按到达顺序滚动融合。

## 变更记录 / Changelog
| 版本 (Version) | 日期 (Date) | 变更内容 (Changes) |
|------|------|----------|
| 1.0.1 | 2026-03-14 | 基于源码补充时间域融合原理、状态缓存行为、Median 向量化实现与限制说明 |
| 1.0.0 | 2026-03-03 | 自动生成文档骨架 / Generated skeleton |
