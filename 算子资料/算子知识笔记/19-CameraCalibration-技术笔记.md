# CameraCalibration 技术笔记

> **对应算子**: `CameraCalibrationOperator`  
> **OperatorType**: `OperatorType.CameraCalibration`  
> **代码依据**: `Acme.Product/src/Acme.Product.Infrastructure/Operators/CameraCalibrationOperator.cs`  
> **相关算子**: [20-Undistort-技术笔记](./20-Undistort-技术笔记.md)、[18-PerspectiveTransform-技术笔记](./18-PerspectiveTransform-技术笔记.md)、[15-CaliperTool-技术笔记](./15-CaliperTool-技术笔记.md)  
> **阅读前置**: 相机内参、畸变、重投影误差、棋盘格标定板  
> **核心来源**: Zhang 2000、OpenCV `calibrateCamera` 文档、本仓库当前实现

---

## 1. 一句话先理解这个算子

`CameraCalibration` 的作用是估计相机的内参和畸变参数，让后续图像校正和尺寸测量有了几何基础。

## 2. 先说清当前实现口径

ClearVision 当前实现支持两种模式：

- `SingleImage`
- `FolderCalibration`

并支持两种标定板：

- `Chessboard`
- `CircleGrid`

几个关键实现口径要特别记住：

- 单图模式确实会直接调用 `Cv2.CalibrateCamera(...)` 并返回结果。
- 棋盘格模式会再做 `CornerSubPix` 亚像素细化。
- 文件夹模式至少需要 3 张有效图像，否则直接失败。
- 输出结果是 JSON，其中包含 `CameraMatrix`、`DistCoeffs`、`ReprojectionError` 等字段。

## 3. 算法原理

相机标定的核心是在已知物理尺寸和几何结构的标定板上，建立：

- 三维物点
- 图像中的对应点

再求解相机内参矩阵、畸变系数以及若干姿态参数。  
Zhang 2000 的平面标定方法是现代工业和 OpenCV 实践里最常见的经典路线。

## 4. 参数说明

### 4.1 输入输出怎么理解

- 输入 `Image`: 单图模式下的标定图。
- 参数 `ImageFolder`: 文件夹模式下的标定数据集目录。
- 输出 `CalibrationData`: 标定结果 JSON。
- 输出 `Image`: 可视化结果图。

### 4.2 关键参数怎么调

- `PatternType`: `Chessboard` 或 `CircleGrid`。
- `BoardWidth` / `BoardHeight`: 标定板内点行列数。
- `SquareSize`: 物理间距，默认 `25.0`，通常以毫米计。
- `Mode`: `SingleImage` 或 `FolderCalibration`。
- `CalibrationOutputPath`: 文件夹模式下尝试写出的 JSON 路径。

### 4.3 与教材口径的差别

- 从标定理论讲，单张图通常不足以得到稳定高质量内参，但当前实现仍支持单图快速标定，主要用于调试与快速验证。
- 当前实现即便写文件失败，也可能仍返回内存中的 `CalibrationData`。
- 接口元信息里 `Image` 仍被声明为必填输入，但在 `FolderCalibration` 模式下，真正参与批量标定的是 `ImageFolder` 指向的数据集目录。

## 5. 推荐使用链路与调参建议

推荐链路：

```text
采集多张标定板图像
  -> CameraCalibration
  -> Undistort
  -> 后续测量 / 透视矫正
```

调参建议：

- 真正要用于生产，优先用多图模式。
- 标定图要覆盖不同位置、角度和视野区域。
- 不要混入模糊、反光严重、尺寸不一致的数据。

## 6. 这个算子的边界

- 它估计的是相机和镜头参数，不直接完成透视拉正。
- 标定质量极度依赖样本多样性和角点提取质量。
- 单图模式更像“验证能不能跑通”，不是高可信生产标定终点。

## 7. 失败案例与常见误区

### 案例1：只拍几张几乎一样的标定图

这样就算能算出参数，稳定性也往往不够好。

### 案例2：拿透视变换代替标定

透视矫正只能处理单平面映射，不能恢复镜头畸变参数。

### 案例3：重投影误差低就万事大吉

还要结合实际图像尺寸、样本分布和下游测量效果一起看。

## 8. 专业来源与延伸阅读

- 本仓库: `CameraCalibrationOperator.cs`
- Zhengyou Zhang, *A Flexible New Technique for Camera Calibration*, 2000
- OpenCV 官方文档: `calibrateCamera`
- 相机模型、畸变模型与重投影误差相关教材

## 9. 一句话总结

`CameraCalibration` 是让视觉测量从“像素级猜测”走向“几何上可解释”的起点，但前提是你的标定数据足够靠谱。
