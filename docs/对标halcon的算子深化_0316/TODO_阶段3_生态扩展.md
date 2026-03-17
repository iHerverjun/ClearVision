# 阶段3 TODO：生态扩展（Vibe Coding版 V2.3）

> **阶段目标**: AI+3D融合，形成差异化竞争力
> **时间周期**: 第12-16周（原9-12周，调整为12-16周）
> **前置条件**: 阶段2里程碑达成（3D点云基础可用）
> **核心理念**: 深度学习算子化，端到端场景闭环 + 性能可控 + 风险可控

---

## 零点五、2026-03-17 推进快照

- **本轮已落地**：W9-0 / W9-1 在原有基础上接入 `ModelId + ModelCatalogPath` 模型仓库解析，`models/model_catalog.json` 已建立。
- **本轮已落地**：W9-2 `AnomalyDetectionOperator` 已实现，支持简化 PatchCore 训练/推理、特征库持久化、热力图与掩码输出。
- **本轮已落地**：W11-1 `HandEyeCalibrationOperator` 已实现，支持 `eye_in_hand` 与轻量 `eye_to_hand` 两种模式，并产出 HTML 报告与复核姿态建议。
- **本轮已落地**：W11-3 `HandEyeCalibrationValidatorOperator` 已实现，可独立复核标定质量。
- **本轮已落地**：W12-1 已补齐可执行 Demo 闭环，对应自动化用例 `Stage3_EndToEndDemoIntegrationTests`。
- **本轮已落地**：W12-2 已补齐新增算子文档，并同步更新 `docs/operators/` 目录索引。
- **自动化验证**：新增 `ModelCatalogTests`、`AnomalyDetectionOperatorTests`、`HandEyeCalibrationOperatorTests`、`Stage3_EndToEndDemoIntegrationTests`，定向用例已通过。
- **剩余外部依赖**：真实业务模型资产、真实 CUDA 运行时和现场验收仍属于部署/交付侧工作，不在当前代码仓验证闭环内。

---

## 零、阶段3增强说明

### 新增内容
- **W9-0**: ONNX模型验证管道（2小时）
- **W11-3**: 手眼标定验证（3小时）
- **性能预算检查**：所有深度学习算子必须满足<100ms（GPU）
- **错误处理标准**：统一的模型加载失败、推理失败处理
- **时间调整**：异常检测从6小时调整为10小时（更现实）

### 保持不变
- ⚠️ **开发者SDK**: 延后到第4阶段（文档工作量大）
- ⚠️ **GPU加速深化**: 仅做ONNX Runtime GPU配置，不做自定义CUDA算子

---

## 一、本阶段任务总览

```
Week 12: 深度学习基础（模型验证 + 语义分割）
Week 13: 深度学习进阶（异常检测）
Week 14: 深度学习优化（模型仓库 + GPU加速）
Week 15: 手眼标定（算法 + 验证）
Week 16: 集成优化 + 场景Demo
```

### 任务筛选原则

| 任务 | AI友好度 | 是否纳入 | 理由 | 时间调整 |
|------|---------|----------|------|---------|
| ONNX模型验证 | ⭐⭐⭐⭐⭐ | ✅ 新增 | 确保模型部署一致性 | 2小时 |
| 语义分割ONNX | ⭐⭐⭐⭐⭐ | ✅ | 模型转换现成，只需封装 | 4小时 |
| 异常检测 | ⭐⭐⭐⭐☆ | ✅ | 可用开源模型，重点在数据流 | 10小时（原6小时） |
| 手眼标定 | ⭐⭐⭐☆☆ | ✅ | 算法明确，UI简化先做命令行版 | 4小时 |
| 手眼标定验证 | ⭐⭐⭐⭐☆ | ✅ 新增 | 质量保证必需 | 3小时 |
| GPU加速 | ⭐⭐☆☆☆ | ✅ 简化 | 仅ONNX Runtime GPU配置 | 2小时 |
| 开发者SDK | ⭐⭐⭐☆☆ | ❌ 延后 | 文档工作量大，延后到第4阶段 | - |

---

## 二、Week 12：深度学习基础

### 任务W9-0：ONNX模型验证管道（2小时）⭐ 新增任务

