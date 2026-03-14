# 自适应阈值 / AdaptiveThreshold

## 基本信息 / Basic Info
| 项目 (Field) | 值 (Value) |
|------|------|
| 类名 (Class) | `AdaptiveThresholdOperator` |
| 枚举值 (Enum) | `OperatorType.AdaptiveThreshold` |
| 分类 (Category) | 预处理 / 二值化 |
| 成熟度 (Maturity) | 稳定 Stable |
| 作者 (Author) | 蘅芜君 |

## 算法原理 / Algorithm Principle
自适应阈值不会对整幅图使用一个全局阈值，而是针对每个像素在其邻域窗口 `W(x, y)` 内计算局部阈值 `T(x, y)`：

- **Mean 模式**：`T(x, y) = mean(W(x, y)) - C`
- **Gaussian 模式**：`T(x, y) = gaussian_weighted_mean(W(x, y)) - C`

随后执行二值判定：

- **Binary**：当 `src(x, y) > T(x, y)` 时输出 `MaxValue`，否则输出 `0`
- **BinaryInv**：与 `Binary` 相反

这类方法适合处理光照不均、背景缓慢变化的图像，因为阈值会随局部亮度变化而调整，而不是被单个全局阈值限制。

> English: The operator computes a local threshold per pixel from a neighborhood window, then performs binary or inverted binary thresholding.

## 实现策略 / Implementation Strategy
当前实现直接封装 OpenCV 的 `Cv2.AdaptiveThreshold`，但在进入 OpenCV 前后增加了几层与工程集成相关的处理：

- **统一转灰度**：若输入是多通道图像，先转为灰度，保证满足 `AdaptiveThreshold` 对单通道输入的要求。
- **窗口尺寸修正**：运行时先将 `BlockSize` 约束到 `[3, 51]`，如果传入偶数，会自动加 `1` 变成奇数。
- **输出转回 BGR**：OpenCV 得到的是单通道二值图，但当前实现会再转成 `BGR` 三通道，以避免浏览器/Canvas 对单通道 PNG 的兼容问题。
- **零拷贝输出封装**：最终通过 `CreateImageOutput` 输出 `ImageWrapper`，同时附带宽高和部分参数回传值，方便后续节点记录与调试。

这说明该算子既是“算法算子”，也是“工程适配算子”：它不只做阈值分割，还处理了前端显示与流程链路兼容性问题。

> English: The implementation uses OpenCV for the thresholding itself, while the C# layer handles grayscale conversion, parameter sanitation, browser-friendly output conversion, and pipeline output packaging.

## 核心 API 调用链 / Core API Call Chain
1. `TryGetInputImage(inputs, "Image")`
2. `GetDoubleParam / GetStringParam / GetIntParam`
3. `Cv2.CvtColor(src, gray, ColorConversionCodes.BGR2GRAY)` 或 `src.CopyTo(gray)`
4. `Cv2.AdaptiveThreshold(gray, binary, maxValue, adaptiveType, threshType, blockSize, c)`
5. `Cv2.CvtColor(binary, dst, ColorConversionCodes.GRAY2BGR)`
6. `CreateImageOutput(dst, additionalData)`

## 参数说明 / Parameters
| 参数名 (Name) | 类型 (Type) | 默认值 (Default) | 范围 (Range) | 说明 (Description) |
|--------|------|--------|------|------|
| `MaxValue` | `double` | `255.0` | `[0, 255]` | 二值图前景输出值。通常保持 `255`，得到标准黑白图。 |
| `AdaptiveMethod` | `enum` | `Gaussian` | `Gaussian` / `Mean` | 局部阈值计算方式。`Gaussian` 更强调邻域中心，通常对光照渐变更稳健；`Mean` 更直接、速度也更容易理解。 |
| `ThresholdType` | `enum` | `Binary` | `Binary` / `BinaryInv` | 二值化方向。前景是亮目标时常用 `Binary`，前景是暗目标时常用 `BinaryInv`。 |
| `BlockSize` | `int` | `11` | **实际执行范围** `[3, 51]`，且必须为奇数 | 局部窗口边长。值越大，阈值更平滑、越接近“局部全局阈值”；值越小，对局部细节和噪声都更敏感。当前实现若收到偶数会自动修正为下一个奇数。 |
| `C` | `double` | `2.0` | `[-100, 100]` | 从局部统计量中减去的常数。`C` 越大，阈值越低，在 `Binary` 模式下通常会留下更多白色前景；`C` 为负时则会抬高阈值。 |

