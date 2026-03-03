# ClearVision Phase 3 — 算法深度提升与遗留问题修复

<!-- DOC_AUDIT_STATUS_START -->
## 文档审计状态（自动更新）
- 审计日期：2026-03-02
- 完成状态：未完成
- 任务统计：总计 7，已完成 0，未完成 7，待办关键词命中 0
- 判定依据：任务清单尚未开始勾选
<!-- DOC_AUDIT_STATUS_END -->



> **适用于**: opencode / AI 编码助手  
> **前置**: Phase 1（关键能力补齐）✅，Phase 2（测试基础设施）✅  
> **目标**: 修复编译错误、提升算法深度、完善遗留功能

---

## 〇、前置：修复编译错误（必须最先完成）

当前 `dotnet build` 全量构建报 2 个 error（Application 层），必须先修复：

### 错误定位

文件: `src\Acme.Product.Application\Services\ProjectService.cs`  
行 33, 35: `'CreateProjectRequest' 未包含 'Flow' 的定义`

### 排查步骤

1. 先阅读 `src\Acme.Product.Application\DTOs\ProjectDto.cs` 的 `CreateProjectRequest` 类 — 第 65 行已有 `Flow` 属性
2. 检查是否有**重复的 `CreateProjectRequest` 定义**在其他文件中（可能是旧版本冲突）

```powershell
cd c:\Users\11234\Desktop\ClearVision\Acme.Product
# 搜索所有 CreateProjectRequest 定义

<!-- DOC_AUDIT_STATUS_START -->
## 文档审计状态（自动更新）
- 审计日期：2026-03-02
- 完成状态：未完成
- 任务统计：总计 7，已完成 0，未完成 7，待办关键词命中 0
- 判定依据：任务清单尚未开始勾选
<!-- DOC_AUDIT_STATUS_END -->


Get-ChildItem -Recurse -Include *.cs | Select-String "class CreateProjectRequest" | ForEach-Object { $_.Path + ":" + $_.LineNumber }
```

3. 如果找到重复定义，删掉旧的那个（保留 `ProjectDto.cs` 中含 `Flow` 属性的版本）
4. 如果没有重复定义，尝试清理构建缓存：

```powershell
dotnet clean
dotnet build
```

5. 验证：`dotnet build` 输出 0 errors

---

## 一、CameraCalibration 文件夹标定（Phase 1 遗留）