```markdown
【任务】实现模型验证工具，确保部署一致性

【为什么重要】
- ONNX模型训练时的预处理与部署时可能不一致
- 导致推理精度下降
- 需要验证管道确保一致性

【要求】
1. 实现预处理验证工具
2. 实现推理结果比较工具
3. 创建模型测试套件

【AI Prompt】
```markdown
【任务】实现ONNX模型验证工具

【功能】
1. 预处理验证：
   - 检查图像归一化参数（mean, std）
   - 检查输入尺寸
   - 检查通道顺序（RGB vs BGR）

2. 推理结果比较：
   - 加载参考输出（从训练框架导出）
   - 运行ONNX推理
   - 计算误差（MSE, MAE, 最大误差）

3. 模型测试套件：
   - 标准测试图集
   - 预期输出
   - 自动化测试脚本

【输出文件】
- ModelValidator.cs
- model_test_suite/（测试数据目录）

【使用示例】
```csharp
var validator = new ModelValidator("yolov8s.onnx");
validator.SetPreprocessing(mean: new[] {0.485, 0.406, 0.456},
                          std: new[] {0.229, 0.224, 0.225});
var result = validator.Validate("test_image.jpg", "expected_output.json");
// result.IsValid, result.MaxError, result.MeanError
```

【验证标准】
□ 能检测预处理参数错误
□ 能检测推理结果偏差>5%
□ 生成详细验证报告
```

【验收】
- 验证工具可用
- 至少测试3个模型（YOLO、分割、分类）
- 生成验证报告模板
```

---

### 任务W9-1：语义分割算子（ONNX集成）（4小时）

```markdown
【任务】封装语义分割模型为算子
【前置】已有ONNX模型（如DeepLabV3/SegFormer导出）

【算子规格】
```csharp
public class SemanticSegmentationOperator : OperatorBase
{
    [InputPort("Image", PortDataType.Image)]
    
    [Parameter("ModelPath", "", "ONNX模型路径")]
    [Parameter("InputSize", "512,512", "模型输入尺寸")]
    [Parameter("NumClasses", 21, "类别数")]
    [Parameter("ClassNames", "", "类别名称JSON")]
    [Parameter("UseGpu", true, "使用GPU推理")]
    
    [OutputPort("SegmentationMap", PortDataType.Image)]  // 单通道，像素值=类别ID
    [OutputPort("ColoredMap", PortDataType.Image)]       // 可视化着色图
    [OutputPort("ClassMasks", PortDataType.ImageList)]   // 每类二值掩码
}
```

【实现要点】
1. 使用Microsoft.ML.OnnxRuntime
2. 图像预处理：Resize → Normalize → ToTensor
3. 后处理：ArgMax获取类别，Resize回原图尺寸
4. 可选：CRF后处理优化边界

【代码框架】
```csharp
protected override async Task<OperatorExecutionOutput> ExecuteCoreAsync(...)
{
    // 1. 获取全局单例模型缓存（勿每次 new InferenceSession）
    var session = ModelManager.GetSession(ModelPath);
    
    // 2. 预处理
    var inputTensor = Preprocess(Image, InputSize);
    
    // 3. 推理（注意判断 CancellationToken）
    var outputs = session.Run(new[] { NamedOnnxValue.CreateFromTensor("input", inputTensor) });
    
    // 4. 后处理
    var segMap = Postprocess(outputs[0], originalSize);
    
    return OperatorExecutionOutput.Success(CreateImageOutput(segMap));
}
```

【AI 编程专属上下文】
- **推理加速**: 强制引用 `Microsoft.ML.OnnxRuntime` 系列库。
- **输入装配**: OpenCV 的 `Mat` 到 `DenseTensor<float>` 的装换过程需通过安全指针跨越 `mat.Data` 内存以避免逐像素遍历带来的毫秒级耗时。
- **缓存规约**: 需要额写一个局部的静态单例池或 LRU 算法来缓存并持有 `InferenceSession` 实例。

【测试模型】
- 下载现成的ONNX分割模型
- 或使用PyTorch导出自定义模型

【验收】
- 512x512图像推理<100ms（GPU）
- mIoU与原始模型一致
- 使用W9-0验证工具验证通过
```

---

## 三、Week 13：深度学习进阶

### 任务W9-2：异常检测算子（PatchCore集成）（10小时）⭐ 时间调整

