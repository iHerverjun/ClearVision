# PerspectiveTransform 技术笔记

> **对应算子**: `PerspectiveTransformOperator`  
> **OperatorType**: `OperatorType.PerspectiveTransform`  
> **代码依据**: `Acme.Product/src/Acme.Product.Infrastructure/Operators/PerspectiveTransformOperator.cs`  
> **相关算子**: [19-CameraCalibration-技术笔记](./19-CameraCalibration-技术笔记.md)、[20-Undistort-技术笔记](./20-Undistort-技术笔记.md)、[14-ShapeMatching-技术笔记](./14-ShapeMatching-技术笔记.md)  
> **阅读前置**: 平面单应性、四点映射、透视畸变  
> **核心来源**: OpenCV `getPerspectiveTransform` / `warpPerspective` 文档、Szeliski《Computer Vision》、本仓库当前实现

---

## 1. 一句话先理解这个算子

`PerspectiveTransform` 的作用是：给定原图中的 4 个点和目标平面上的 4 个点，把一个斜着拍的平面拉正。

## 2. 先说清当前实现口径

ClearVision 当前实现支持两种点输入方式：

- 新方式: 输入端口 `SrcPoints` / `DstPoints` 或对应 JSON
- 旧方式: 16 个独立参数 `SrcX1 ... DstY4`

如果新方式存在且点数足够，代码优先走新方式；否则退回旧版 16 参数。  
最后统一调用：

- `Cv2.GetPerspectiveTransform`
- `Cv2.WarpPerspective`

并输出 `PointSetMode` 和 `PointCount` 等附加信息。

## 3. 算法原理

对于同一个平面，如果只是因为视角不同看起来变形成四边形，那么可以用一个 3x3 单应矩阵把它映射回规则平面。  
这和相机标定不是一回事：透视变换只解决“平面上的四点映射关系”。

## 4. 参数说明

### 4.1 输入输出怎么理解

- 输入 `Image`: 待矫正图像。
- 输入 `SrcPoints` / `DstPoints`: 四点对应关系。
- 输出 `Image`: 透视拉正后的图像。

### 4.2 关键参数怎么调

- `SrcPointsJson` / `DstPointsJson`: 新版 JSON 点集输入。
- `SrcX1 ... DstY4`: 兼容旧流程的独立点参数。
- `OutputWidth` / `OutputHeight`: 输出平面尺寸，默认 `640 x 480`。

### 4.3 与教材口径的差别

- 当前实现明显兼顾了新旧两套点传入方式。
- 只要点集足够，它不会自行排序点位正确性，输入点顺序本身就很重要。

## 5. 推荐使用链路与调参建议

推荐链路：

```text
Undistort
  -> PerspectiveTransform
  -> TemplateMatch / 测量 / OCR
```

调参建议：

- 先做 [20-Undistort-技术笔记](./20-Undistort-技术笔记.md)，再做透视拉正，通常更合理。
- 四点顺序必须一一对应，常用“左上、右上、右下、左下”。
- 输出尺寸应与业务所需的标准平面尺寸一致。

## 6. 这个算子的边界

- 它只适用于单一平面关系。
- 输入点不准时，结果会整体拉歪。
- 它不能替代镜头畸变校正，也不能替代相机内参标定。

## 7. 失败案例与常见误区

### 案例1：点顺序错了，结果翻折或扭曲

这是透视矫正里最常见的问题之一。

### 案例2：没去畸变就直接做透视

若镜头畸变明显，四点关系本身就不稳定，结果容易有系统误差。

### 案例3：把透视矫正当相机标定

透视矫正解决的是平面映射，不是相机内参估计。

## 8. 专业来源与延伸阅读

- 本仓库: `PerspectiveTransformOperator.cs`
- OpenCV 官方文档: `getPerspectiveTransform`、`warpPerspective`
- Richard Szeliski, *Computer Vision: Algorithms and Applications*
- 与 [19-CameraCalibration-技术笔记](./19-CameraCalibration-技术笔记.md)、[20-Undistort-技术笔记](./20-Undistort-技术笔记.md) 对照阅读

## 9. 一句话总结

`PerspectiveTransform` 是“把一个平面拉正”的算子，不是相机标定，也不是镜头去畸变。