> 文件: [CameraCalibrationOperator.cs](file:///c:/Users/11234/Desktop/ClearVision/Acme.Product/src/Acme.Product.Infrastructure/Operators/CameraCalibrationOperator.cs)  
> 枚举: `CameraCalibration = 24`（已有）

### 1.1 在 OperatorFactory.cs 中补充参数

在 `CameraCalibration` 元数据的 `Parameters` 列表末尾追加：

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

### 1.2 在 ExecuteCoreAsync 中添加文件夹标定分支

```csharp
var mode = GetStringParam(@operator, "Mode", "SingleImage");

if (mode == "FolderCalibration")
{
    var imageFolder = GetStringParam(@operator, "ImageFolder", "");
    var outputPath = GetStringParam(@operator, "CalibrationOutputPath", "calibration_result.json");
    
    if (string.IsNullOrEmpty(imageFolder) || !Directory.Exists(imageFolder))
        return Task.FromResult(OperatorExecutionOutput.Failure("标定图片文件夹不存在"));

    var imageFiles = Directory.GetFiles(imageFolder, "*.png")
        .Concat(Directory.GetFiles(imageFolder, "*.jpg"))
        .Concat(Directory.GetFiles(imageFolder, "*.bmp")).ToArray();

    if (imageFiles.Length < 3)
        return Task.FromResult(OperatorExecutionOutput.Failure("标定至少需要3张图片"));

    var objectPoints = new List<Mat>();
    var imagePoints = new List<Mat>();
    Size imageSize = default;
    int successCount = 0;

    foreach (var file in imageFiles)
    {
        using var img = Cv2.ImRead(file, ImreadModes.Grayscale);
        if (img.Empty()) continue;
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
            successCount++;
        }
    }

    if (successCount < 3)
        return Task.FromResult(OperatorExecutionOutput.Failure($"仅 {successCount} 张图检测到角点，至少需要3张"));

    var cameraMatrix = new Mat();
    var distCoeffs = new Mat();
    double rmsError = Cv2.CalibrateCamera(objectPoints, imagePoints, imageSize,
        cameraMatrix, distCoeffs, out _, out _);

    // 保存标定结果
    var calibData = new Dictionary<string, object>
    {
        { "RmsError", rmsError },
        { "ImageCount", successCount },
        { "ImageSize", new { imageSize.Width, imageSize.Height } },
        { "CameraMatrix", MatTo2DArray(cameraMatrix) },
        { "DistCoeffs", MatTo1DArray(distCoeffs) }
    };
    
    var json = System.Text.Json.JsonSerializer.Serialize(calibData, 
        new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
    File.WriteAllText(outputPath, json);

    // 用最后一张标定图做可视化
    using var lastImg = Cv2.ImRead(imageFiles[^1]);
    using var undistorted = new Mat();
    Cv2.Undistort(lastImg, undistorted, cameraMatrix, distCoeffs);

    var additionalData = new Dictionary<string, object>
    {
        { "RmsError", rmsError },
        { "SuccessCount", successCount },
        { "TotalImages", imageFiles.Length },
        { "OutputPath", outputPath }
    };

    // 释放标定用 Mat
    foreach (var m in objectPoints) m.Dispose();
    foreach (var m in imagePoints) m.Dispose();
    cameraMatrix.Dispose();
    distCoeffs.Dispose();

    return Task.FromResult(OperatorExecutionOutput.Success(CreateImageOutput(undistorted, additionalData)));
}

// else: 保持原有 SingleImage 逻辑不变
```

### 1.3 辅助方法

```csharp
private double[,] MatTo2DArray(Mat mat)
{
    var rows = mat.Rows;
    var cols = mat.Cols;
    var arr = new double[rows, cols];
    for (int r = 0; r < rows; r++)
        for (int c = 0; c < cols; c++)
            arr[r, c] = mat.At<double>(r, c);
    return arr;
}

private double[] MatTo1DArray(Mat mat)
{
    var len = mat.Rows * mat.Cols;
    var arr = new double[len];
    for (int i = 0; i < len; i++)
        arr[i] = mat.At<double>(0, i);
    return arr;
}
```

---

## 二、码识别优化 (CodeRecognitionOperator)

> 文件: `src\Acme.Product.Infrastructure\Operators\CodeRecognitionOperator.cs`  
> 当前问题: `MatToBitmap()` 中做了 `mat.ToBytes(".png")` 编码，违背零拷贝设计

### 2.1 替换 MatToBitmap 实现

用内存直接拷贝代替 PNG 编解码，性能提升约 10-50 倍：

```csharp
private System.Drawing.Bitmap MatToBitmapDirect(Mat mat)
{
    // 转为连续的 BGR 格式
    using var bgr = mat.Type() == MatType.CV_8UC1 
        ? new Mat() 
        : mat;
    
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
            // 一次性内存拷贝（最快）
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
            // 逐行拷贝（处理 stride 不一致的情况）
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

> [!IMPORTANT]  
> 需要在 `Acme.Product.Infrastructure.csproj` 中启用 `<AllowUnsafeBlocks>true</AllowUnsafeBlocks>`。  
> 如果不想用 unsafe，保留当前 PNG 编解码方案也可以（功能不受影响，仅性能差异）。

### 2.2 启用多码识别

将 `Decode()` 替换为 `DecodeMultiple()`：

```csharp
// 旧：只返回第一个结果
// var result = reader.Decode(luminanceSource);

// 新：返回所有识别到的码
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

---

## 三、深度学习扩展 — 自定义类别标签

> 文件: `src\Acme.Product.Infrastructure\Operators\DeepLearningOperator.cs`  
> 当前问题: COCO 80 类硬编码，用户自定义模型无法显示正确标签

### 3.1 新增参数

在 `OperatorFactory.cs` 的 `DeepLearning` 元数据中追加：

```csharp
new() { Name = "LabelFile", DisplayName = "标签文件路径", DataType = "file", DefaultValue = "" },
```

### 3.2 标签加载逻辑

在 `ExecuteCoreAsync` 中，获取标签文件并替代硬编码标签：

```csharp
var labelFile = GetStringParam(@operator, "LabelFile", "");
string[] labels;

if (!string.IsNullOrEmpty(labelFile) && File.Exists(labelFile))
{
    // 用户自定义标签（每行一个标签）
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
```

### 3.3 同时在模型目录查找标签

如果模型路径为 `model.onnx`，自动查找同目录下的 `labels.txt`：

```csharp
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

---

## 四、颜色检测算子（新增）

> **枚举**: `ColorDetection = 45`  
> **文件**: `ColorDetectionOperator.cs`  
> **难度**: ⭐⭐ 低

### 4.1 枚举 + DI + 工厂

```csharp
// OperatorEnums.cs — 在 SubpixelEdgeDetection = 44 之后
/// <summary>
/// 颜色检测 - HSV/Lab 空间颜色分析
/// </summary>
ColorDetection = 45,
```

```csharp
// DependencyInjection.cs
services.AddSingleton<IOperatorExecutor, ColorDetectionOperator>();
```

工厂元数据：

```csharp
_metadata[OperatorType.ColorDetection] = new OperatorMetadata
{
    Type = OperatorType.ColorDetection,
    DisplayName = "颜色检测",
    Description = "基于 HSV/Lab 空间的颜色分析与分级",
    Category = "特征提取",
    IconName = "color",
    InputPorts = new List<PortDefinition>
    {
        new() { Name = "Image", DisplayName = "输入图像", DataType = PortDataType.Image, IsRequired = true }
    },
    OutputPorts = new List<PortDefinition>
    {
        new() { Name = "Image", DisplayName = "结果图像", DataType = PortDataType.Image },
        new() { Name = "ColorInfo", DisplayName = "颜色信息", DataType = PortDataType.Any }
    },
    Parameters = new List<ParameterDefinition>
    {
        new() { Name = "ColorSpace", DisplayName = "颜色空间", DataType = "enum", DefaultValue = "HSV",
            Options = new List<ParameterOption>
            {
                new() { Label = "HSV", Value = "HSV" },
                new() { Label = "Lab", Value = "Lab" }
            }
        },
        new() { Name = "AnalysisMode", DisplayName = "分析模式", DataType = "enum", DefaultValue = "Average",
            Options = new List<ParameterOption>
            {
                new() { Label = "平均色", Value = "Average" },
                new() { Label = "主色提取", Value = "Dominant" },
                new() { Label = "颜色范围检测", Value = "Range" }
            }
        },
        new() { Name = "HueLow", DisplayName = "H下限", DataType = "int", DefaultValue = 0, MinValue = 0, MaxValue = 180 },
        new() { Name = "HueHigh", DisplayName = "H上限", DataType = "int", DefaultValue = 180, MinValue = 0, MaxValue = 180 },
        new() { Name = "SatLow", DisplayName = "S下限", DataType = "int", DefaultValue = 50, MinValue = 0, MaxValue = 255 },
        new() { Name = "SatHigh", DisplayName = "S上限", DataType = "int", DefaultValue = 255, MinValue = 0, MaxValue = 255 },
        new() { Name = "ValLow", DisplayName = "V下限", DataType = "int", DefaultValue = 50, MinValue = 0, MaxValue = 255 },
        new() { Name = "ValHigh", DisplayName = "V上限", DataType = "int", DefaultValue = 255, MinValue = 0, MaxValue = 255 },
        new() { Name = "DominantK", DisplayName = "主色数量K", DataType = "int", DefaultValue = 3, MinValue = 1, MaxValue = 10 }
    }
};
```

### 4.2 核心算法

```
根据 AnalysisMode:

"Average":
  1. CvtColor(src, hsv/lab)
  2. Cv2.Mean(hsv/lab) → 得到平均 H/S/V 或 L/a/b
  3. 输出平均颜色值

"Dominant" (主色提取 — K-Means):
  1. Resize 到 64x64 (加速)
  2. Reshape 为 Nx3 矩阵
  3. Cv2.Kmeans(data, K, ...) → 得到 K 个聚类中心
  4. 按聚类大小排序 → 输出前 K 个主要颜色

"Range" (颜色范围检测):
  1. CvtColor(src, hsv)
  2. Cv2.InRange(hsv, lower, upper, mask)
  3. 计算 mask 中白色像素面积占比
  4. 输出掩膜图像 + 面积百分比
```

---

## 五、串口通信算子（新增）

> **枚举**: `SerialCommunication = 46`  
> **文件**: `SerialCommunicationOperator.cs`  
> **难度**: ⭐⭐ 低

### 5.1 枚举 + DI + 工厂

```csharp
// OperatorEnums.cs
/// <summary>
/// 串口通信 - RS-232/485 PLC 通信
/// </summary>
SerialCommunication = 46,
```

```csharp
// DependencyInjection.cs
services.AddSingleton<IOperatorExecutor, SerialCommunicationOperator>();
```

工厂元数据：

```csharp
_metadata[OperatorType.SerialCommunication] = new OperatorMetadata
{
    Type = OperatorType.SerialCommunication,
    DisplayName = "串口通信",
    Description = "RS-232/485 串口数据收发",
    Category = "通信",
    IconName = "serial",
    InputPorts = new List<PortDefinition>
    {
        new() { Name = "Data", DisplayName = "发送数据", DataType = PortDataType.Any, IsRequired = false }
    },
    OutputPorts = new List<PortDefinition>
    {
        new() { Name = "Response", DisplayName = "接收数据", DataType = PortDataType.Any }
    },
    Parameters = new List<ParameterDefinition>
    {
        new() { Name = "PortName", DisplayName = "串口号", DataType = "string", DefaultValue = "COM1" },
        new() { Name = "BaudRate", DisplayName = "波特率", DataType = "enum", DefaultValue = "9600",
            Options = new List<ParameterOption>
            {
                new() { Label = "9600", Value = "9600" },
                new() { Label = "19200", Value = "19200" },
                new() { Label = "38400", Value = "38400" },
                new() { Label = "57600", Value = "57600" },
                new() { Label = "115200", Value = "115200" }
            }
        },
        new() { Name = "DataBits", DisplayName = "数据位", DataType = "int", DefaultValue = 8, MinValue = 5, MaxValue = 8 },
        new() { Name = "StopBits", DisplayName = "停止位", DataType = "enum", DefaultValue = "One",
            Options = new List<ParameterOption>
            {
                new() { Label = "1", Value = "One" },
                new() { Label = "1.5", Value = "OnePointFive" },
                new() { Label = "2", Value = "Two" }
            }
        },
        new() { Name = "Parity", DisplayName = "校验位", DataType = "enum", DefaultValue = "None",
            Options = new List<ParameterOption>
            {
                new() { Label = "无", Value = "None" },
                new() { Label = "奇校验", Value = "Odd" },
                new() { Label = "偶校验", Value = "Even" }
            }
        },
        new() { Name = "SendData", DisplayName = "发送内容", DataType = "string", DefaultValue = "" },
        new() { Name = "Encoding", DisplayName = "编码", DataType = "enum", DefaultValue = "UTF8",
            Options = new List<ParameterOption>
            {
                new() { Label = "UTF-8", Value = "UTF8" },
                new() { Label = "ASCII", Value = "ASCII" },
                new() { Label = "HEX", Value = "HEX" }
            }
        },
        new() { Name = "TimeoutMs", DisplayName = "超时(毫秒)", DataType = "int", DefaultValue = 3000, MinValue = 100, MaxValue = 30000 }
    }
};
```

### 5.2 核心实现

参考 `TcpCommunicationOperator.cs` 的结构：

```csharp
using System.IO.Ports;

// 在 ExecuteCoreAsync 中:
var portName = GetStringParam(@operator, "PortName", "COM1");
var baudRate = int.Parse(GetStringParam(@operator, "BaudRate", "9600"));
var dataBits = GetIntParam(@operator, "DataBits", 8);
var stopBits = Enum.Parse<StopBits>(GetStringParam(@operator, "StopBits", "One"));
var parity = Enum.Parse<Parity>(GetStringParam(@operator, "Parity", "None"));
var timeoutMs = GetIntParam(@operator, "TimeoutMs", 3000);
var sendData = GetStringParam(@operator, "SendData", "");
var encoding = GetStringParam(@operator, "Encoding", "UTF8");

using var port = new SerialPort(portName, baudRate, parity, dataBits, stopBits)
{
    ReadTimeout = timeoutMs,
    WriteTimeout = timeoutMs
};

try
{
    port.Open();
    
    // 发送
    if (!string.IsNullOrEmpty(sendData))
    {
        byte[] bytes = encoding == "HEX"
            ? Convert.FromHexString(sendData.Replace(" ", ""))
            : System.Text.Encoding.GetEncoding(encoding).GetBytes(sendData);
        port.Write(bytes, 0, bytes.Length);
    }
    
    // 接收
    await Task.Delay(100, cancellationToken); // 等待设备响应
    string response = "";
    if (port.BytesToRead > 0)
    {
        byte[] buffer = new byte[port.BytesToRead];
        port.Read(buffer, 0, buffer.Length);
        response = encoding == "HEX"
            ? BitConverter.ToString(buffer).Replace("-", " ")
            : System.Text.Encoding.GetEncoding(encoding).GetString(buffer);
    }
    
    var output = new Dictionary<string, object>
    {
        { "Response", response },
        { "BytesReceived", response.Length },
        { "Port", portName }
    };
    return Task.FromResult(OperatorExecutionOutput.Success(output));
}
catch (Exception ex)
{
    return Task.FromResult(OperatorExecutionOutput.Failure($"串口通信失败: {ex.Message}"));
}
```

---

## 六、单元测试（为新增/修改的算子）

为以下算子创建测试文件（参照 Phase 2 的模板）：

| 文件 | 枚举 |
|------|------|
| `ColorDetectionOperatorTests.cs` | ColorDetection |
| `SerialCommunicationOperatorTests.cs` | SerialCommunication (仅测参数验证) |

同时更新 `CameraCalibrationOperatorTests.cs` 增加文件夹标定模式的参数验证测试。

---

## 七、构建验证

```powershell
cd c:\Users\11234\Desktop\ClearVision\Acme.Product

# 全量构建 — 必须 0 errors

<!-- DOC_AUDIT_STATUS_START -->
## 文档审计状态（自动更新）
- 审计日期：2026-03-02
- 完成状态：未完成
- 任务统计：总计 7，已完成 0，未完成 7，待办关键词命中 0
- 判定依据：任务清单尚未开始勾选
<!-- DOC_AUDIT_STATUS_END -->


dotnet build

# 运行所有测试

<!-- DOC_AUDIT_STATUS_START -->
## 文档审计状态（自动更新）
- 审计日期：2026-03-02
- 完成状态：未完成
- 任务统计：总计 7，已完成 0，未完成 7，待办关键词命中 0
- 判定依据：任务清单尚未开始勾选
<!-- DOC_AUDIT_STATUS_END -->


dotnet test

# 单独测试新增算子

<!-- DOC_AUDIT_STATUS_START -->
## 文档审计状态（自动更新）
- 审计日期：2026-03-02
- 完成状态：未完成
- 任务统计：总计 7，已完成 0，未完成 7，待办关键词命中 0
- 判定依据：任务清单尚未开始勾选
<!-- DOC_AUDIT_STATUS_END -->


dotnet test --filter "FullyQualifiedName~ColorDetection"
dotnet test --filter "FullyQualifiedName~SerialCommunication"
```

---

## 八、执行顺序

| 顺序 | 任务 | 类型 | 说明 |
|:----:|------|------|------|
| 0 | **修复编译错误** | 修复 | `dotnet clean && dotnet build` 或删除重复定义 |
| 1 | CameraCalibration 改进 | 修改 | 文件夹标定模式 |
| 2 | 码识别优化 | 修改 | MatToBitmap 性能 + 多码识别 |
| 3 | 深度学习自定义标签 | 修改 | LabelFile 参数 |
| 4 | 颜色检测算子 | 新增 | 枚举 45 |
| 5 | 串口通信算子 | 新增 | 枚举 46 |
| 6 | 单元测试 | 新增 | 2 个新测试文件 |
| 7 | 全量构建验证 | 验证 | `dotnet build && dotnet test` |

---

## 九、完成标准

- [ ] `dotnet build` 全量 0 errors
- [ ] CameraCalibration 支持 FolderCalibration 模式
- [ ] CodeRecognition 支持多码识别
- [ ] DeepLearning 支持自定义标签文件
- [ ] ColorDetection 新算子可用
- [ ] SerialCommunication 新算子可用
- [ ] `dotnet test` 全量通过
