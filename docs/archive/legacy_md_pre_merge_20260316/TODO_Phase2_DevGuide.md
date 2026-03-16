# ClearVision Phase 2 — 测试基础设施建设 开发指导手册

<!-- DOC_AUDIT_STATUS_START -->
## 文档审计状态（自动更新）
- 审计日期：2026-03-06
- 完成状态：已完成（回填）
- 任务统计：总计 6，已完成 6，未完成 0，待办关键词命中 0
- 判定依据：按 2026-03-06 深度审查回填：关键测试文件存在且全量测试通过。
<!-- DOC_AUDIT_STATUS_END -->



> **适用于**: opencode / AI 编码助手  
> **前置**: Phase 1（关键能力补齐）已完成  
> **目标**: 为所有 39+ 个算子建立系统化的测试覆盖

---

## 一、现状分析

### 已有测试文件
- `OperatorTests.cs` — 4 个基础算子 (ImageAcquisition, GaussianBlur, CannyEdge, Threshold)
- `DeepLearningOperatorTests.cs` — 深度学习算子
- `GeometricFittingOperatorTests.cs` — 几何拟合 (Phase 1 新增)
- `RoiManagerOperatorTests.cs` — ROI管理器 (Phase 1 新增)
- `ShapeMatchingOperatorTests.cs` — 形状匹配 (Phase 1 新增)
- `SubpixelEdgeDetectionOperatorTests.cs` — 亚像素边缘 (Phase 1 新增)
- 5 个集成测试文件

### 缺失测试的算子 (约 29 个)

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

---

## 二、开发规范

### 2.1 文件位置
所有测试放在 `tests\Acme.Product.Tests\Operators\` 目录下。

### 2.2 命名约定
- 文件名: `{算子名}OperatorTests.cs`
- 类名: `{算子名}OperatorTests`
- 方法名: `{Method}_With{Condition}_Should{Expected}`

### 2.3 依赖
- 使用 `FluentAssertions` 做断言
- 使用 `Moq` mock `ILogger`
- 需要 ILogger 的算子 (继承 OperatorBase 的) 用 Mock 创建
- 旧的不需要 ILogger 的算子直接 `new`

### 2.4 测试图像创建辅助方法

以下是创建测试用 OpenCV Mat 的辅助方法，**请在一个共享的 `TestHelpers.cs` 中创建**：

```csharp
// tests\Acme.Product.Tests\Operators\TestHelpers.cs
using Acme.Product.Infrastructure.Operators;
using OpenCvSharp;

namespace Acme.Product.Tests.Operators;

public static class TestHelpers
{
    /// <summary>
    /// 创建一个纯色测试图像的 ImageWrapper
    /// </summary>
    public static ImageWrapper CreateTestImage(int width = 200, int height = 200, Scalar? color = null)
    {
        var c = color ?? new Scalar(128, 128, 128);
        using var mat = new Mat(height, width, MatType.CV_8UC3, c);
        return new ImageWrapper(mat);
    }

    /// <summary>
    /// 创建包含简单几何形状的测试图像（用于边缘检测、轮廓检测等）
    /// </summary>
    public static ImageWrapper CreateShapeTestImage()
    {
        using var mat = new Mat(400, 400, MatType.CV_8UC3, Scalar.Black);
        Cv2.Rectangle(mat, new Rect(50, 50, 100, 100), Scalar.White, -1);
        Cv2.Circle(mat, new Point(300, 200), 60, Scalar.White, -1);
        return new ImageWrapper(mat);
    }

    /// <summary>
    /// 创建灰度梯度图像（用于阈值测试）
    /// </summary>
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

    /// <summary>
    /// 将 ImageWrapper 放入 inputs 字典
    /// </summary>
    public static Dictionary<string, object> CreateImageInputs(ImageWrapper image, string key = "Image")
    {
        return new Dictionary<string, object> { { key, image } };
    }
}
```

---

## 三、任务列表 — 按优先级分组

### 第一组：图像处理基础算子（最常用，优先覆盖）

为以下每个算子创建测试文件，每个至少 4 个测试方法：

#### 3.1 MedianBlurOperatorTests.cs

```csharp
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Infrastructure.Operators;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace Acme.Product.Tests.Operators;

public class MedianBlurOperatorTests
{
    private readonly MedianBlurOperator _operator;