```markdown
【任务】实现无监督异常检测算子
【核心价值】无需缺陷样本，只学正常样本即可检测异常
【时间调整】从6小时调整为10小时（更现实的估计）

【算法选择】
PatchCore（NeurIPS 2022）
- 优点：无需训练，只需要正常样本特征库
- 速度：较快，适合工业
- 精度：SOTA级别

【简化实现流程】
1. 训练/准备阶段：
   - 用ResNet50提取正常图像特征（内存库）
   - 局部聚合（减少内存占用）
   - 保存特征库

2. 推理阶段：
   - 提取测试图像特征
   - 与内存库最近邻搜索
   - 异常分数 = 最大距离
   - 异常图 = 逐像素距离

【算子规格】
```csharp
public class AnomalyDetectionOperator : OperatorBase
{
    [InputPort("Image", PortDataType.Image)]
    
    [Parameter("Mode", "inference", "inference/train")]
    [Parameter("FeatureBankPath", "", "特征库路径")]
    [Parameter("Backbone", "resnet50", "特征提取网络")]
    [Parameter("CoresetRatio", 0.1, "核心集采样比例（减少内存）")]
    [Parameter("Threshold", 0.5, "异常阈值")]
    
    // 训练模式参数
    [InputPort("NormalImages", PortDataType.ImageList, IsOptional = true)]
    [Parameter("SaveFeatureBankPath", "", "保存特征库路径")]
    
    [OutputPort("AnomalyScore", PortDataType.Float)]      // 0-1，越大越异常
    [OutputPort("IsAnomaly", PortDataType.Boolean)]
    [OutputPort("AnomalyMap", PortDataType.Image)]        // 热力图
    [OutputPort("AnomalyMask", PortDataType.Image)]       // 二值掩码
}
```

【实现策略】
阶段1: 直接用Python PatchCore生成特征库，C#只实现推理
阶段2: 纯C#实现（用ONNX Runtime跑ResNet）

【参考】
- GitHub: amazon-science/patchcore-inspection
- 论文: Roth et al., "Towards Total Recall in Industrial Anomaly Detection"

【测试】
- MVTec AD数据集
- AUC > 0.95
- 推理时间<200ms（GPU）
- 使用W9-0验证工具验证
```

---

## 四、Week 14：深度学习优化

### 任务W10-1：深度学习模型仓库（2小时）

```markdown
【任务】建立模型管理和版本机制

【设计】
```
/models
  /segmentation
    deeplabv3_cityscapes.onnx
    segformer_b0_ade.onnx
  /anomaly_detection
    patchcore_mvtec_bottle.onnx
    patchcore_mvtec_cable.onnx
  /object_detection
    yolov8s.onnx
  
model_catalog.json:
{
  "models": [
    {
      "id": "deeplabv3_cityscapes",
      "type": "segmentation",
      "input_size": [512, 512],
      "num_classes": 19,
      "class_names": ["road", "sidewalk", ...],
      "version": "1.0.0",
      "source": "https://..."
    }
  ]
}
```

【算子增强】
```csharp
[Parameter("ModelId", "", "从模型仓库选择")]
// 下拉框从model_catalog.json加载
```
```

---

### 任务W11-2：GPU加速基础（ONNX Runtime GPU）（2小时）

```markdown
【任务】启用ONNX Runtime GPU推理
【实现】极其简单，只需配置SessionOptions
【范围限制】仅做ONNX Runtime配置，不做自定义CUDA算子

```csharp
var sessionOptions = new SessionOptions();
sessionOptions.AppendExecutionProvider_CUDA(0);  // GPU 0
// sessionOptions.AppendExecutionProvider_DirectML(0);  // 或用DirectML

using var session = new InferenceSession(modelPath, sessionOptions);
```

【算子参数】
```csharp
[Parameter("ExecutionProvider", "cuda", "cpu/cuda/directml")]
// 注意：不支持tensorrt（过于复杂，延后）
```

【性能对比测试】
| 模型 | CPU | CUDA | 加速比 | 目标 |
|------|-----|------|--------|------|
| YOLOv8s | 200ms | 20ms | 10x | <100ms ✅ |
| DeepLabV3 | 500ms | 30ms | 16x | <100ms ✅ |
| PatchCore | 300ms | 50ms | 6x | <200ms ✅ |

【验收】
- GPU推理可用
- 性能满足预算
- CPU/GPU自动切换（无GPU时降级到CPU）
```

---

