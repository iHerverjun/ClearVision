# Undistort 技术笔记

> **对应算子**: `UndistortOperator`  
> **OperatorType**: `OperatorType.Undistort`  
> **代码依据**: `Acme.Product/src/Acme.Product.Infrastructure/Operators/UndistortOperator.cs`  
> **相关算子**: [19-CameraCalibration-技术笔记](./19-CameraCalibration-技术笔记.md)、[18-PerspectiveTransform-技术笔记](./18-PerspectiveTransform-技术笔记.md)、[16-CircleMeasurement-技术笔记](./16-CircleMeasurement-技术笔记.md)  
> **阅读前置**: 径向畸变、相机内参、标定结果 JSON  
> **核心来源**: OpenCV `undistort` 文档、Zhang 标定体系、本仓库当前实现

---

## 1. 一句话先理解这个算子

`Undistort` 的作用是根据标定结果，把镜头带来的桶形或枕形变形尽量拉回更接近真实几何的样子。

## 2. 先说清当前实现口径

ClearVision 当前实现的关键点有：

- 标定数据优先从输入端口 `CalibrationData` 读取。
- 如果端口没有，再回退到参数 `CalibrationFile`。
- `CameraMatrix` 兼容两种 JSON 结构：一维 9 元数组或 3x3 二维数组。
- `DistCoeffs` 也支持扁平数组和嵌套数组。
- 真正执行时直接调用 `Cv2.Undistort(...)`，没有暴露 `newCameraMatrix`、ROI 裁剪等高级控制。

因此它的强项不是高级相机模型管理，而是把常见标定 JSON 快速接入去畸变流程。

## 3. 算法原理

镜头畸变会让原本应当是直线的边在图像里弯曲。  
去畸变的核心是：根据相机内参和畸变系数，把每个像素重新映射回更接近理想 pinhole 模型的位置。

它与 [18-PerspectiveTransform-技术笔记](./18-PerspectiveTransform-技术笔记.md) 的差别是：

- 去畸变修正的是镜头模型误差。
- 透视变换修正的是平面视角关系。

## 4. 参数说明

### 4.1 输入输出怎么理解

- 输入 `Image`: 待去畸变图像。
- 输入 `CalibrationData`: 标定 JSON，可选但优先级最高。
- 参数 `CalibrationFile`: 当没有输入端口时，从文件读取 JSON。
- 输出 `Image`: 去畸变后的图像。

### 4.2 关键参数怎么调

- 当前唯一显式参数是 `CalibrationFile`。
- 真正决定效果的是上游 `CameraMatrix` 与 `DistCoeffs` 是否可靠。

### 4.3 与教材口径的差别

- 当前实现更关注 JSON 兼容解析，而不是复杂相机模型控制。
- 如果 `DistCoeffs` 缺失，源码可能按空数组继续执行，这在工程上是兼容策略，但也意味着你要自己确认数据完整性。

## 5. 推荐使用链路与调参建议

推荐链路：

```text
CameraCalibration
  -> Undistort
  -> PerspectiveTransform / 测量 / 匹配
```

调参建议：

- 先确认标定结果 JSON 结构是否正确。
- 去畸变后再做平面透视拉正，通常更符合几何逻辑。
- 如果当前镜头畸变很轻，去畸变视觉上变化可能不大，但对测量仍可能有价值。

## 6. 这个算子的边界

- 它不是重新标定，只是消费已有标定结果。
- 它当前只覆盖标准 pinhole 模型的常见用法。
- 它不会自动保证当前图像尺寸与标定时尺寸完全一致。

## 7. 失败案例与常见误区

### 案例1：没有标定数据却想去畸变

这是最基本的前提缺失，算法本身无从计算。

### 案例2：把去畸变当成透视矫正

去畸变并不会自动把斜着拍的标签纸变成长方形正视图。

### 案例3：标定数据格式不一致

当前实现虽然做了兼容，但仍应尽量统一上游 JSON 结构。

## 8. 专业来源与延伸阅读

- 本仓库: `UndistortOperator.cs`
- OpenCV 官方文档: `undistort`
- Zhang 标定方法及相机模型相关资料
- 与 [19-CameraCalibration-技术笔记](./19-CameraCalibration-技术笔记.md) 和 [18-PerspectiveTransform-技术笔记](./18-PerspectiveTransform-技术笔记.md) 对照阅读

## 9. 一句话总结

`Undistort` 是把“标定结果”真正落到图像上的一步，它修的是镜头畸变，不是透视关系，也不是测量逻辑本身。
