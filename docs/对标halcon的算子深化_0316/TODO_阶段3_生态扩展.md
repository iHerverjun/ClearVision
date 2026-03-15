# 阶段3 TODO：生态扩展（Vibe Coding版）

> **阶段目标**: AI+3D融合，形成差异化竞争力  
> **时间周期**: 第9-12周  
> **前置条件**: 阶段2里程碑达成（3D点云基础可用）  
> **核心理念**: 深度学习算子化，端到端场景闭环

---

## 一、本阶段任务总览

```
Week 9-10: 深度学习算子（分割/异常检测）
Week 11:   手眼标定向导 + GPU加速基础
Week 12:   集成优化 + 场景Demo
```

### 任务筛选原则

| 任务 | AI友好度 | 是否纳入 | 理由 |
|------|---------|----------|------|
| 语义分割ONNX | ⭐⭐⭐⭐⭐ | ✅ | 模型转换现成，只需封装 |
| 异常检测 | ⭐⭐⭐⭐☆ | ✅ | 可用开源模型，重点在数据流 |
| 手眼标定 | ⭐⭐⭐☆☆ | ✅ | 算法明确，UI简化先做命令行版 |
| GPU加速 | ⭐⭐☆☆☆ | ⚠️ | 可选，先用ONNX Runtime GPU |
| 开发者SDK | ⭐⭐⭐☆☆ | ⚠️ | 文档工作量大，延后 |

---

## 二、Week 9-10：深度学习算子

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
protected override async Task<OperatorExecutionResult> ExecuteAsync(...)
{
    // 1. 加载ONNX模型（缓存）
    using var session = new InferenceSession(ModelPath);
    
    // 2. 预处理
    var inputTensor = Preprocess(Image, InputSize);
    
    // 3. 推理
    var outputs = session.Run(new[] { NamedOnnxValue.CreateFromTensor("input", inputTensor) });
    
    // 4. 后处理
    var segMap = Postprocess(outputs[0], originalSize);
    
    return OperatorExecutionResult.Success(new { SegmentationMap = segMap });
}
```

【测试模型】
- 下载现成的ONNX分割模型
- 或使用PyTorch导出自定义模型

【验收】
- 512x512图像推理<100ms（GPU）
- mIoU与原始模型一致
```

---

### 任务W9-2：异常检测算子（PatchCore集成）（6小时）

```markdown
【任务】实现无监督异常检测算子
【核心价值】无需缺陷样本，只学正常样本即可检测异常

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
```

---

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

## 三、Week 11：手眼标定 + GPU加速

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

【使用流程（命令行版）】
1. 采集N组数据（机器人位姿+对应图像）
2. 检测每张图的标定板位姿
3. 调用算子计算手眼矩阵
4. 验证：转换后的点应在预期位置

【验收】
- 重投影误差 < 1mm
- 采集8-10个点即可稳定收敛
```

---

### 任务W11-2：GPU加速基础（ONNX Runtime GPU）（2小时）

```markdown
【任务】启用ONNX Runtime GPU推理
【实现】极其简单，只需配置SessionOptions

```csharp
var sessionOptions = new SessionOptions();
sessionOptions.AppendExecutionProvider_CUDA(0);  // GPU 0
// sessionOptions.AppendExecutionProvider_DirectML(0);  // 或用DirectML

using var session = new InferenceSession(modelPath, sessionOptions);
```

【算子参数】
```csharp
[Parameter("ExecutionProvider", "cuda", "cpu/cuda/directml/tensorrt")]
```

【性能对比测试】
| 模型 | CPU | CUDA | 加速比 |
|------|-----|------|--------|
| YOLOv8s | 200ms | 20ms | 10x |
| DeepLabV3 | 500ms | 30ms | 16x |
| PatchCore | 100ms | 15ms | 6x |
```

---

## 四、Week 12：集成优化 + 场景Demo

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

## 五、阶段3里程碑

### 必完成（MVP）

- [ ] 语义分割算子可用（ONNX集成）
- [ ] 异常检测算子可用（简化PatchCore）
- [ ] 手眼标定核心算法可用（命令行版）
- [ ] 1个端到端Demo可演示

### 可选增强

- [ ] GPU推理启用（ONNX Runtime CUDA）
- [ ] 模型仓库机制
- [ ] 完整算子文档

---

## 六、Vibe Coding节奏（长期维持）

```
Week 9-10:
├── 每日1个深度学习算子封装
├── 重点在数据预处理/后处理
└── 模型用现成的ONNX

Week 11:
├── 手眼标定算法（3天）
└── GPU加速配置（1天）

Week 12:
├── Demo搭建（3天）
└── 文档+优化（2天）
```

---

## 七、最终成果

### 12周后可演示能力

```
传统CV（阶段1）:
✅ 亚像素测量 <0.05px
✅ 鲁棒形状匹配（30%遮挡OK）

3D视觉（阶段2）:
✅ 点云基础处理
✅ 平面分割 <1mm

AI能力（阶段3）:
✅ 语义分割
✅ 异常检测（无监督）
✅ 手眼标定 <1mm

综合（Demo）:
✅ 2D+3D+AI融合检测流程
```

### 对标Halcon

| 维度 | 当前 | 12周后 | Halcon |
|------|------|--------|--------|
| 2D测量 | 40% | 75% | 100% |
| 匹配定位 | 30% | 70% | 100% |
| 3D视觉 | 0% | 60% | 100% |
| AI检测 | 50% | 80% | 60% |
| **综合** | **30%** | **71%** | **90%** |

---

## 八、Prompt模板库（完整版）

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

*12周后，ClearVision将具备与Halcon一较高下的核心能力*