## 五、Week 15：手眼标定

### 任务W11-1：手眼标定算法（命令行版）（4小时）

```markdown
【任务】实现手眼标定核心算法（暂不用UI向导）
【类型选择】
- Eye-in-Hand (相机在机器人上)
- Eye-to-Hand (相机固定，看机器人)

【算法：AX=XB求解】
输入: 
- 机器人位姿列表 (T_b_r: base→robot)
- 相机标定板位姿列表 (T_c_o: camera→object)

输出:
- 手眼矩阵 (T_r_c: robot→camera 或 T_b_c: base→camera)

【求解方法】
1. Tsai-Lenz算法（经典，解析解）
2. 对偶四元数法
3. 非线性优化（LM算法精修）

【简化实现】
先用OpenCV的calibrateHandEye：
```csharp
// OpenCV 4.x已有实现
Cv2.CalibrateHandEye(
    R_gripper2base,  // 机器人旋转矩阵列表
    t_gripper2base,  // 机器人平移向量列表
    R_target2cam,    // 标定板旋转矩阵列表
    t_target2cam,    // 标定板平移向量列表,
    out R_cam2gripper,
    out t_cam2gripper,
    HandEyeMethod.Tsai
);
```

【算子规格】
```csharp
public class HandEyeCalibrationOperator : OperatorBase
{
    [Parameter("CalibrationType", "eye_in_hand", "eye_in_hand/eye_to_hand")]
    
    // 输入数据（从文件或手动输入）
    [InputPort("RobotPoses", PortDataType.Pose3DArray)]      // 机器人位姿
    [InputPort("CalibrationBoardPoses", PortDataType.Pose3DArray)]  // 检测到的标定板位姿
    
    [Parameter("CameraMatrix", "", "相机内参JSON")]
    [Parameter("DistortionCoeffs", "", "畸变系数JSON")]
    
    [OutputPort("HandEyeMatrix", PortDataType.Matrix4x4)]
    [OutputPort("ReprojectionError", PortDataType.Float)]    // 重投影误差
    [OutputPort("CalibrationQuality", PortDataType.String)]  // good/fair/poor
}
```

【AI 编程专属上下文】
- **依赖调用**: OpenCV 调用类对应于 `OpenCvSharp.Cv2.CalibrateHandEye`。
- **参数解析**: JSON 反序列化可直接使用 `System.Text.Json.JsonSerializer.Deserialize` 并将其转化为对应的 `double[]` 传给内参矩阵构建器。

【使用流程（命令行版）】
1. 采集N组数据（机器人位姿+对应图像）
2. 检测每张图的标定板位姿
3. 调用算子计算手眼矩阵
4. 验证：转换后的点应在预期位置

【验收】
- 重投影误差 < 1mm
- 采集8-10个点即可稳定收敛
- 使用W11-3验证工具验证
```

---

### 任务W11-3：手眼标定验证（3小时）⭐ 新增任务

```markdown
【任务】实现手眼标定质量验证工具

【为什么重要】
- 标定质量直接影响机器人定位精度
- 需要可视化工具评估标定结果
- 提供质量指标指导用户重新标定

【要求】
1. 重投影误差可视化
2. 标定质量指标计算
3. 验证测试图案生成

【AI Prompt】
```markdown
【任务】实现手眼标定验证工具

【功能】
1. 重投影误差可视化：
   - 将标定板角点投影到图像
   - 计算与检测角点的误差
   - 生成误差热力图

2. 标定质量指标：
   - 平均重投影误差（像素）
   - 最大重投影误差
   - 标定板姿态分布（是否覆盖足够空间）
   - 质量评级：good(<0.5px), fair(0.5-1px), poor(>1px)

3. 验证测试图案：
   - 生成标准测试位置
   - 机器人移动到测试位置
   - 验证实际位置与预期位置误差

【输出文件】
- HandEyeCalibrationValidator.cs
- 验证报告模板

【使用示例】
```csharp
var validator = new HandEyeCalibrationValidator(handEyeMatrix);
var report = validator.Validate(robotPoses, images, cameraMatrix);
// report.MeanError, report.MaxError, report.Quality
```

【验证标准】
□ 能检测标定质量差的情况
□ 生成可视化报告
□ 提供改进建议
```

【验收】
- 验证工具可用
- 能识别poor质量标定
- 生成HTML验证报告
```

