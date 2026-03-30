# CannyEdge 技术笔记

> **对应算子**: `CannyEdgeOperator`  
> **OperatorType**: `OperatorType.EdgeDetection`  
> **代码依据**: `Acme.Product/src/Acme.Product.Infrastructure/Operators/CannyEdgeOperator.cs`  
> **相关算子**: [GaussianBlur](./01-GaussianBlur-技术笔记.md)、[SubpixelEdgeDetection](./07-SubpixelEdgeDetection-技术笔记.md)、[FindContours](./12-FindContours-技术笔记.md)  
> **阅读前置**: 梯度、边缘、噪声这几个概念先有直觉即可  
> **核心来源**: ClearVision 当前实现、OpenCV `Canny`、Canny 1986

---

## 1. 一句话先理解这个算子

`CannyEdge` 的目标不是“把图像变清楚”，而是尽量把真正的边界提出来，同时压掉噪声和伪边缘。

---

## 2. 先说清当前实现口径

ClearVision 当前实现大体遵循经典 Canny 流程，但有几处很重要的工程细节：

1. 输入如果是彩色图，会先转灰度
2. 如果启用高斯预模糊，会先做一次 `GaussianBlur`
3. 若 `AutoThreshold=true`，会先计算图像中位灰度，再按 `sigma` 推导阈值
4. 调用 `Cv2.Canny`
5. 附加输出里会回传实际使用的高低阈值

要特别注意的实现口径是：

- 自动阈值并不是 Otsu，它是基于**中位灰度**的经验公式
- 代码里高斯预处理固定使用 `sigma=1.0`，并不是把别的 sigma 参数暴露出来
- 元数据里 `GaussianKernelSize` 标成 `3-15`，但实际 `GetIntParam` 读参范围更宽，到 `31`

---

## 3. 算法原理

### 3.1 经典 Canny 在做什么

经典 Canny 通常包含这几步：

1. 高斯平滑抑制噪声
2. 计算梯度幅值和方向
3. 非极大值抑制，让边缘尽量细
4. 双阈值滞后连接，保留强边缘并有条件连接弱边缘

这也是它比简单 Sobel 更常用于工程边缘提取的原因。

### 3.2 为什么需要两个阈值

因为现实里的边缘强度不是完全整齐的：

- 强边缘直接保留
- 弱边缘如果连在强边缘上，也可能保留
- 其余噪声边缘则尽量丢掉

所以 `Threshold1` 和 `Threshold2` 并不是重复参数，而是控制“连接策略”的两个档位。

### 3.3 它和亚像素边缘检测的关系

[SubpixelEdgeDetection](./07-SubpixelEdgeDetection-技术笔记.md) 常把 Canny 当成候选边缘生成的一步，但它的目标更进一步，是把边缘位置从像素级细化到亚像素级。

---

## 4. 参数说明

### 4.1 输入输出端口怎么理解

| 端口 | 作用 |
|------|------|
| `Image` 输入 | 原图或灰度图 |
| `Image` 输出 | 边缘图 |
| `Edges` 输出 | 额外输出的边缘图像数据 |

### 4.2 关键参数怎么调

| 参数 | 当前实现默认值 | 怎么理解 |
|------|------|------|
| `Threshold1` | `50.0` | 低阈值，影响边缘连接 |
| `Threshold2` | `150.0` | 高阈值，决定强边缘门槛 |
| `AutoThreshold` | `false` | 是否用中位灰度自动推导阈值 |
| `AutoThresholdSigma` | `0.33` | 自动阈值公式中的松紧系数 |
| `EnableGaussianBlur` | `true` | 是否先做高斯平滑 |
| `GaussianKernelSize` | `5` | 预平滑核大小，偶数会自动修成奇数 |
| `ApertureSize` | `3` | Sobel 孔径大小 |
| `L2Gradient` | `false` | 是否用更精确的 L2 范数计算梯度幅值 |

### 4.3 和教材口径不完全一样的地方

- 教材里常把阈值设置当成独立调参问题，当前实现额外提供了“中位灰度自动阈值”分支
- 高斯平滑的 `sigma` 在代码里固定为 `1.0`
- 输出里会额外记录 `Threshold1Used`、`Threshold2Used`

---

## 5. 推荐使用链路与调参建议

### 5.1 常见链路

```text
Gray / Blur
  -> CannyEdge
  -> FindContours
  -> Shape / Measurement
```

### 5.2 调参建议

- 初学时可以先记住一个朴素比例：`低阈值 : 高阈值 ≈ 1 : 2` 或 `1 : 3`
- 如果现场波动较大，可以先试 `AutoThreshold=true`
- 如果边缘很多断裂，常见原因是阈值太高，或者前面的平滑太强

---

## 6. 这个算子的边界

### 6.1 它输出的是边缘，不是闭合目标

边缘图天然容易断裂，所以后面常常需要 [FindContours](./12-FindContours-技术笔记.md) 或别的结构化处理。

### 6.2 它怕低对比和重噪声

边缘本质上依赖灰度变化。如果目标和背景对比太弱，或者噪声太强，Canny 也会不稳定。

### 6.3 它不是高精度测量终点

如果你要做宽度、直线或圆的高精度测量，像素级 Canny 通常不够，还需要 [SubpixelEdgeDetection](./07-SubpixelEdgeDetection-技术笔记.md) 或 [CaliperTool](./15-CaliperTool-技术笔记.md)。

---

## 7. 失败案例与常见误区

### 案例 1：边缘满图都是，后面轮廓检测爆炸

常见原因是阈值太低，或者前面没有足够的平滑和 ROI 限定。

### 案例 2：启用自动阈值后结果和预想不一样

因为当前实现的自动阈值不是 Otsu，而是按图像中位灰度估算，图像亮度分布一变，阈值就会跟着变。

### 常见误区

- 误区一：Canny 只要调两个阈值就行  
  实际上前面的平滑质量、输入对比度和 ROI 都很重要。
- 误区二：Canny 检出的边缘就是最终测量边  
  真正的精密测量通常还要继续细化。

---

## 8. 专业来源与延伸阅读

- ClearVision 本地实现: `../../Acme.Product/src/Acme.Product.Infrastructure/Operators/CannyEdgeOperator.cs`
- ClearVision 本地资料: `../算子手册.md`、`../算子名片/EdgeDetection.md`
- OpenCV Documentation: *Canny*
- John Canny, *A Computational Approach to Edge Detection*, IEEE TPAMI, 1986
- Szeliski, *Computer Vision: Algorithms and Applications*, edge detection chapters

---

## 9. 一句话总结

`CannyEdge` 是一把非常经典的“像素级边缘提取刀”，但它只是把边缘先找出来，并不自动替你完成轮廓整理和高精度测量。
