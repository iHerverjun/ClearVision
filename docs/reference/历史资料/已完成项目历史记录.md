# ClearVision 已完成项目历史记录

> **文档合并日期**: 2026-03-16  
> **合并范围**: Phase 1-5 开发指南、算子库阶段评审、历史 TODO 归档  
> **原始文件**: 已归档至 `docs/archive/legacy_md_pre_merge_20260316/`

---

## 目录

1. [项目概述](#一项目概述)
2. [Phase 1：关键能力补齐](#二phase-1关键能力补齐)
3. [Phase 2：测试基础设施建设](#三phase-2测试基础设施建设)
4. [Phase 3：算法深度提升](#四phase-3算法深度提升与遗留问题修复)
5. [Phase 4：生产就绪性提升](#五phase-4生产就绪性提升)
6. [Phase 5：前端增强与端到端测试](#六phase-5前端增强与端到端测试)
7. [算子库阶段评审](#七算子库阶段评审)
8. [历史任务归档](#八历史任务归档)
9. [??????](#???????2026-03-16)

---

## ?????????2026-03-16?

> ????????????? `docs/` ???????????????????????

### 9.1 2026-03-04 Bug ????

- ?????`BUG_AUDIT_2026-03-04.md`
- ??????? 2026-03-04 ??????????????UI E2E ????????? 9 ??????
- ???????????? [CURRENT_BUG_ARCH_AUDIT_2026-03-12](c:/Users/11234/Desktop/ClearVision/docs/CURRENT_BUG_ARCH_AUDIT_2026-03-12.md) ?????????????????

### 9.2 2026-03-12 Bug ????

- ?????`BUG_FIX_PLAN_2026-03-12.md`
- ?????? 2026-03-12 ??????? P1/P2/P3 ?????????? WebMessage ????MQTT ???FlowCanvas???????????????????
- ??????????????????????????????????????

### 9.3 2026-02-28 ??????

- ?????`performance_audit_2026-02-28.md`
- ????????????????????????????????????????ONNX ?????????????? PNG/Base64/Clone ????
- ????????????????????????????????????????????????

### 9.4 AI ?????????

- ?????`quality_ai_evolution_todo.md`
- ???????? Prompt ???????????????????????????????????????? Sprint A/B ???
- ???????????????????????????????????????????????

### 9.5 ???????

- ?????`TODO_OperatorLibrary_AlgorithmAudit.md`
  - ???????? 7 ??????????????????? `docs/AlgorithmAudit/`?
- ?????`TODO_OperatorLibrary_Management.md`
  - ????????????? Attribute??????NuGet ?????????????????????????? `docs/operators/` ???????

---

## 一、项目概述

### 1.1 项目简介

**ClearVision** 是一款简化版工业视觉检测软件，参考海康 VisionMaster 设计，支持：

- 🔄 **算子流水线** - 可视化图像处理流程编排
- 🔍 **缺陷检测** - 基于机器学习的质量检测
- 📊 **结果分析** - 检测数据统计与报表
- 🎥 **多相机支持** - 工业相机图像采集

### 1.2 核心功能模块

1. **项目管理** - 工程文件的创建、保存、加载
2. **算子库** - 118+ 常用图像处理算子
3. **流程编辑器** - 拖拽式算子流程设计
4. **图像显示** - 实时预览与结果标注
5. **相机管理** - 工业相机配置与采集
6. **检测结果** - NG/OK 判定与数据记录

### 1.3 技术栈

| 层级 | 技术 |
|------|------|
| 后端框架 | .NET 8 + Minimal APIs |
| 前端 | WebView2 + 原生 ES6 模块 |
| 图像处理 | OpenCvSharp4 |
| 数据库 | SQLite + Entity Framework Core 8 |
| AI 推理 | ONNX Runtime + PaddleOCRSharp |
| PLC 通信 | 自研 Acme.PlcComm（S7/MC/FINS） |

### 1.4 项目统计（截至 2026-03-06）

| 指标 | 数值 |
|------|------|
| C# 源代码 | 12,389 行（111 文件） |
| JavaScript | 5,535 行（34 文件） |
| CSS | 6 文件 |
| 单元测试 | 658+ 个（100% 通过率） |
| 算子总数 | 118 个 |
| 文档补全率 | 45.76%（54/118 已完成详细文档） |

### 1.5 阶段完成总览

| 阶段 | 状态 | 完成度 |
|------|------|--------|
| Phase 1：关键能力补齐 | ✅ 已完成 | 100% |
| Phase 2：测试基础设施 | ✅ 已完成 | 100% |
| Phase 3：算法深度提升 | ✅ 已完成 | 100% |
| Phase 4：生产就绪性 | ✅ 已完成 | 100% |
| Phase 5：前端增强 | ✅ 已完成 | 100% |

---

## 二、Phase 1：关键能力补齐

> **时间**: 2026-01 ~ 2026-02  
> **目标**: 补齐 5 个工业视觉检测核心算子  
> **来源文档**: `TODO_Phase1_DevGuide.md`, `TODO_Phase1_Fix.md`

### 2.1 已完成算子清单

| 顺序 | 算子 | 枚举值 | 难度 | 状态 |
|-----:|------|:------:|:----:|:----:|
| 1 | GeometricFitting（几何拟合） | 41 | ⭐⭐ | ✅ 已完成 |
| 2 | RoiManager（ROI管理器） | 42 | ⭐⭐ | ✅ 已完成 |
| 3 | ShapeMatching（形状匹配） | 43 | ⭐⭐⭐⭐ | ✅ 已完成 |
| 4 | SubpixelEdgeDetection（亚像素边缘） | 44 | ⭐⭐⭐ | ✅ 已完成 |
| 5 | CameraCalibration（相机标定改进） | 24(已有) | ⭐⭐⭐ | ✅ 已完成 |

### 2.2 几何拟合 (GeometricFitting)

**功能**: 从轮廓点拟合直线、圆或椭圆

**核心算法**：

```csharp
private (double cx, double cy, double r) FitCircleLeastSquares(Point2f[] points)
{
    int n = points.Length;
    double sumX = 0, sumY = 0, sumX2 = 0, sumY2 = 0, sumXY = 0;
    double sumX3 = 0, sumY3 = 0, sumX2Y = 0, sumXY2 = 0;

    for (int i = 0; i < n; i++)
    {
        double x = points[i].X, y = points[i].Y;
        sumX += x; sumY += y;
        sumX2 += x * x; sumY2 += y * y; sumXY += x * y;
        sumX3 += x * x * x; sumY3 += y * y * y;
        sumX2Y += x * x * y; sumXY2 += x * y * y;
    }

    double A = n * sumX2 - sumX * sumX;
    double B = n * sumXY - sumX * sumY;
    double C = n * sumY2 - sumY * sumY;
    double D = 0.5 * (n * sumX3 + n * sumXY2 - sumX * sumX2 - sumX * sumY2);
    double E = 0.5 * (n * sumX2Y + n * sumY3 - sumY * sumX2 - sumY * sumY2);

    double det = A * C - B * B;
    if (Math.Abs(det) < 1e-10) return (0, 0, 0);

    double cx = (D * C - B * E) / det;
    double cy = (A * E - B * D) / det;
    double r = Math.Sqrt(sumX2 / n - 2 * cx * sumX / n + cx * cx
                       + sumY2 / n - 2 * cy * sumY / n + cy * cy);
    return (cx, cy, r);
}
```

### 2.3 ROI 管理器 (RoiManager)

**功能**: 矩形/圆形/多边形区域选择

**支持的形状和操作**：

| 形状 | 操作 | 说明 |
|------|------|------|
| Rectangle | Crop/Mask | 矩形区域裁剪或掩膜 |
| Circle | Crop/Mask | 圆形区域裁剪或掩膜 |
| Polygon | Crop/Mask | 多边形区域裁剪或掩膜 |

### 2.4 形状匹配 (ShapeMatching)

**功能**: 旋转/缩放不变的高级模板匹配

**算法设计** — 基于金字塔的多角度模板匹配：

```
=== 阶段一: 准备 ===
1. 获取模板: 优先从 Template 输入端口，否则从 TemplatePath 参数加载文件
2. 转灰度: src_gray, tmpl_gray

=== 阶段二: 构建旋转模板集 ===
3. angles = [AngleStart, AngleStart+AngleStep, ..., AngleStart+AngleExtent]
4. 对每个 angle:
   - rotMatrix = Cv2.GetRotationMatrix2D(center, angle, 1.0)
   - rotatedTmpl = Cv2.WarpAffine(tmpl_gray, rotMatrix, size)
   - 存入 Dictionary<double, Mat>

=== 阶段三: 金字塔粗搜索 ===
5. 在最顶层金字塔（最小分辨率）上对每个角度做 MatchTemplate
6. 初筛得分 > MinScore * 0.8 的候选 (angle, x, y)

=== 阶段四: 精搜索 ===
7. 在原图上对候选区域做 MatchTemplate 精定位
8. NMS 去除重叠匹配（IoU > 0.5 的保留得分最高者）
9. 取前 MaxMatches 个结果
```

### 2.5 亚像素边缘提取 (SubpixelEdgeDetection)

**功能**: 高精度亚像素级边缘提取

**算法设计** — 基于梯度法向插值的亚像素定位：

```
1. 灰度 → GaussianBlur(sigma) → Canny(low, high) → FindContours
2. 对每个轮廓中的每个像素级边缘点 P(x,y):
   a. 计算梯度方向: dx = Sobel_X(x,y), dy = Sobel_Y(x,y)
   b. 沿梯度方向取 3 个采样值:
      g_neg = 双线性插值(x - dx_norm, y - dy_norm)
      g_cur = gray(x, y)
      g_pos = 双线性插值(x + dx_norm, y + dy_norm)
   c. 抛物线拟合求亚像素偏移 offset:
      offset = 0.5 * (g_neg - g_pos) / (g_neg - 2*g_cur + g_pos)
   d. 亚像素坐标:
      subpixel_x = x + offset * dx_norm
      subpixel_y = y + offset * dy_norm
3. 输出 Point2f[] 坐标数组
```

### 2.6 Phase 1 修复指令执行记录

**DI 注册完成**：

```csharp
// DependencyInjection.cs
// Phase 1 关键能力补齐
services.AddSingleton<IOperatorExecutor, GeometricFittingOperator>();
services.AddSingleton<IOperatorExecutor, RoiManagerOperator>();
services.AddSingleton<IOperatorExecutor, ShapeMatchingOperator>();
services.AddSingleton<IOperatorExecutor, SubpixelEdgeDetectionOperator>();
```

**工厂元数据注册完成**：
- ✅ GeometricFitting 元数据（拟合类型、阈值、最小面积、最少点数）
- ✅ RoiManager 元数据（形状、操作、坐标参数）
- ✅ ShapeMatching 元数据（模板路径、匹配分数、角度范围、金字塔层数）
- ✅ SubpixelEdgeDetection 元数据（阈值、Sigma、亚像素方法）

**单元测试创建完成**：
- ✅ `GeometricFittingOperatorTests.cs`
- ✅ `RoiManagerOperatorTests.cs`
- ✅ `ShapeMatchingOperatorTests.cs`
- ✅ `SubpixelEdgeDetectionOperatorTests.cs`

### 2.7 完成证据

- Phase1 涉及的核心算子实现已存在：
  - `Acme.Product/src/Acme.Product.Infrastructure/Operators/GeometricFittingOperator.cs`
  - `Acme.Product/src/Acme.Product.Infrastructure/Operators/RoiManagerOperator.cs`
  - `Acme.Product/src/Acme.Product.Infrastructure/Operators/ShapeMatchingOperator.cs`
  - `Acme.Product/src/Acme.Product.Infrastructure/Operators/SubpixelEdgeDetectionOperator.cs`
- 对应测试文件已存在并全量通过

---

## 三、Phase 2：测试基础设施建设

> **时间**: 2026-02  
> **目标**: 为所有 39+ 个算子建立系统化的测试覆盖  
> **来源文档**: `TODO_Phase2_DevGuide.md`

### 3.1 测试基础设施现状

#### 已有测试文件（Phase 1 之前）

- `OperatorTests.cs` — 4 个基础算子
- `DeepLearningOperatorTests.cs` — 深度学习算子
- `GeometricFittingOperatorTests.cs` — 几何拟合
- `RoiManagerOperatorTests.cs` — ROI管理器
- `ShapeMatchingOperatorTests.cs` — 形状匹配
- `SubpixelEdgeDetectionOperatorTests.cs` — 亚像素边缘
- 5 个集成测试文件

#### 缺失测试的算子 (约 29 个)

| 类别 | 缺失算子 |
|------|---------|
| 滤波 | MedianBlur, BilateralFilter |
| 几何变换 | ImageResize, ImageCrop, ImageRotate, PerspectiveTransform |
| 形态学 | Morphology |
| 检测 | BlobDetection, FindContours, TemplateMatch, CodeRecognition |
| 测量 | MeasureDistance, CircleMeasurement, LineMeasurement, ContourMeasurement, AngleMeasurement, GeometricTolerance |
| 标定 | CameraCalibration, Undistort, CoordinateTransform |
| 通信 | ModbusCommunication, TcpCommunication, DatabaseWrite |
| 流程控制 | ConditionalBranch |
| 预处理 | ColorConversion, AdaptiveThreshold, HistogramEqualization |
| 输出 | ResultOutput |

### 3.2 测试开发规范

#### 文件位置

所有测试放在 `tests\Acme.Product.Tests\Operators\` 目录下。

#### 命名约定

- 文件名: `{算子名}OperatorTests.cs`
- 类名: `{算子名}OperatorTests`
- 方法名: `{Method}_With{Condition}_Should{Expected}`

#### 测试图像创建辅助方法（TestHelpers.cs）

```csharp
public static class TestHelpers
{
    public static ImageWrapper CreateTestImage(int width = 200, int height = 200, Scalar? color = null)
    {
        var c = color ?? new Scalar(128, 128, 128);
        using var mat = new Mat(height, width, MatType.CV_8UC3, c);
        return new ImageWrapper(mat);
    }

    public static ImageWrapper CreateShapeTestImage()
    {
        using var mat = new Mat(400, 400, MatType.CV_8UC3, Scalar.Black);
        Cv2.Rectangle(mat, new Rect(50, 50, 100, 100), Scalar.White, -1);
        Cv2.Circle(mat, new Point(300, 200), 60, Scalar.White, -1);
        return new ImageWrapper(mat);
    }

    public static ImageWrapper CreateGradientTestImage()
    {
        using var mat = new Mat(200, 200, MatType.CV_8UC3);
        for (int y = 0; y < 200; y++)
            for (int x = 0; x < 200; x++)
            {
                byte val = (byte)(x * 255 / 200);
                mat.Set(y, x, new Vec3b(val, val, val));
            }
        return new ImageWrapper(mat);
    }
}
```

### 3.3 测试分组完成情况

#### 第一组：图像处理基础算子（9个）✅ 已完成

| 文件名 | 算子类 | 状态 |
|--------|-------|:----:|
| `MedianBlurOperatorTests.cs` | MedianBlurOperator | ✅ |
| `BilateralFilterOperatorTests.cs` | BilateralFilterOperator | ✅ |
| `ImageResizeOperatorTests.cs` | ImageResizeOperator | ✅ |
| `ImageCropOperatorTests.cs` | ImageCropOperator | ✅ |
| `ImageRotateOperatorTests.cs` | ImageRotateOperator | ✅ |
| `PerspectiveTransformOperatorTests.cs` | PerspectiveTransformOperator | ✅ |
| `MorphologyOperatorTests.cs` | MorphologyOperator | ✅ |
| `ColorConversionOperatorTests.cs` | ColorConversionOperator | ✅ |
| `AdaptiveThresholdOperatorTests.cs` | AdaptiveThresholdOperator | ✅ |

#### 第二组：检测与测量算子（10个）✅ 已完成

| 文件名 | 算子类 | 状态 |
|--------|-------|:----:|
| `BlobDetectionOperatorTests.cs` | BlobDetectionOperator | ✅ |
| `FindContoursOperatorTests.cs` | FindContoursOperator | ✅ |
| `TemplateMatchOperatorTests.cs` | TemplateMatchOperator | ✅ |
| `CodeRecognitionOperatorTests.cs` | CodeRecognitionOperator | ✅ |
| `MeasureDistanceOperatorTests.cs` | MeasureDistanceOperator | ✅ |
| `CircleMeasurementOperatorTests.cs` | CircleMeasurementOperator | ✅ |
| `LineMeasurementOperatorTests.cs` | LineMeasurementOperator | ✅ |
| `ContourMeasurementOperatorTests.cs` | ContourMeasurementOperator | ✅ |
| `AngleMeasurementOperatorTests.cs` | AngleMeasurementOperator | ✅ |
| `GeometricToleranceOperatorTests.cs` | GeometricToleranceOperator | ✅ |

#### 第三组：标定与流程控制算子（5个）✅ 已完成

| 文件名 | 算子类 | 状态 |
|--------|-------|:----:|
| `CameraCalibrationOperatorTests.cs` | CameraCalibrationOperator | ✅ |
| `UndistortOperatorTests.cs` | UndistortOperator | ✅ |
| `CoordinateTransformOperatorTests.cs` | CoordinateTransformOperator | ✅ |
| `ConditionalBranchOperatorTests.cs` | ConditionalBranchOperator | ✅ |
| `ResultOutputOperatorTests.cs` | ResultOutputOperator | ✅ |

#### 第四组：通信算子（仅参数验证）✅ 已完成

| 文件名 | 算子类 | 测试范围 |
|--------|-------|---------|
| `ModbusCommunicationOperatorTests.cs` | ModbusCommunicationOperator | 只测参数验证，不测实际连接 |
| `TcpCommunicationOperatorTests.cs` | TcpCommunicationOperator | 只测参数验证，不测实际连接 |
| `DatabaseWriteOperatorTests.cs` | DatabaseWriteOperator | 只测参数验证和SQL注入防护 |

### 3.4 完成证据

- 文档列出的关键缺失测试已补齐：
  - `MedianBlurOperatorTests.cs`
  - `BilateralFilterOperatorTests.cs`
  - `CoordinateTransformOperatorTests.cs`
  - `ModbusCommunicationOperatorTests.cs`
  - `TcpCommunicationOperatorTests.cs`
  - `DatabaseWriteOperatorTests.cs`
  - `Integration/BasicFlowIntegrationTests.cs`
  - `Integration/ColorDetectionIntegrationTests.cs`
- 全量测试（2026-03-06）：`658` 通过 / `5` 跳过 / `0` 失败

---

## 四、Phase 3：算法深度提升与遗留问题修复

> **时间**: 2026-02  
> **目标**: 修复编译错误、提升算法深度、完善遗留功能  
> **来源文档**: `TODO_Phase3_DevGuide.md`

### 4.1 编译错误修复

**问题定位**: `ProjectService.cs` 第 33, 35 行: `'CreateProjectRequest' 未包含 'Flow' 的定义`

**修复方案**: 
- `ProjectDto.cs` 第 65 行已有 `Flow` 属性
- 检查并删除重复的 `CreateProjectRequest` 定义
- 清理构建缓存：`dotnet clean && dotnet build`

**验证**: `dotnet build` 输出 0 errors ✅

### 4.2 相机标定改进 (CameraCalibration)

**文件**: `CameraCalibrationOperator.cs`（枚举值 24）

**改进点**：

| 现状问题 | 改进方案 |
|---------|---------|
| 只做 `FindChessboardCorners`，不做标定 | 添加 `Cv2.CalibrateCamera` 调用 |
| 单图输入 | 新增 `ImageFolder` 参数，支持文件夹批量读取 |
| 标定结果不持久化 | 保存为 JSON 文件到 `CalibrationOutputPath` |

**新增参数**：

```csharp
new() { Name = "Mode", DisplayName = "模式", DataType = "enum", DefaultValue = "SingleImage",
    Options = new List<ParameterOption>
    {
        new() { Label = "单图检测", Value = "SingleImage" },
        new() { Label = "文件夹标定", Value = "FolderCalibration" }
    }
},
new() { Name = "ImageFolder", DisplayName = "标定图片文件夹", DataType = "string", DefaultValue = "" },
new() { Name = "CalibrationOutputPath", DisplayName = "标定结果保存路径", DataType = "string",
    DefaultValue = "calibration_result.json" },
```

**文件夹标定核心逻辑**：

```csharp
if (mode == "FolderCalibration")
{
    var imageFiles = Directory.GetFiles(imageFolder, "*.png")
        .Concat(Directory.GetFiles(imageFolder, "*.jpg")).ToArray();

    var objectPoints = new List<Mat>();
    var imagePoints = new List<Mat>();
    Size imageSize = default;

    foreach (var file in imageFiles)
    {
        using var img = Cv2.ImRead(file, ImreadModes.Grayscale);
        if (imageSize == default) imageSize = img.Size();

        if (Cv2.FindChessboardCorners(img, patternSize, out var corners))
        {
            Cv2.CornerSubPix(img, corners, new Size(11, 11), new Size(-1, -1),
                new TermCriteria(CriteriaTypes.Eps | CriteriaTypes.MaxIter, 30, 0.001));
            imagePoints.Add(Mat.FromArray(corners));

            var objPts = new Point3f[patternSize.Width * patternSize.Height];
            for (int j = 0; j < patternSize.Height; j++)
                for (int i = 0; i < patternSize.Width; i++)
                    objPts[j * patternSize.Width + i] = new Point3f(i * gridSize, j * gridSize, 0);
            objectPoints.Add(Mat.FromArray(objPts));
        }
    }

    var cameraMatrix = new Mat();
    var distCoeffs = new Mat();
    Cv2.CalibrateCamera(objectPoints, imagePoints, imageSize,
        cameraMatrix, distCoeffs, out _, out _);

    var calibData = new { CameraMatrix = MatToArray(cameraMatrix), DistCoeffs = MatToArray(distCoeffs) };
    File.WriteAllText(outputPath, JsonSerializer.Serialize(calibData, new JsonSerializerOptions { WriteIndented = true }));
}
```

### 4.3 码识别优化 (CodeRecognitionOperator)

**当前问题**: `MatToBitmap()` 中做了 `mat.ToBytes(".png")` 编码，违背零拷贝设计

**优化方案** — 内存直接拷贝代替 PNG 编解码，性能提升约 10-50 倍：

```csharp
private System.Drawing.Bitmap MatToBitmapDirect(Mat mat)
{
    using var bgr = mat.Type() == MatType.CV_8UC1 ? new Mat() : mat;
    if (mat.Type() == MatType.CV_8UC1)
        Cv2.CvtColor(mat, bgr, ColorConversionCodes.GRAY2BGR);
    
    var bitmap = new System.Drawing.Bitmap(
        bgr.Width, bgr.Height,
        System.Drawing.Imaging.PixelFormat.Format24bppRgb);
    
    var bmpData = bitmap.LockBits(
        new System.Drawing.Rectangle(0, 0, bitmap.Width, bitmap.Height),
        System.Drawing.Imaging.ImageLockMode.WriteOnly,
        System.Drawing.Imaging.PixelFormat.Format24bppRgb);
    
    try
    {
        if (bgr.IsContinuous() && bmpData.Stride == bgr.Step())
        {
            unsafe
            {
                Buffer.MemoryCopy(
                    bgr.DataPointer.ToPointer(),
                    bmpData.Scan0.ToPointer(),
                    (long)bmpData.Stride * bmpData.Height,
                    (long)bgr.Step() * bgr.Height);
            }
        }
        else
        {
            for (int y = 0; y < bgr.Height; y++)
            {
                unsafe
                {
                    Buffer.MemoryCopy(
                        (byte*)bgr.DataPointer + y * bgr.Step(),
                        (byte*)bmpData.Scan0 + y * bmpData.Stride,
                        bmpData.Stride,
                        Math.Min((int)bgr.Step(), bmpData.Stride));
                }
            }
        }
    }
    finally
    {
        bitmap.UnlockBits(bmpData);
    }
    
    return bitmap;
}
```

**多码识别支持**：

```csharp
// 将 Decode() 替换为 DecodeMultiple()
var results = reader.DecodeMultiple(luminanceSource);
if (results != null && results.Length > 0)
{
    var codeResults = results.Select((r, i) => new Dictionary<string, object>
    {
        { "Index", i },
        { "Text", r.Text },
        { "Format", r.BarcodeFormat.ToString() },
        { "Points", r.ResultPoints?.Select(p => new { X = p.X, Y = p.Y }).ToArray() ?? Array.Empty<object>() }
    }).ToList();
    
    additionalData["Codes"] = codeResults;
    additionalData["CodeCount"] = codeResults.Count;
    additionalData["Text"] = results[0].Text; // 保持向后兼容
}
```

### 4.4 深度学习扩展 — 自定义类别标签

**当前问题**: COCO 80 类硬编码，用户自定义模型无法显示正确标签

**新增参数**：

```csharp
new() { Name = "LabelFile", DisplayName = "标签文件路径", DataType = "file", DefaultValue = "" },
```

**标签加载逻辑**：

```csharp
var labelFile = GetStringParam(@operator, "LabelFile", "");
string[] labels;

if (!string.IsNullOrEmpty(labelFile) && File.Exists(labelFile))
{
    labels = File.ReadAllLines(labelFile)
        .Where(l => !string.IsNullOrWhiteSpace(l))
        .ToArray();
    _logger.LogInformation("加载自定义标签文件: {File}, 共 {Count} 个标签", labelFile, labels.Length);
}
else
{
    // 回退到 COCO 80 类默认标签
    labels = GetCocoLabels();
}

// 同时在模型目录查找标签
if (string.IsNullOrEmpty(labelFile))
{
    var modelDir = Path.GetDirectoryName(modelPath);
    var autoLabelFile = Path.Combine(modelDir ?? "", "labels.txt");
    if (File.Exists(autoLabelFile))
    {
        labels = File.ReadAllLines(autoLabelFile)
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .ToArray();
        _logger.LogInformation("自动发现标签文件: {File}", autoLabelFile);
    }
}
```

### 4.5 新增算子

#### 4.5.1 颜色检测算子 (ColorDetection)

- **枚举**: `ColorDetection = 45`
- **文件**: `ColorDetectionOperator.cs`
- **功能**: 基于 HSV/Lab 空间的颜色分析与分级
- **分析模式**: Average（平均色）、Dominant（主色提取）、Range（颜色范围检测）

#### 4.5.2 串口通信算子 (SerialCommunication)

- **枚举**: `SerialCommunication = 46`
- **文件**: `SerialCommunicationOperator.cs`
- **功能**: RS-232/485 串口数据收发
- **参数**: PortName, BaudRate, DataBits, StopBits, Parity, SendData, Encoding, TimeoutMs

### 4.6 完成证据

- `CreateProjectRequest` 类型在 `ProjectDto.cs` 中存在，构建基线正常
- `CameraCalibrationOperator` 已存在并支持文档中提到的关键参数：Mode, ImageFolder, CalibrationOutputPath
- `CodeRecognitionOperator` 已实现并可参与全量测试
- `ColorDetectionOperator` 和 `SerialCommunicationOperator` 已实现并注册

---

## 五、Phase 4：生产就绪性提升

> **时间**: 2026-02  
> **目标**: 修复遗留编译问题、完善通信健壮性、提升系统稳定性  
> **来源文档**: `TODO_Phase4_DevGuide.md`

### 5.1 修复全量构建错误

**最高优先级任务**：确保 `dotnet build` 全量 0 errors

**验证命令**：

```powershell
cd c:\Users\11234\Desktop\ClearVision\Acme.Product
dotnet clean
dotnet build
```

**验证结果**: ✅ 0 errors

### 5.2 通信算子健壮性提升

#### 5.2.1 Modbus 连接池与重连

**当前问题**: 每次执行新建 TCP 连接，无重连机制

**解决方案** — 添加静态连接缓存：

```csharp
private static readonly ConcurrentDictionary<string, TcpClient> _connectionPool = new();
private static readonly ConcurrentDictionary<string, SemaphoreSlim> _connectionLocks = new();

private async Task<TcpClient> GetOrCreateConnection(string host, int port, int timeoutMs, CancellationToken ct)
{
    var key = $"{host}:{port}";
    var lockObj = _connectionLocks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
    
    await lockObj.WaitAsync(ct);
    try
    {
        if (_connectionPool.TryGetValue(key, out var existing) && existing.Connected)
            return existing;
        
        // 清理旧连接
        if (existing != null)
        {
            try { existing.Close(); } catch { }
            _connectionPool.TryRemove(key, out _);
        }
        
        // 建立新连接
        var client = new TcpClient();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeoutMs);
        await client.ConnectAsync(host, port, cts.Token);
        
        _connectionPool[key] = client;
        _logger.LogInformation("Modbus 连接已建立: {Key}", key);
        return client;
    }
    finally
    {
        lockObj.Release();
    }
}
```

#### 5.2.2 TCP 通信连接池

同上模式，为 TCP 通信也添加连接池。参照 ModbusCommunicationOperator 的实现即可。

#### 5.2.3 心跳检测

```csharp
private bool IsConnectionAlive(TcpClient client)
{
    try
    {
        if (!client.Connected) return false;
        return !(client.Client.Poll(1, SelectMode.SelectRead) && client.Client.Available == 0);
    }
    catch
    {
        return false;
    }
}
```

### 5.3 FlowExecutionService 增强

#### 5.3.1 执行超时保护

```csharp
using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
timeoutCts.CancelAfter(TimeSpan.FromSeconds(30)); // 默认 30 秒超时

try
{
    result = await executor.ExecuteAsync(@operator, inputs, timeoutCts.Token);
}
catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
{
    result = OperatorExecutionOutput.Failure($"算子 '{@operator.Name}' 执行超时 (30秒)");
}
```

### 5.4 日志规范化

#### 5.4.1 为所有算子添加结构化日志

确认每个算子的 `ExecuteCoreAsync` 开头都有参数日志：

```csharp
_logger.LogInformation("{OperatorName} 开始执行, 参数: FitType={FitType}, Threshold={Threshold}",
    nameof(GeometricFittingOperator), fitType, threshold);
```

#### 5.4.2 统一错误日志格式

确认所有 `catch` 块都使用 `_logger.LogError(ex, ...)` 而非 `Console.WriteLine`。

**检查命令**：

```powershell
Get-ChildItem -Path src\Acme.Product.Infrastructure\Operators -Include *.cs -Recurse | Select-String "Console.Write"
```

**验证结果**: ✅ 无 `Console.Write` 残留

### 5.5 完成证据

- 连接池/重连相关实现已在位：
  - `PlcCommunicationOperatorBase.cs`
  - `ModbusCommunicationOperator.cs`
  - `TcpCommunicationOperator.cs`
- 协议层健壮性代码在位：
  - `Acme.PlcComm/Core/PlcBaseClient.cs`（`ReadExactAsync`、重连策略上限）
  - `Acme.PlcComm/Siemens/S7AddressParser.cs`（支持 `MW`/`MB`/`MD` 风格）
  - `Acme.PlcComm/Siemens/SiemensS7Client.cs`（位读写与 `MW0` 心跳路径）
  - `Acme.PlcComm/Omron/FinsFrameBuilder.cs`（位写长度语义分支）
- PLC 子集测试（2026-03-06）：`18` 通过 / `0` 失败

---

## 六、Phase 5：前端增强与端到端测试

> **时间**: 2026-02  
> **目标**: 提升前端体验、补充集成测试、确保全量构建 0 errors  
> **来源文档**: `TODO_Phase5_DevGuide.md`

### 6.1 前端属性面板 — file 类型参数支持

**实现位置**: `src\Acme.Product.Desktop\wwwroot\src\features\flow-editor\propertyPanel.js`

**实现代码**：

```javascript
if (param.dataType === 'file' || param.dataType === 'folder') {
    const container = document.createElement('div');
    container.className = 'param-file-group';
    container.style.display = 'flex';
    container.style.gap = '4px';
    
    const input = document.createElement('input');
    input.type = 'text';
    input.className = 'param-input';
    input.value = param.value || param.defaultValue || '';
    input.placeholder = param.dataType === 'folder' ? '选择文件夹...' : '选择文件...';
    input.readOnly = true;
    input.style.flex = '1';
    
    const btn = document.createElement('button');
    btn.className = 'btn-outline btn-sm';
    btn.textContent = '浏览';
    btn.onclick = async () => {
        const command = param.dataType === 'folder' 
            ? 'PickFolderCommand' 
            : 'PickFileCommand';
        
        try {
            const result = await window.chrome.webview.hostObjects.bridge.SendCommand(
                JSON.stringify({ Command: command, Parameters: {} })
            );
            const parsed = JSON.parse(result);
            if (parsed && parsed.FilePath) {
                input.value = parsed.FilePath;
                onParameterChanged(param.name, parsed.FilePath);
            }
        } catch (e) {
            console.error('文件选择失败:', e);
        }
    };
    
    container.appendChild(input);
    container.appendChild(btn);
    return container;
}
```

**样式**：

```css
.param-file-group {
    display: flex;
    gap: 4px;
    align-items: center;
}
.param-file-group .param-input {
    flex: 1;
    cursor: pointer;
}
.param-file-group .btn-outline {
    white-space: nowrap;
    padding: 4px 8px;
    font-size: 12px;
}
```

### 6.2 算子图标扩展

**实现位置**: `operatorLibrary.js`

```javascript
const iconMap = {
    // ... 已有映射 ...
    'color': '🎨',        // ColorDetection
    'serial': '🔌',       // SerialCommunication
    'fitting': '📐',      // GeometricFitting
    'roi': '⬜',          // RoiManager
    'shape': '🔍',        // ShapeMatching
    'subpixel': '🎯',    // SubpixelEdgeDetection
};
```

### 6.3 端到端集成测试

#### 6.3.1 基础流程测试

**文件**: `tests\Acme.Product.Tests\Integration\BasicFlowIntegrationTests.cs`

```csharp
public class BasicFlowIntegrationTests
{
    [Fact]
    public async Task GaussianBlur_Then_Threshold_ShouldProduceOutput()
    {
        var blurOp = new Operator("高斯模糊", OperatorType.GaussianBlur, 0, 0);
        var threshOp = new Operator("阈值", OperatorType.Threshold, 200, 0);
        
        var blurExecutor = new GaussianBlurOperator(new Mock<ILogger<GaussianBlurOperator>>().Object);
        var threshExecutor = new ThresholdOperator(new Mock<ILogger<ThresholdOperator>>().Object);
        
        using var testImage = TestHelpers.CreateGradientTestImage();
        var inputs = new Dictionary<string, object> { { "Image", testImage } };
        
        var blurResult = await blurExecutor.ExecuteAsync(blurOp, inputs);
        blurResult.IsSuccess.Should().BeTrue("高斯模糊应成功");
        
        var threshResult = await threshExecutor.ExecuteAsync(threshOp, blurResult.OutputData);
        threshResult.IsSuccess.Should().BeTrue("阈值处理应成功");
        threshResult.OutputData.Should().ContainKey("Image");
    }

    [Fact]
    public async Task ColorConversion_Then_AdaptiveThreshold_ShouldWork()
    {
        var colorOp = new Operator("颜色转换", OperatorType.ColorConversion, 0, 0);
        var atOp = new Operator("自适应阈值", OperatorType.AdaptiveThreshold, 200, 0);
        
        var colorExec = new ColorConversionOperator(new Mock<ILogger<ColorConversionOperator>>().Object);
        var atExec = new AdaptiveThresholdOperator(new Mock<ILogger<AdaptiveThresholdOperator>>().Object);
        
        using var testImage = TestHelpers.CreateShapeTestImage();
        var inputs = new Dictionary<string, object> { { "Image", testImage } };
        
        var r1 = await colorExec.ExecuteAsync(colorOp, inputs);
        r1.IsSuccess.Should().BeTrue();
        
        var r2 = await atExec.ExecuteAsync(atOp, r1.OutputData);
        r2.IsSuccess.Should().BeTrue();
    }
}
```

#### 6.3.2 颜色检测流程测试

```csharp
public class ColorDetectionIntegrationTests
{
    [Fact]
    public async Task ColorDetection_AverageMode_ShouldReturnColorValues()
    {
        var op = new Operator("颜色检测", OperatorType.ColorDetection, 0, 0);
        var executor = new ColorDetectionOperator(new Mock<ILogger<ColorDetectionOperator>>().Object);
        
        // 纯红色图像
        using var redImage = TestHelpers.CreateTestImage(color: new OpenCvSharp.Scalar(0, 0, 255));
        var inputs = new Dictionary<string, object> { { "Image", redImage } };
        
        var result = await executor.ExecuteAsync(op, inputs);
        result.IsSuccess.Should().BeTrue();
    }
}
```

### 6.4 性能基准测试

```csharp
public class OperatorPerformanceTests
{
    [Fact]
    public async Task GaussianBlur_ShouldComplete_Within100ms()
    {
        var op = new Operator("高斯模糊", OperatorType.GaussianBlur, 0, 0);
        var executor = new GaussianBlurOperator(new Mock<ILogger<GaussianBlurOperator>>().Object);
        using var testImage = TestHelpers.CreateTestImage(1920, 1080); // 1080p
        var inputs = new Dictionary<string, object> { { "Image", testImage } };
        
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var result = await executor.ExecuteAsync(op, inputs);
        sw.Stop();
        
        result.IsSuccess.Should().BeTrue();
        sw.ElapsedMilliseconds.Should().BeLessThan(100, "1080p 高斯模糊应在 100ms 内完成");
    }
}
```

### 6.5 构建验证

```powershell
cd c:\Users\11234\Desktop\ClearVision\Acme.Product

# 全量构建
dotnet build

# 全量测试
dotnet test --verbosity normal
```

**验证结果**: ✅ 0 errors, 全量测试通过

### 6.6 完成证据

- 前端属性面板存在 `file` 参数支持：
  - `Acme.Product/src/Acme.Product.Desktop/wwwroot/src/features/flow-editor/propertyPanel.js`（`case 'file'`）
  - `PickFileCommand` 调用链路已在位
- 流程集成测试在位：
  - `Acme.Product/tests/Acme.Product.Tests/Integration/BasicFlowIntegrationTests.cs`
  - `Acme.Product/tests/Acme.Product.Tests/Integration/ColorDetectionIntegrationTests.cs`
- 算子图标与前端映射已扩展：
  - `app.js`
  - `features/operator-library/operatorLibrary.js`

---

## 七、算子库阶段评审

> **日期**: 2026-02-26  
> **范围**: `docs/operators` 文档清理复审  
> **来源文档**: `OperatorLibrary_StageReview_2026-02-26.md`

### 7.1 量化结果

| 指标 | 结果 |
|------|------|
| 算子文档总数（不含 `_TEMPLATE.md`、`CATALOG.md`） | 118 |
| 已完成补全文档（无 `TODO/O(?)/~?ms`） | 54 |
| 仍含占位符文档 | 64 |
| 当前补全率 | 45.76% |
| `catalog.json` 算子总数 / 唯一 ID 数 | 118 / 118 |
| `catalog.json` 中 `docPath` 缺失 | 0 |

### 7.2 已完成批次复核

| 批次 | 文档数 | 复核状态 |
|------|------|:--------:|
| Phase 2.3 P0 | 13 | ✅ 通过 |
| Phase 2.3 P1 | 16 | ✅ 通过 |
| Phase 2.3 P2 | 25 | ✅ 通过 |
| **合计** | **54** | ✅ **通过** |

**复核结论**：`P0/P1/P2` 已补全文档中，`实现策略/API链/性能/场景/限制` 与 `0.2.0` 变更记录完整，且未发现占位符残留。

### 7.3 剩余清理盘点（64）

按类别统计（Top）：

| 类别 | 剩余数量 |
|------|:--------:|
| `预处理` | 11 |
| `检测` | 7 |
| `定位` | 7 |
| `数据处理` | 7 |
| `逻辑工具` | 3 |
| `匹配定位` | 4 |
| `图像处理` | 3 |
| `Preprocessing` | 2 |
| `识别` | 2 |

**通信与流程相关剩余项**：`0`（上一轮剩余 8 项已全部补齐）

### 7.4 复审发现

1. 文档清理阶段已形成稳定节奏：每批次补全后可通过统一占位符扫描快速验收
2. `catalog.json` 类别命名存在中英文混用（如 `预处理` 与 `Preprocessing` 并存），影响统计聚合可读性
3. Phase 2.2 的"人工补充"总项仍未全量完成（因仍有 64 份文档未补齐），当前状态与事实一致

### 7.5 建议下一步（阶段后续）

1. 通信与流程收口已完成，下一步转入 `预处理/检测/定位/数据处理` 四类高存量分批推进（建议每批 10-15 份）
2. 针对仍含占位符的 `逻辑工具/匹配定位/图像处理` 做小批次穿插清理，降低尾项积压
3. 在 Phase 4/5 前增加一次类别规范化清理（统一 `Category` 命名规范），避免目录统计分裂

---

## 八、历史任务归档

> **归档日期**: 2026-03-06  
> **来源文档**: `TODO_Completed_Archive_2026-03-06.md`, `TODO_Old.md`
| `BUG_AUDIT_2026-03-04.md` | ??? | 2026-03-04 ?????? |
| `BUG_FIX_PLAN_2026-03-12.md` | ??? | 2026-03-12 ?????? |
| `performance_audit_2026-02-28.md` | ??? | ??????????? |
| `quality_ai_evolution_todo.md` | ??? | AI ??????????? |
| `TODO_OperatorLibrary_AlgorithmAudit.md` | ??? | ?????????? |
| `TODO_OperatorLibrary_Management.md` | ??? | ??????????? |

### 8.1 归档目的

将"时间较早、已实际完成、但原文状态不一致"的 TODO 文档统一归档，便于后续只追踪真实待办。

**归档策略**：
1. 保留原文件，不删除，确保可追溯
2. 本文件只记录"已完成归档"的结论与证据索引
3. 证据以代码文件存在、测试通过、构建通过为准

### 8.2 已归档阶段

#### A. Phase 1 能力补齐

- **来源文档**: `TODO_Phase1_DevGuide.md`, `TODO_Phase1_Fix.md`
- **归档状态**: ✅ 已完成（归档）
- **完成依据**: 核心算子实现与测试文件已存在

#### B. Phase 2 测试基础设施

- **来源文档**: `TODO_Phase2_DevGuide.md`
- **归档状态**: ✅ 已完成（归档）
- **完成依据**: 关键缺失测试已补齐，全量测试 `658` 通过

#### C. Phase 3 算法与遗留修复

- **来源文档**: `TODO_Phase3_DevGuide.md`
- **归档状态**: ✅ 已完成（归档）
- **完成依据**: `CreateProjectRequest` 类型正常，关键算子已实现

#### D. Phase 4 生产就绪增强

- **来源文档**: `TODO_Phase4_DevGuide.md`
- **归档状态**: ✅ 已完成（归档）
- **完成依据**: 连接池/重连实现已在位，PLC 子集测试通过

#### E. Phase 5 前端增强与端到端

- **来源文档**: `TODO_Phase5_DevGuide.md`
- **归档状态**: ✅ 已完成（归档）
- **完成依据**: 前端 `file` 参数支持，集成测试通过

#### F. 算子库深度管理

- **来源文档**: `TODO_OperatorLibrary_Management.md`
- **归档状态**: ✅ 已完成（归档）
- **完成依据**: 文档自身统计 `72/72` 已完成

#### G. 算法深度审计计划

- **来源文档**: `TODO_OperatorLibrary_AlgorithmAudit.md`
- **归档状态**: ✅ 已完成（归档）
- **完成依据**: 7批审计与勘误均标注完成

### 8.3 全局验证记录

```powershell
# 构建验证
dotnet build Acme.Product/Acme.Product.sln -c Release
# 结果: 成功，0 warning / 0 error

# 测试验证
dotnet test Acme.Product/tests/Acme.Product.Tests/Acme.Product.Tests.csproj -c Release
# 结果: 通过 658，跳过 5，失败 0
```

### 8.4 Sprint 4-5 已完成功能

#### Sprint 4（2026-02-02）

| ID | 功能模块 | 状态 |
|----|---------|:----:|
| S4-001 | **ImageViewerComponent** | ✅ |
| S4-002 | **OperatorLibraryPanel** | ✅ |
| S4-003 | **WebSocket实时通信** | ✅ |
| S4-004 | **OperatorService** | ✅ |
| S4-005 | **ImageAcquisitionService** | ✅ |
| S4-006 | **端到端流程集成** | ✅ |
| S4-007 | **ImageData值对象** | ✅ |
| S4-008 | **ResultAnalysisService** | ✅ |
| S4-009 | **集成测试补充** | ✅ |

#### Sprint 5（2026-02-03 ~ 2026-02-04）

| ID | 功能模块 | 状态 |
|----|---------|:----:|
| S5-001 | **Serilog结构化日志** | ✅ |
| S5-002 | **集成测试完善** | ✅ |
| S5-003 | **实时数据流推送** | ✅ |
| S5-004 | **图像处理性能优化** | ✅ |
| S5-005 | **算子管道并行优化** | ✅ |
| S5-006 | **LRU图像缓存** | ✅ |
| S5-007 | **UI测试框架** | ✅ |
| S5-008 | **CI/CD初始化** | ✅ |
| S5-009 | **异常处理中间件** | ✅ |
| S5-010 | **代码覆盖率报告** | ✅ |

### 8.5 近期关键Bug修复（2026-02-04）

| 问题 | 严重程度 | 状态 |
|------|---------|:----:|
| **算子拖拽失效** | 🔴 P0 | ✅ |
| **新建工程对话框位置错误** | 🔴 P0 | ✅ |
| **WebView2通讯失效** | 🔴 P0 | ✅ |
| **工程数据跨工程同步** | 🔴 P0 | ✅ |
| **工程保存失效** | 🔴 P0 | ✅ |
| **CSS缓存问题** | 🟡 P1 | ✅ |
| **消息类型不一致** | 🟡 P1 | ✅ |
| **版本号冲突风险** | 🟡 P1 | ✅ |
| **CSS选择器缺失** | 🟡 P1 | ✅ |

### 8.6 UI升级已完成

| 功能 | 状态 |
|------|:----:|
| **UI 2.0 数字水墨设计** | ✅ |
| **亮色/暗色模式切换** | ✅ |
| **响应式布局** | ✅ |
| **无障碍支持** | ✅ |
| **CSS缓存解决方案** | ✅ |

---

## ?????????2026-03-16?

> ????????????? `docs/` ???????????????????????

### 9.1 2026-03-04 Bug ????

- ?????`BUG_AUDIT_2026-03-04.md`
- ??????? 2026-03-04 ??????????????UI E2E ????????? 9 ??????
- ???????????? [CURRENT_BUG_ARCH_AUDIT_2026-03-12](c:/Users/11234/Desktop/ClearVision/docs/CURRENT_BUG_ARCH_AUDIT_2026-03-12.md) ?????????????????

### 9.2 2026-03-12 Bug ????

- ?????`BUG_FIX_PLAN_2026-03-12.md`
- ?????? 2026-03-12 ??????? P1/P2/P3 ?????????? WebMessage ????MQTT ???FlowCanvas???????????????????
- ??????????????????????????????????????

### 9.3 2026-02-28 ??????

- ?????`performance_audit_2026-02-28.md`
- ????????????????????????????????????????ONNX ?????????????? PNG/Base64/Clone ????
- ????????????????????????????????????????????????

### 9.4 AI ?????????

- ?????`quality_ai_evolution_todo.md`
- ???????? Prompt ???????????????????????????????????????? Sprint A/B ???
- ???????????????????????????????????????????????

### 9.5 ???????

- ?????`TODO_OperatorLibrary_AlgorithmAudit.md`
  - ???????? 7 ??????????????????? `docs/AlgorithmAudit/`?
- ?????`TODO_OperatorLibrary_Management.md`
  - ????????????? Attribute??????NuGet ?????????????????????????? `docs/operators/` ???????

---

## 附录：原始文档索引

本文档由以下原始历史文档合并而成，原始文件已归档至 `docs/archive/legacy_md_pre_merge_20260316/`：

| 原始文件 | 合并章节 | 主要内容 |
|---------|---------|---------|
| `TODO_Phase1_DevGuide.md` | 第二章 | Phase 1 开发指导手册 |
| `TODO_Phase1_Fix.md` | 第二章 | Phase 1 修复指令 |
| `TODO_Phase2_DevGuide.md` | 第三章 | Phase 2 测试基础设施 |
| `TODO_Phase3_DevGuide.md` | 第四章 | Phase 3 算法深度提升 |
| `TODO_Phase4_DevGuide.md` | 第五章 | Phase 4 生产就绪性提升 |
| `TODO_Phase5_DevGuide.md` | 第六章 | Phase 5 前端增强 |
| `OperatorLibrary_StageReview_2026-02-26.md` | 第七章 | 算子库阶段评审 |
| `TODO_Completed_Archive_2026-03-06.md` | 第八章 | 历史任务归档 |
| `TODO_Old.md` | 第八章 | 早期任务清单 |
| `BUG_AUDIT_2026-03-04.md` | 第九章 | 2026-03-04 缺陷审计基线 |
| `BUG_FIX_PLAN_2026-03-12.md` | 第九章 | 2026-03-12 修复实施计划 |
| `performance_audit_2026-02-28.md` | 第九章 | 性能与长期运行隐患排查 |
| `quality_ai_evolution_todo.md` | 第九章 | AI 质量深化与生成进化规划 |
| `TODO_OperatorLibrary_AlgorithmAudit.md` | 第九章 | 算子算法深度审计规划 |
| `TODO_OperatorLibrary_Management.md` | 第九章 | 算子库治理与文档化方案 |

---

*文档维护：ClearVision 开发团队*  
*最后更新：2026-03-16（文档合并完成）*