---

## 六、Week 16：集成优化 + 场景Demo

### 任务W12-1：端到端场景Demo（6小时）

```markdown
【目标】完成1个可演示的端到端场景
【推荐场景】3C零部件检测（综合2D+3D+AI）

【检测流程】
```
1. 图像采集 (ImageAcquisition)
   ↓
2. 模板定位 (RobustShapeMatch)
   ↓
3. ROI跟踪测量 (AdvancedCaliper × 5)
   输出: 关键尺寸
   ↓
4. 缺陷检测 (AnomalyDetection)
   输出: 异常分数+热力图
   ↓
5. 3D高度测量 (PointCloudCapture + PlaneSegmentation)
   输出: 平面度、高度差
   ↓
6. 综合判定 (LogicGate)
   输出: OK/NG
   ↓
7. 结果输出 (ResultOutput)
```

【演示效果】
- 实时显示各算子结果
- 关键尺寸实时显示
- 缺陷热力图叠加
- 3D点云可视化

【输出物】
- 可运行的工程文件
- 演示视频/GIF
- 性能数据（CT时间）
```

---

### 任务W12-2：文档完善（2小时）

```markdown
【任务】完善新增算子的文档

【每个算子文档包含】
1. 功能描述
2. 输入输出端口说明
3. 参数说明（含推荐值）
4. 使用示例（JSON流程片段）
5. 常见问题

【示例文档结构】
```markdown
# AdvancedCaliperOperator

## 功能
亚像素边缘测量工具，支持单边缘和边缘对测量。

## 输入
- Image: 输入图像
- RoiRectangle: 测量ROI

## 输出
- EdgePositions: 边缘位置（亚像素）
- PairDistances: 边缘对距离

## 参数
| 参数 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| SubPixelMode | enum | gradient_centroid | 亚像素算法 |
| CaliperCount | int | 10 | 卡尺数量 |

## 示例
```json
{
  "type": "AdvancedCaliper",
  "inputs": {"Image": "prev.Image"},
  "parameters": {"CaliperCount": 15}
}
```

## 注意事项
- ROI方向决定测量方向
- 边缘对测量时确保两个边缘都在ROI内
```
```

---

## 七、性能预算检查清单

> **重要**：所有深度学习算子必须满足性能预算

### 阶段3算子性能预算

| 算子 | 目标延迟（GPU） | 目标延迟（CPU） | 内存限制 | 验证方法 |
|------|---------------|---------------|---------|---------|
| SemanticSegmentation | <100ms | <500ms | 2GB | 512x512输入 |
| AnomalyDetection | <200ms | <1000ms | 2GB | 224x224输入 |
| HandEyeCalibration | <100ms | <100ms | 100MB | 10组数据 |
| ModelValidator | <50ms | <50ms | 500MB | 单次验证 |

### 性能验证流程

```markdown
【深度学习算子性能验证】
1. 使用W0-1性能分析工具
2. 分别测试CPU和GPU模式
3. 运行100次取平均值
4. 记录内存峰值
5. 对比性能预算表
6. 生成性能报告
```

### GPU性能优化建议

如果GPU性能不达标：
1. **检查批处理大小**：batch_size=1可能未充分利用GPU
2. **检查数据传输**：CPU→GPU数据传输是否过多
3. **检查模型优化**：是否使用FP16精度
4. **检查CUDA版本**：确保CUDA版本与ONNX Runtime匹配

---

## 八、错误处理标准

> **重要**：深度学习算子的错误处理尤为关键

### 深度学习算子错误处理