    public MedianBlurOperatorTests()
    {
        _operator = new MedianBlurOperator(new Mock<ILogger<MedianBlurOperator>>().Object);
    }

    [Fact]
    public void OperatorType_ShouldBeMedianBlur()
    {
        _operator.OperatorType.Should().Be(OperatorType.MedianBlur);
    }

    [Fact]
    public async Task ExecuteAsync_WithNullInputs_ShouldReturnFailure()
    {
        var op = new Operator("测试", OperatorType.MedianBlur, 0, 0);
        var result = await _operator.ExecuteAsync(op, null);
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_WithValidImage_ShouldReturnSuccess()
    {
        var op = new Operator("测试", OperatorType.MedianBlur, 0, 0);
        using var image = TestHelpers.CreateTestImage();
        var inputs = TestHelpers.CreateImageInputs(image);
        var result = await _operator.ExecuteAsync(op, inputs);
        result.IsSuccess.Should().BeTrue();
        result.OutputData.Should().ContainKey("Image");
    }

    [Fact]
    public void ValidateParameters_Default_ShouldBeValid()
    {
        var op = new Operator("测试", OperatorType.MedianBlur, 0, 0);
        _operator.ValidateParameters(op).IsValid.Should().BeTrue();
    }
}
```

#### 3.2 其余图像处理算子

按照上面 MedianBlur 的模板，为以下算子创建测试（替换类名、枚举值）：

| 文件名 | 算子类 | 枚举值 | 特殊注意 |
|--------|-------|--------|---------|
| `BilateralFilterOperatorTests.cs` | BilateralFilterOperator | BilateralFilter | 同 MedianBlur 模板 |
| `ImageResizeOperatorTests.cs` | ImageResizeOperator | ImageResize | 验证输出尺寸是否正确 |
| `ImageCropOperatorTests.cs` | ImageCropOperator | ImageCrop | 需设置 X/Y/Width/Height 参数 |
| `ImageRotateOperatorTests.cs` | ImageRotateOperator | ImageRotate | 验证旋转角度参数 |
| `PerspectiveTransformOperatorTests.cs` | PerspectiveTransformOperator | PerspectiveTransform | 同基础模板 |
| `MorphologyOperatorTests.cs` | MorphologyOperator | Morphology | 同基础模板 |
| `ColorConversionOperatorTests.cs` | ColorConversionOperator | ColorConversion | 同基础模板 |
| `AdaptiveThresholdOperatorTests.cs` | AdaptiveThresholdOperator | AdaptiveThreshold | 同基础模板 |
| `HistogramEqualizationOperatorTests.cs` | HistogramEqualizationOperator | HistogramEqualization | 同基础模板 |

### 第二组：检测与测量算子

| 文件名 | 算子类 | 枚举值 | 特殊注意 |
|--------|-------|--------|---------|
| `BlobDetectionOperatorTests.cs` | BlobDetectionOperator | BlobDetection | 用 `CreateShapeTestImage` |
| `FindContoursOperatorTests.cs` | FindContoursOperator | FindContours | 用 `CreateShapeTestImage` |
| `TemplateMatchOperatorTests.cs` | TemplateMatchOperator | TemplateMatching | 需要两张图作为输入 |
| `CodeRecognitionOperatorTests.cs` | CodeRecognitionOperator | CodeRecognition | 同基础模板 |
| `MeasureDistanceOperatorTests.cs` | MeasureDistanceOperator | MeasureDistance | 同基础模板 |
| `CircleMeasurementOperatorTests.cs` | CircleMeasurementOperator | CircleMeasurement | 用 `CreateShapeTestImage` |
| `LineMeasurementOperatorTests.cs` | LineMeasurementOperator | LineMeasurement | 同基础模板 |
| `ContourMeasurementOperatorTests.cs` | ContourMeasurementOperator | ContourMeasurement | 同基础模板 |
| `AngleMeasurementOperatorTests.cs` | AngleMeasurementOperator | AngleMeasurement | 同基础模板 |
| `GeometricToleranceOperatorTests.cs` | GeometricToleranceOperator | GeometricTolerance | 同基础模板 |

### 第三组：标定与通信算子

| 文件名 | 算子类 | 枚举值 | 特殊注意 |
|--------|-------|--------|---------|
| `CameraCalibrationOperatorTests.cs` | CameraCalibrationOperator | CameraCalibration | 同基础模板 |
| `UndistortOperatorTests.cs` | UndistortOperator | Undistort | 同基础模板 |
| `CoordinateTransformOperatorTests.cs` | CoordinateTransformOperator | CoordinateTransform | 同基础模板 |
| `ConditionalBranchOperatorTests.cs` | ConditionalBranchOperator | ConditionalBranch | 测试 True/False 分支 |
| `ResultOutputOperatorTests.cs` | ResultOutputOperator | ResultOutput | 同基础模板 |

### 第四组：通信算子（跳过实际连接测试）

| 文件名 | 算子类 | 枚举值 | 特殊注意 |
|--------|-------|--------|---------|
| `ModbusCommunicationOperatorTests.cs` | ModbusCommunicationOperator | ModbusCommunication | 只测参数验证，不测实际连接 |
| `TcpCommunicationOperatorTests.cs` | TcpCommunicationOperator | TcpCommunication | 只测参数验证，不测实际连接 |
| `DatabaseWriteOperatorTests.cs` | DatabaseWriteOperator | DatabaseWrite | 只测参数验证和SQL注入防护 |

---

## 四、每个测试文件的最低要求

每个测试文件至少包含以下 4 个测试：

```csharp
[Fact] OperatorType_ShouldBe{枚举值}()           // 枚举验证
[Fact] ExecuteAsync_WithNullInputs_ShouldReturnFailure()  // 空输入
[Fact] ExecuteAsync_WithValidImage_ShouldReturnSuccess()   // 正常执行
[Fact] ValidateParameters_Default_ShouldBeValid()          // 默认参数
```

对于有特殊参数的算子，额外添加：
```csharp
[Fact] ValidateParameters_WithInvalid{Param}_ShouldReturnInvalid()  // 参数边界
```

---

## 五、构建验证

```powershell
cd c:\Users\11234\Desktop\ClearVision\Acme.Product

# 编译测试项目

<!-- DOC_AUDIT_STATUS_START -->
## 文档审计状态（自动更新）
- 审计日期：2026-03-06
- 完成状态：已完成（回填）
- 任务统计：总计 6，已完成 6，未完成 0，待办关键词命中 0
- 判定依据：按 2026-03-06 深度审查回填：关键测试文件存在且全量测试通过。
<!-- DOC_AUDIT_STATUS_END -->


dotnet build tests\Acme.Product.Tests\Acme.Product.Tests.csproj

# 运行所有测试

<!-- DOC_AUDIT_STATUS_START -->
## 文档审计状态（自动更新）
- 审计日期：2026-03-06
- 完成状态：已完成（回填）
- 任务统计：总计 6，已完成 6，未完成 0，待办关键词命中 0
- 判定依据：按 2026-03-06 深度审查回填：关键测试文件存在且全量测试通过。
<!-- DOC_AUDIT_STATUS_END -->


dotnet test tests\Acme.Product.Tests\Acme.Product.Tests.csproj --verbosity normal

# 按组运行

<!-- DOC_AUDIT_STATUS_START -->
## 文档审计状态（自动更新）
- 审计日期：2026-03-06
- 完成状态：已完成（回填）
- 任务统计：总计 6，已完成 6，未完成 0，待办关键词命中 0
- 判定依据：按 2026-03-06 深度审查回填：关键测试文件存在且全量测试通过。
<!-- DOC_AUDIT_STATUS_END -->


dotnet test --filter "FullyQualifiedName~MedianBlur"
dotnet test --filter "FullyQualifiedName~BilateralFilter"
```

---

## 六、执行建议

1. **先创建 `TestHelpers.cs`** — 所有测试都依赖它
2. **按组执行** — 先完成第一组（9个），编译运行确认通过，再做第二组
3. **每完成一组就 `dotnet test` 验证**
4. **通信算子(第四组)只测参数验证**，不要尝试实际 TCP/Modbus 连接

---

## 七、完成标准

- [ ] `TestHelpers.cs` 创建完成
- [ ] 第一组: 9 个图像处理算子测试 (全部通过)
- [ ] 第二组: 10 个检测与测量算子测试 (全部通过)
- [ ] 第三组: 5 个标定与流程控制算子测试 (全部通过)
- [ ] 第四组: 3 个通信算子测试 (全部通过)
- [ ] `dotnet test` 全量通过，0 failures