## 输入/输出端口 / Input/Output Ports
### 输入 / Inputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 必填 (Required) | 说明 (Description) |
|------|------|------|------|------|
| `Image` | 输入图像 | `Image` | Yes | 待进行局部阈值分割的输入图像。实现会优先转为灰度再处理。 |

### 输出 / Outputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 说明 (Description) |
|------|------|------|------|
| `Image` | 输出图像 | `Image` | 二值化结果图。**注意：运行时输出为 3 通道 `BGR` 图像**，不是单通道灰度图。 |

### 运行时附加输出 / Runtime Additional Outputs
由 `CreateImageOutput(...)` 和当前算子实现附带输出：

| 名称 (Name) | 数据类型 (DataType) | 说明 (Description) |
|------|------|------|
| `Width` | `Integer` | 输出图像宽度。 |
| `Height` | `Integer` | 输出图像高度。 |
| `AdaptiveMethod` | `String` | 本次执行实际采用的局部阈值方法。 |
| `BlockSize` | `Integer` | 本次执行实际使用的窗口大小；若输入为偶数，这里会反映修正后的奇数值。 |
| `C` | `Double` | 本次执行实际使用的常数偏置。 |

## 性能特征 / Performance
| 指标 (Metric) | 值 (Value) |
|------|------|
| 时间复杂度 (Time Complexity) | 近似为 `O(H × W × k²)`，其中 `k = BlockSize`；实际核心计算由 OpenCV 本地实现完成。 |
| 典型耗时 (Typical Latency) | 仓库中未提供固定 benchmark；主要耗时集中在局部阈值计算本身，以及前后的灰度/BGR 转换。 |
| 内存特征 (Memory Profile) | 额外分配 1 张灰度图、1 张中间二值图和 1 张最终 BGR 输出图。 |

## 适用场景 / Use Cases
- **适合 (Suitable)**：背景亮度不均、存在阴影、反光或亮度渐变的图像二值化。
- **适合 (Suitable)**：作为轮廓检测、Blob 分析、缺陷分割前的预处理步骤。
- **适合 (Suitable)**：纸张、标签、字符、表面纹理等“局部对比存在但全局亮度不稳定”的场景。
- **不适合 (Not Suitable)**：需要基于颜色通道分别判断的任务，因为当前实现统一转灰度。
- **不适合 (Not Suitable)**：噪声非常强但未先做平滑/降噪的图像，尤其在 `BlockSize` 较小时容易产生碎片噪点。
- **不适合 (Not Suitable)**：需要保留单通道二值图原始格式的下游模块，因为当前输出会被转换成 BGR。

## 已知限制 / Known Limitations
1. 元数据属性里 `BlockSize` 的声明上限为 `99`，但执行与校验实际限制为 `51`；文档以源码真实行为为准。
2. 算子会强制把单通道二值结果转成三通道 `BGR` 输出，这对显示兼容友好，但会改变下游拿到的数据形态。
3. 当前实现没有在算子内部做去噪、归一化或形态学后处理；结果质量较依赖输入图像质量与参数选择。
4. 当前实现只处理“灰度化后的单通道二值分割”，并不支持对彩色图像做逐通道自适应阈值。
5. 代码中未对非 `8-bit` 深度图像做显式位深转换；若上游提供非常规位深图像，需要先做格式归一化更稳妥。

## 变更记录 / Changelog
| 版本 (Version) | 日期 (Date) | 变更内容 (Changes) |
|------|------|----------|
| 1.0.1 | 2026-03-14 | 基于源码补充算法公式、参数真实行为、输出兼容性与限制说明 |
| 1.0.0 | 2026-03-03 | 自动生成文档骨架 / Generated skeleton |