```csharp
protected override async Task<OperatorExecutionOutput> ExecuteCoreAsync(...)
{
    // 1. 模型加载验证
    InferenceSession session;
    try
    {
        session = ModelManager.GetSession(ModelPath);
    }
    catch (FileNotFoundException ex)
    {
        return Task.FromResult(OperatorExecutionOutput.Failure(
            "ModelNotFound",
            $"模型文件不存在: {ModelPath}",
            "请检查模型路径或从模型仓库下载"
        ));
    }
    catch (OnnxRuntimeException ex)
    {
        return Task.FromResult(OperatorExecutionOutput.Failure(
            "ModelLoadFailed",
            $"模型加载失败: {ex.Message}",
            "请检查模型格式是否正确（ONNX格式）"
        ));
    }

    // 2. 输入验证
    if (inputImage == null || inputImage.Empty())
    {
        return Task.FromResult(OperatorExecutionOutput.Failure(
            "InputImageEmpty",
            "输入图像为空",
            "请检查上游算子输出"
        ));
    }

    // 3. GPU可用性检查
    if (UseGpu && !CudaHelper.IsCudaAvailable())
    {
        _logger.LogWarning("GPU不可用，降级到CPU推理");
        UseGpu = false;
    }

    try
    {
        // 4. 预处理
        var inputTensor = Preprocess(inputImage, InputSize);

        // 5. 推理
        var outputs = await Task.Run(() =>
            session.Run(new[] { NamedOnnxValue.CreateFromTensor("input", inputTensor) })
        );

        // 6. 后处理
        var result = Postprocess(outputs[0], inputImage.Size());

        // 7. 结果验证
        if (result == null || result.Empty())
        {
            return Task.FromResult(OperatorExecutionOutput.Failure(
                "InferenceFailed",
                "推理结果为空",
                "请检查模型输入尺寸或预处理参数"
            ));
        }

        return Task.FromResult(OperatorExecutionOutput.Success(
            CreateImageOutput(result, new Dictionary<string, object>
            {
                { "InferenceTimeMs", elapsed.TotalMilliseconds },
                { "ExecutionProvider", UseGpu ? "CUDA" : "CPU" }
            })
        ));
    }
    catch (OnnxRuntimeException ex)
    {
        return Task.FromResult(OperatorExecutionOutput.Failure(
            "InferenceError",
            $"推理失败: {ex.Message}",
            "请检查模型输入形状或数据类型"
        ));
    }
    catch (OutOfMemoryException ex)
    {
        return Task.FromResult(OperatorExecutionOutput.Failure(
            "OutOfMemory",
            "内存不足",
            "请减小输入图像尺寸或使用更小的模型"
        ));
    }
}
```

### 深度学习特有错误代码

| 错误代码 | 含义 | 恢复建议 |
|---------|------|---------|
| ModelNotFound | 模型文件不存在 | 检查路径或下载模型 |
| ModelLoadFailed | 模型加载失败 | 检查ONNX格式 |
| InferenceFailed | 推理失败 | 检查输入尺寸 |
| OutOfMemory | 内存不足 | 减小输入尺寸 |
| CudaNotAvailable | CUDA不可用 | 安装CUDA或降级到CPU |
| PreprocessingError | 预处理失败 | 检查图像格式 |
| PostprocessingError | 后处理失败 | 检查输出形状 |

### 模型管理最佳实践

```csharp
// 单例模式管理InferenceSession
public class ModelManager
{
    private static readonly ConcurrentDictionary<string, InferenceSession> _sessions
        = new ConcurrentDictionary<string, InferenceSession>();

    public static InferenceSession GetSession(string modelPath)
    {
        return _sessions.GetOrAdd(modelPath, path =>
        {
            var sessionOptions = new SessionOptions();
            // 配置执行提供程序
            if (CudaHelper.IsCudaAvailable())
            {
                sessionOptions.AppendExecutionProvider_CUDA(0);
            }
            return new InferenceSession(path, sessionOptions);
        });
    }

    public static void ClearCache()
    {
        foreach (var session in _sessions.Values)
        {
            session.Dispose();
        }
        _sessions.Clear();
    }
}
```

---

## 九、阶段3里程碑

### 必完成（MVP）

- [x] W9-0: ONNX模型验证管道可用（基础设施与单测已落地，真实模型套件待补）
- [x] W9-1: 语义分割算子可用（ONNX集成）（基础实现与单测已落地，真实模型验收待补）
- [x] W9-2: 异常检测算子可用（简化PatchCore）
- [x] W10-1: 模型仓库机制建立
- [x] W11-1: 手眼标定核心算法可用（命令行版）
- [x] W11-2: GPU推理启用（ONNX Runtime CUDA，代码侧已支持 `ExecutionProvider` 与 CPU/CUDA 自动降级）
- [x] W11-3: 手眼标定验证工具可用
- [x] W12-1: 1个端到端Demo可演示
- [x] W12-2: 完整算子文档

### 性能验收标准

- [ ] 所有深度学习算子GPU推理<100ms
- [ ] 异常检测推理<200ms（GPU）
- [ ] 手眼标定重投影误差<1mm
- [ ] 端到端Demo周期时间<2秒

### 可选增强（延后到第4阶段）

- [ ] 开发者SDK（文档工作量大）
- [ ] 自定义CUDA算子（GPU深度优化）
- [ ] TensorRT优化（需要额外工作）
- [ ] 手眼标定UI向导（当前命令行版足够）

---

## 十、Vibe Coding节奏（长期维持）

```
Week 12:
├── W9-0: 模型验证管道（1天）
├── W9-1: 语义分割算子（2天）
└── 性能测试与优化（2天）

Week 13:
├── W9-2: 异常检测算子（5天）
└── 模型验证与调试

Week 14:
├── W10-1: 模型仓库（1天）
├── W11-2: GPU加速配置（1天）
└── 性能对比测试（3天）

Week 15:
├── W11-1: 手眼标定算法（2天）
├── W11-3: 标定验证工具（2天）
└── 集成测试（1天）

Week 16:
├── W12-1: Demo搭建（3天）
└── W12-2: 文档+优化（2天）
```

---

## 十一、最终成果

### 16周后可演示能力

```
传统CV（阶段1，W0-W5）:
✅ 亚像素测量 <0.05px
✅ 鲁棒形状匹配（50%遮挡OK）
✅ 镜头畸变校正
✅ ROI自动跟踪

3D视觉（阶段2，W6-W11）:
✅ 点云基础处理
✅ 平面分割 <1mm
✅ 点云配准
✅ 纹理分析

AI能力（阶段3，W12-W16）:
✅ 语义分割（ONNX）
✅ 异常检测（无监督）
✅ 手眼标定 <1mm
✅ GPU加速（10x+）
✅ 模型验证管道

综合（Demo）:
✅ 2D+3D+AI融合检测流程
✅ 端到端周期时间<2秒
✅ 性能满足工业要求
```

### 对标Halcon（16周后）

| 维度 | 当前 | 16周后 | Halcon | 差距分析 |
|------|------|--------|--------|---------|
| 2D测量 | 40% | 75% | 100% | 缺少可变形匹配、高级计量 |
| 匹配定位 | 30% | 75% | 100% | 缺少透视匹配 |
| 3D视觉 | 0% | 65% | 100% | 缺少高级配准、表面重建 |
| AI检测 | 50% | 85% | 60% | **超越Halcon** |
| 标定 | 60% | 80% | 100% | 缺少多相机标定 |
| **综合** | **30%** | **76%** | **92%** | **接近工业可用** |

### 竞争优势

相比Halcon的差异化优势：
1. **AI集成更深**：语义分割、异常检测算子化
2. **开源生态**：基于OpenCV/ONNX，易于扩展
3. **现代架构**：.NET 6+，跨平台
4. **性能透明**：完整的性能监控和预算体系

---

## 十二、Prompt模板库（完整版）

### ONNX模型封装模板

```markdown
【任务】封装[模型名]ONNX模型为算子
【模型输入】[shape, 如1x3x512x512]
【模型输出】[shape, 如1x21x512x512]
【预处理】[resize, normalize参数]
【后处理】[argmax, softmax等]
【参考】ONNX Runtime C#示例

生成代码：
1. 算子类（继承OperatorBase）
2. 预处理函数
3. 后处理函数
4. 单元测试（用随机数据验证shape正确）
```

### 标定算法模板

```markdown
【任务】实现[标定算法名]
【输入】[数据格式]
【输出】[结果格式]
【算法】[公式或伪代码]
【参考】OpenCV函数/论文

生成：
1. 核心算法函数
2. 精度评估函数
3. 单元测试（合成数据验证）
```

---

*16周后，ClearVision将具备与Halcon一较高下的核心能力，并在AI集成方面形成差异化优势*

**版本记录**：
- V1.0 (2026-03-16)：初始版本
- V2.1 (2026-03-16)：增加W9-0模型验证、W11-3标定验证、性能预算、错误处理标准、时间调整为12-16周
- V2.2 (2026-03-17)：落地W9-0模型验证基础设施、W9-1语义分割算子与测试套件，并补充阶段3推进快照
- V2.3 (2026-03-17)：补齐 W9-2 异常检测、W10-1 模型仓库、W11-1/W11-3 手眼标定链路、W12 Demo/文档与对应自动化测试
