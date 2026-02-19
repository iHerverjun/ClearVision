# ClearVision 项目结构化分析文档

> **生成日期**: 2026-02-19  
> **项目**: ClearVision 工业视觉检测平台  
> **用途**: AI自动生成工作流 - 结构化信息提取

---

## 第一章：技术栈与项目架构

### 1.1 应用类型

**桌面应用 + 混合架构**
- **宿主层**: WinForms + WebView2 (Windows桌面应用)
- **前端**: HTML5/JavaScript/CSS3 (在WebView2中运行)
- **后端**: .NET 8 C# 服务
- **通信**: WebView2双向消息通信

### 1.2 编程语言与框架版本

| 组件 | 技术 | 版本 |
|------|------|------|
| 运行时 | .NET | 8.0 |
| 语言 | C# | 12 |
| 图像处理 | OpenCvSharp | 4.9 |
| 深度学习 | ONNX Runtime | 最新版 |
| 前端 | HTML/JS/CSS | ES6+ |
| 数据库 | SQLite + EF Core | 8.0 |
| DI容器 | Microsoft.Extensions.DependencyInjection | 8.0 |

### 1.3 图形画布/节点编辑器方案

**完全自研 Canvas 实现**

关键文件位置:
- `Acme.Product.Desktop/wwwroot/src/core/canvas/flowCanvas.js` - 核心画布实现
- 使用 HTML5 Canvas API 自研
- 支持拖拽、连线、缩放、框选、撤销/重做

核心类/对象:
```javascript
class FlowCanvas {
    nodes: Map<string, Node>      // 节点集合
    connections: Array<Connection> // 连接集合
    scale: number                 // 缩放比例
    offset: {x, y}               // 视口偏移
    // ... 方法: addNode, removeNode, addConnection, serialize, deserialize
}
```

### 1.4 图像处理后端

**OpenCvSharp (OpenCV 4.9 的 .NET 绑定)**

主要图像处理库:
- **OpenCvSharp** - 核心图像处理、传统视觉算法
- **ONNX Runtime** - 深度学习模型推理 (YOLO系列)
- **ZBar/ZXing** - 条码识别

### 1.5 项目结构总览

```
Acme.Product/
├── src/
│   ├── Acme.Product.Core/              # 🔵 领域核心层
│   │   ├── Entities/                   #    领域实体
│   │   │   ├── Operator.cs            #    算子实体定义
│   │   │   ├── OperatorFlow.cs        #    流程实体(包含算子和连接)
│   │   │   ├── Project.cs             #    工程实体
│   │   │   └── ...
│   │   ├── Operators/                  #    算子接口
│   │   │   └── IOperatorExecutor.cs   #    算子执行器接口
│   │   ├── Services/                   #    领域服务接口
│   │   │   ├── IOperatorFactory.cs    #    算子工厂接口
│   │   │   └── IFlowExecutionService.cs #  流程执行服务接口
│   │   ├── ValueObjects/               #    值对象
│   │   │   └── VisionValueObjects.cs  #    Port, Parameter, Connection等
│   │   └── Enums/                      #    枚举定义
│   │       └── OperatorEnums.cs       #    OperatorType, PortDataType等
│   │
│   ├── Acme.Product.Application/       # 🟢 应用服务层
│   │   ├── DTOs/                       #    数据传输对象
│   │   │   ├── ProjectDto.cs          #    工程DTO
│   │   │   ├── OperatorDto.cs         #    算子DTO
│   │   │   └── OperatorFlowDto.cs     #    流程DTO
│   │   └── Services/                   #    应用服务
│   │
│   ├── Acme.Product.Contracts/         # 🟡 契约层(WebView2消息)
│   │   └── Messages/                   #    消息定义
│   │
│   ├── Acme.Product.Infrastructure/    # 🟠 基础设施层
│   │   ├── Operators/                  #    46个算子实现
│   │   │   ├── OperatorBase.cs        #    算子基类
│   │   │   ├── ImageAcquisitionOperator.cs
│   │   │   ├── CannyEdgeOperator.cs
│   │   │   ├── ConditionalBranchOperator.cs
│   │   │   └── ... (共46个算子)
│   │   ├── Services/                   #    服务实现
│   │   │   ├── FlowExecutionService.cs #    流程执行服务
│   │   │   ├── OperatorFactory.cs     #    算子工厂(元数据注册)
│   │   │   ├── ProjectJsonSerializer.cs #  工程JSON序列化
│   │   │   └── JsonFileProjectFlowStorage.cs # 工程存储
│   │   ├── ImageProcessing/            #    图像处理算法
│   │   └── Persistence/                #    数据持久化
│   │
│   └── Acme.Product.Desktop/           # 🔴 桌面宿主层
│       ├── wwwroot/                    #    前端资源
│       │   ├── src/
│       │   │   ├── core/canvas/       #    画布核心
│       │   │   │   └── flowCanvas.js  #    流程画布实现
│       │   │   ├── features/          #    功能模块
│       │   │   └── ...
│       │   └── index.html
│       └── Program.cs                  #    WinForms入口
│
└── tests/                              # ✅ 测试项目
    └── Acme.Product.Tests/
```

### 1.6 算子注册/发现机制

**硬编码注册 + 元数据驱动**

算子在 `OperatorFactory.cs` 中通过元数据硬编码注册，非反射动态发现。

**核心代码位置**: `Acme.Product.Infrastructure/Services/OperatorFactory.cs` (第90行起)

```csharp
private void InitializeDefaultOperators()
{
    // 图像采集
    _metadata[OperatorType.ImageAcquisition] = new OperatorMetadata
    {
        Type = OperatorType.ImageAcquisition,
        DisplayName = "图像采集",
        Description = "从文件或相机采集图像",
        Category = "采集",
        IconName = "camera",
        InputPorts = new List<PortDefinition>(),
        OutputPorts = new List<PortDefinition>
        {
            new() { Name = "Image", DisplayName = "图像", DataType = PortDataType.Image }
        },
        Parameters = new List<ParameterDefinition>
        {
            new() { Name = "sourceType", DisplayName = "采集源", DataType = "enum", ... },
            new() { Name = "filePath", DisplayName = "文件路径", DataType = "file", ... }
        }
    };
    // ... 其他算子
}
```

**新增算子需要修改的文件**:
1. `Acme.Product.Core/Enums/OperatorEnums.cs` - 添加新的算子类型枚举值
2. `Acme.Product.Infrastructure/Services/OperatorFactory.cs` - 添加算子元数据
3. `Acme.Product.Infrastructure/Operators/` - 创建算子执行器类（继承OperatorBase）
4. `Acme.Product.Desktop/wwwroot/src/features/operator-library/operatorLibrary.js` - 添加前端图标映射

### 1.7 状态管理与执行引擎

**拓扑排序 + 层级并行执行**

执行流程核心代码位置: `FlowExecutionService.cs`

**执行流程**:
1. 调用 `ExecuteFlowAsync()` 开始执行
2. 获取拓扑排序的执行顺序: `flow.GetExecutionOrder()`
3. 构建执行层级: `BuildExecutionLayers()` - 将无依赖的算子分到同一层
4. 顺序或并行执行各层级算子
5. 每个算子超时保护: 30秒

**拓扑排序实现** (`OperatorFlow.cs` 第128-160行):
```csharp
public IEnumerable<Operator> GetExecutionOrder()
{
    var visited = new HashSet<Guid>();
    var result = new List<Operator>();

    foreach (var op in _operators)
    {
        VisitOperator(op, visited, result);
    }
    return result;
}

private void VisitOperator(Operator op, HashSet<Guid> visited, List<Operator> result)
{
    if (visited.Contains(op.Id)) return;
    visited.Add(op.Id);
    
    // 先访问依赖的算子
    var dependencies = _connections
        .Where(c => c.TargetOperatorId == op.Id)
        .Select(c => _operators.FirstOrDefault(o => o.Id == c.SourceOperatorId))
        .Where(o => o != null);
    
    foreach (var dep in dependencies)
    {
        VisitOperator(dep!, visited, result);
    }
    result.Add(op);
}
```

**入口函数签名**:
```csharp
// 执行完整流程
Task<FlowExecutionResult> ExecuteFlowAsync(
    OperatorFlow flow,
    Dictionary<string, object>? inputData = null,
    bool enableParallel = false,
    CancellationToken cancellationToken = default);

// 执行单个算子
Task<OperatorExecutionResult> ExecuteOperatorAsync(
    Operator @operator, 
    Dictionary<string, object>? inputs = null);
```

---

## 第二章：数据类型体系（端口类型系统）

### 2.1 所有数据类型清单

定义位置: `Acme.Product.Core/Enums/OperatorEnums.cs` (第448-494行)

| 类型标识 | 简称 | 说明 | 对应的实际类/结构体 |
|----------|------|------|---------------------|
| `PortDataType.Image` (0) | Image | 图像数据 | `ImageWrapper` (封装OpenCvSharp.Mat) |
| `PortDataType.Integer` (1) | Integer | 整数值 | `int` / `long` |
| `PortDataType.Float` (2) | Float | 浮点数值 | `float` / `double` |
| `PortDataType.Boolean` (3) | Boolean | 布尔值 | `bool` |
| `PortDataType.String` (4) | String | 字符串 | `string` |
| `PortDataType.Point` (5) | Point | 2D坐标点 | `Point` / `Point2f` |
| `PortDataType.Rectangle` (6) | Rectangle | 矩形区域 | `Rect` / `Rect2f` |
| `PortDataType.Contour` (7) | Contour | 轮廓/区域数据 | `Mat` / `Point[][]` |
| `PortDataType.Any` (99) | Any | 任意类型 | `object` |

**前端颜色映射** (`flowCanvas.js` 第4-18行):
```javascript
const PORT_TYPE_COLORS = {
    'Image':     '#52c41a',  // 绿色
    'String':    '#1890ff',  // 蓝色
    'Integer':   '#fa8c16',  // 橙色
    'Float':     '#fa8c16',  // 橙色
    'Boolean':   '#f5222d',  // 红色
    'Point':     '#eb2f96',  // 粉色
    'Rectangle': '#eb2f96',  // 粉色
    'Contour':   '#722ed1',  // 紫色
    'Any':       '#bfbfbf',  // 灰色
};
```

### 2.2 类型继承/兼容关系

**类型兼容性规则**:

1. **Any 类型兼容所有类型** - `Any` 类型的输入端口可以接受任何类型的数据
2. **同类型完全兼容** - `Image` → `Image`, `Integer` → `Integer` 等
3. **数值类型兼容性**: `Integer` 和 `Float` 视为兼容 (`Number` 类别)
4. **几何类型兼容性**: `Point` 和 `Rectangle` 视为兼容 (`Geometry` 类别)

**兼容性检查代码** (`flowCanvas.js` 第743-761行):
```javascript
checkTypeCompatibility(sourceType, targetType) {
    const normalize = (t) => {
        if (t === 'Any' || t === 99) return 'Any';
        if (t === 'Image' || t === 0) return 'Image';
        if (t === 'Integer' || t === 1 || t === 'Float' || t === 2) return 'Number';
        if (t === 'Boolean' || t === 3) return 'Boolean';
        if (t === 'String' || t === 4) return 'String';
        if (t === 'Point' || t === 5 || t === 'Rectangle' || t === 6) return 'Geometry';
        if (t === 'Contour' || t === 7) return 'Contour';
        return t;
    };

    const s = normalize(sourceType);
    const t = normalize(targetType);

    if (s === 'Any' || t === 'Any') return true;
    return s === t;
}
```

后端兼容性检查 (`OperatorFlow.cs` 第183-188行):
```csharp
if (sourcePort.DataType != targetPort.DataType &&
    sourcePort.DataType != PortDataType.Any &&
    targetPort.DataType != PortDataType.Any)
{
    throw new InvalidOperationException($"端口数据类型不匹配...");
}
```

### 2.3 连线规则

**规则说明与代码位置**:

| 规则 | 说明 | 代码位置 |
|------|------|----------|
| **一对多（扇出）** | 一个输出端口可以连接到多个输入端口 | ✅ 支持 - 输出端口可有多条连接 |
| **多对一（扇入）** | 一个输入端口只能接收一个输出端口 | ❌ 禁止 - 会报错 "目标输入端口已被占用" |
| **环路检测** | 不允许形成循环依赖 | ✅ 支持检测 - `WouldCreateCycle()` |
| **自连接** | 同一个算子的输出不能连接到自己的输入 | ✅ 禁止 - "不能连接到自己" |
| **类型检查** | 连线时检查类型兼容性 | ✅ `checkTypeCompatibility()` |

**关键代码片段**:

1. **输入端口占用检查** (`flowCanvas.js` 第701-710行):
```javascript
// 检查输入端口是否已被占用（一个输入端口只能有一个连接）
const targetPortOccupied = this.connections.find(conn =>
    conn.target === nodeId &&
    conn.targetPort === portIndex
);

if (targetPortOccupied) {
    console.warn('[FlowCanvas] 目标输入端口已被占用');
    this.cancelConnection();
    return;
}
```

2. **环路检测** (`OperatorFlow.cs` 第197-223行):
```csharp
private bool WouldCreateCycle(OperatorConnection newConnection)
{
    var visited = new HashSet<Guid>();
    return HasCycle(newConnection.TargetOperatorId, newConnection.SourceOperatorId, visited);
}

private bool HasCycle(Guid current, Guid target, HashSet<Guid> visited)
{
    if (current == target) return true;
    if (!visited.Add(current)) return true; // 已访问过，说明有环
    
    var nextOperators = _connections
        .Where(c => c.SourceOperatorId == current)
        .Select(c => c.TargetOperatorId);
    
    foreach (var next in nextOperators)
    {
        if (HasCycle(next, target, visited)) return true;
    }
    return false;
}
```

3. **自连接检查** (`flowCanvas.js` 第680-684行):
```javascript
// 检查连接有效性
if (this.connectingFrom.nodeId === nodeId) {
    console.warn('[FlowCanvas] 不能连接到自己');
    this.cancelConnection();
    return;
}
```

---

*文档待续 - 下一章：第三章（算子注册表）*

## 第三章：算子注册表（逐个算子详细描述）

### 算子类型枚举定义

位置: `Acme.Product.Core/Enums/OperatorEnums.cs` (第12-334行)

所有算子按功能分类如下:

### 3.1 完整算子JSON数组

```json
[
  {
    "operator_id": "ImageAcquisition",
    "name": "图像采集",
    "english_name": "Image Acquisition",
    "category": "图像源",
    "description": "从文件或相机采集图像。支持本地图像文件加载和工业相机实时采集。",
    "keywords": ["图像采集", "相机", "文件加载", "图像源", "camera", "acquisition", "input"],
    "inputs": [],
    "outputs": [
      {
        "port_name": "Image",
        "display_name": "图像",
        "data_type": "Image",
        "description": "采集到的图像数据"
      }
    ],
    "params": [
      {
        "param_name": "sourceType",
        "display_name": "采集源",
        "type": "enum",
        "default": "file",
        "range": ["file", "camera"],
        "description": "图像来源类型：file=文件, camera=相机"
      },
      {
        "param_name": "filePath",
        "display_name": "文件路径",
        "type": "file",
        "default": "",
        "description": "图像文件完整路径"
      },
      {
        "param_name": "cameraId",
        "display_name": "相机",
        "type": "cameraBinding",
        "default": "",
        "description": "绑定的相机设备ID"
      },
      {
        "param_name": "exposureTime",
        "display_name": "曝光时间",
        "type": "double",
        "default": 5000.0,
        "range": [1.0, 1000000.0],
        "description": "相机曝光时间(微秒)"
      },
      {
        "param_name": "gain",
        "display_name": "增益",
        "type": "double",
        "default": 1.0,
        "range": [1.0, 20.0],
        "description": "相机增益倍数"
      },
      {
        "param_name": "triggerMode",
        "display_name": "触发模式",
        "type": "enum",
        "default": "Software",
        "range": ["Software", "Hardware"],
        "description": "触发模式：Software=软触发, Hardware=外触发"
      }
    ],
    "notes": "无输入端口，只能作为流程起点"
  },
  {
    "operator_id": "Filtering",
    "name": "滤波",
    "english_name": "Gaussian Blur",
    "category": "预处理",
    "description": "高斯模糊滤波，用于图像降噪和平滑处理。",
    "keywords": ["滤波", "高斯模糊", "降噪", "平滑", "gaussian", "blur", "filter"],
    "inputs": [
      {
        "port_name": "Image",
        "display_name": "图像",
        "data_type": "Image",
        "required": true,
        "description": "输入图像"
      }
    ],
    "outputs": [
      {
        "port_name": "Image",
        "display_name": "图像",
        "data_type": "Image",
        "description": "滤波后的图像"
      }
    ],
    "params": [
      {
        "param_name": "KernelSize",
        "display_name": "核大小",
        "type": "int",
        "default": 5,
        "range": [1, 31],
        "description": "高斯核大小，必须是奇数"
      },
      {
        "param_name": "SigmaX",
        "display_name": "Sigma X",
        "type": "double",
        "default": 1.0,
        "range": [0.1, 10.0],
        "description": "X方向标准差"
      },
      {
        "param_name": "SigmaY",
        "display_name": "Sigma Y",
        "type": "double",
        "default": 0.0,
        "range": [0.0, 10.0],
        "description": "Y方向标准差(0表示与X相同)"
      },
      {
        "param_name": "BorderType",
        "display_name": "边界填充",
        "type": "enum",
        "default": "4",
        "range": ["0:Constant", "1:Replicate", "2:Reflect", "3:Wrap", "4:Default"],
        "description": "边界处理方式"
      }
    ],
    "notes": ""
  },
  {
    "operator_id": "EdgeDetection",
    "name": "边缘检测",
    "english_name": "Canny Edge Detection",
    "category": "边缘检测",
    "description": "Canny边缘检测算法，检测图像中的边缘轮廓。",
    "keywords": ["边缘检测", "Canny", "轮廓", "edge", "canny", "detection"],
    "inputs": [
      {
        "port_name": "Image",
        "display_name": "图像",
        "data_type": "Image",
        "required": true,
        "description": "输入图像"
      }
    ],
    "outputs": [
      {
        "port_name": "Image",
        "display_name": "图像",
        "data_type": "Image",
        "description": "边缘检测结果图像"
      },
      {
        "port_name": "Edges",
        "display_name": "边缘",
        "data_type": "Image",
        "description": "纯边缘图像"
      }
    ],
    "params": [
      {
        "param_name": "Threshold1",
        "display_name": "低阈值",
        "type": "double",
        "default": 50.0,
        "range": [0.0, 255.0],
        "description": "滞后阈值低值"
      },
      {
        "param_name": "Threshold2",
        "display_name": "高阈值",
        "type": "double",
        "default": 150.0,
        "range": [0.0, 255.0],
        "description": "滞后阈值高值"
      },
      {
        "param_name": "EnableGaussianBlur",
        "display_name": "启用高斯模糊",
        "type": "bool",
        "default": true,
        "description": "是否在边缘检测前应用高斯模糊"
      },
      {
        "param_name": "GaussianKernelSize",
        "display_name": "高斯核大小",
        "type": "int",
        "default": 5,
        "range": [3, 15],
        "description": "预处理高斯核大小"
      },
      {
        "param_name": "ApertureSize",
        "display_name": "Sobel孔径",
        "type": "enum",
        "default": "3",
        "range": ["3", "5", "7"],
        "description": "Sobel算子孔径大小"
      }
    ],
    "notes": ""
  },
  {
    "operator_id": "Thresholding",
    "name": "二值化",
    "english_name": "Threshold",
    "category": "预处理",
    "description": "图像阈值分割，支持多种阈值类型和Otsu自动阈值。",
    "keywords": ["二值化", "阈值", "分割", "Otsu", "threshold", "binary"],
    "inputs": [
      {
        "port_name": "Image",
        "display_name": "图像",
        "data_type": "Image",
        "required": true,
        "description": "输入图像"
      }
    ],
    "outputs": [
      {
        "port_name": "Image",
        "display_name": "图像",
        "data_type": "Image",
        "description": "二值化后的图像"
      }
    ],
    "params": [
      {
        "param_name": "Threshold",
        "display_name": "阈值",
        "type": "double",
        "default": 127.0,
        "range": [0.0, 255.0],
        "description": "分割阈值"
      },
      {
        "param_name": "MaxValue",
        "display_name": "最大值",
        "type": "double",
        "default": 255.0,
        "range": [0.0, 255.0],
        "description": "超过阈值的像素值"
      },
      {
        "param_name": "Type",
        "display_name": "类型",
        "type": "enum",
        "default": "0",
        "range": ["0:Binary", "1:Binary Inv", "2:Trunc", "3:To Zero", "4:To Zero Inv", "8:Otsu", "16:Triangle"],
        "description": "阈值类型"
      },
      {
        "param_name": "UseOtsu",
        "display_name": "使用Otsu",
        "type": "bool",
        "default": false,
        "description": "是否使用Otsu自动计算阈值"
      }
    ],
    "notes": ""
  },
  {
    "operator_id": "Morphology",
    "name": "形态学",
    "english_name": "Morphology",
    "category": "形态学",
    "description": "形态学操作，包括腐蚀、膨胀、开运算、闭运算等。",
    "keywords": ["形态学", "腐蚀", "膨胀", "开运算", "闭运算", "morphology", "erode", "dilate"],
    "inputs": [
      {
        "port_name": "Image",
        "display_name": "图像",
        "data_type": "Image",
        "required": true,
        "description": "输入图像(二值或灰度)"
      }
    ],
    "outputs": [
      {
        "port_name": "Image",
        "display_name": "图像",
        "data_type": "Image",
        "description": "形态学处理后的图像"
      }
    ],
    "params": [
      {
        "param_name": "Operation",
        "display_name": "操作类型",
        "type": "enum",
        "default": "Erode",
        "range": ["Erode", "Dilate", "Open", "Close", "Gradient", "TopHat", "BlackHat"],
        "description": "形态学操作类型"
      },
      {
        "param_name": "KernelSize",
        "display_name": "核大小",
        "type": "int",
        "default": 3,
        "range": [1, 21],
        "description": "结构元素大小"
      },
      {
        "param_name": "KernelShape",
        "display_name": "核形状",
        "type": "enum",
        "default": "Rect",
        "range": ["Rect", "Cross", "Ellipse"],
        "description": "结构元素形状"
      },
      {
        "param_name": "Iterations",
        "display_name": "迭代次数",
        "type": "int",
        "default": 1,
        "range": [1, 10],
        "description": "操作迭代次数"
      }
    ],
    "notes": ""
  },
  {
    "operator_id": "BlobAnalysis",
    "name": "Blob分析",
    "english_name": "Blob Analysis",
    "category": "Blob分析",
    "description": "连通区域分析，用于检测和统计二值图像中的连通区域。",
    "keywords": ["Blob分析", "连通区域", "面积分析", "blob", "connected components"],
    "inputs": [
      {
        "port_name": "Image",
        "display_name": "图像",
        "data_type": "Image",
        "required": true,
        "description": "二值输入图像"
      }
    ],
    "outputs": [
      {
        "port_name": "Image",
        "display_name": "标记图像",
        "data_type": "Image",
        "description": "标记后的彩色图像"
      },
      {
        "port_name": "Blobs",
        "display_name": "Blob数据",
        "data_type": "Contour",
        "description": "Blob轮廓数据"
      },
      {
        "port_name": "BlobCount",
        "display_name": "Blob数量",
        "data_type": "Integer",
        "description": "检测到的Blob数量"
      }
    ],
    "params": [
      {
        "param_name": "MinArea",
        "display_name": "最小面积",
        "type": "int",
        "default": 100,
        "range": [0, 999999],
        "description": "最小Blob面积过滤"
      },
      {
        "param_name": "MaxArea",
        "display_name": "最大面积",
        "type": "int",
        "default": 100000,
        "range": [0, 999999],
        "description": "最大Blob面积过滤"
      },
      {
        "param_name": "Color",
        "display_name": "目标颜色",
        "type": "enum",
        "default": "White",
        "range": ["White", "Black"],
        "description": "前景颜色"
      }
    ],
    "notes": "输入必须是二值图像"
  },
  {
    "operator_id": "TemplateMatching",
    "name": "模板匹配",
    "english_name": "Template Matching",
    "category": "模板匹配",
    "description": "在图像中搜索模板图像的位置，返回匹配分数和坐标。",
    "keywords": ["模板匹配", "匹配定位", "相关性", "template", "matching", "NCC"],
    "inputs": [
      {
        "port_name": "Image",
        "display_name": "输入图像",
        "data_type": "Image",
        "required": true,
        "description": "待搜索的图像"
      },
      {
        "port_name": "Template",
        "display_name": "模板图像",
        "data_type": "Image",
        "required": true,
        "description": "要搜索的模板图像"
      }
    ],
    "outputs": [
      {
        "port_name": "Image",
        "display_name": "结果图像",
        "data_type": "Image",
        "description": "显示匹配结果的图像"
      },
      {
        "port_name": "Position",
        "display_name": "匹配位置",
        "data_type": "Point",
        "description": "最佳匹配位置坐标"
      },
      {
        "port_name": "Score",
        "display_name": "匹配分数",
        "data_type": "Float",
        "description": "匹配相似度分数(0-1)"
      },
      {
        "port_name": "IsMatch",
        "display_name": "是否匹配",
        "data_type": "Boolean",
        "description": "是否匹配成功"
      }
    ],
    "params": [
      {
        "param_name": "Method",
        "display_name": "匹配方法",
        "type": "enum",
        "default": "NCC",
        "range": ["NCC", "SQDiff", "CCORR"],
        "description": "匹配算法"
      },
      {
        "param_name": "Threshold",
        "display_name": "匹配分数阈值",
        "type": "double",
        "default": 0.8,
        "range": [0.1, 1.0],
        "description": "判定匹配成功的最低分数"
      },
      {
        "param_name": "MaxMatches",
        "display_name": "最大匹配数",
        "type": "int",
        "default": 1,
        "range": [1, 100],
        "description": "返回的最大匹配数"
      }
    ],
    "notes": "支持多目标匹配"
  },
  {
    "operator_id": "Measurement",
    "name": "测量",
    "english_name": "Distance Measurement",
    "category": "测量",
    "description": "几何测量，支持点到点、水平、垂直距离测量。",
    "keywords": ["测量", "距离", "尺寸", "measurement", "distance"],
    "inputs": [
      {
        "port_name": "Image",
        "display_name": "输入图像",
        "data_type": "Image",
        "required": true,
        "description": "输入图像(用于显示结果)"
      }
    ],
    "outputs": [
      {
        "port_name": "Image",
        "display_name": "结果图像",
        "data_type": "Image",
        "description": "显示测量结果的图像"
      },
      {
        "port_name": "Distance",
        "display_name": "测量距离",
        "data_type": "Float",
        "description": "测量得到的距离(像素)"
      }
    ],
    "params": [
      {
        "param_name": "X1",
        "display_name": "起点X",
        "type": "int",
        "default": 0,
        "description": "测量起点X坐标"
      },
      {
        "param_name": "Y1",
        "display_name": "起点Y",
        "type": "int",
        "default": 0,
        "description": "测量起点Y坐标"
      },
      {
        "param_name": "X2",
        "display_name": "终点X",
        "type": "int",
        "default": 100,
        "description": "测量终点X坐标"
      },
      {
        "param_name": "Y2",
        "display_name": "终点Y",
        "type": "int",
        "default": 100,
        "description": "测量终点Y坐标"
      },
      {
        "param_name": "MeasureType",
        "display_name": "测量类型",
        "type": "enum",
        "default": "PointToPoint",
        "range": ["PointToPoint", "Horizontal", "Vertical"],
        "description": "测量方式"
      }
    ],
    "notes": ""
  },
  {
    "operator_id": "CodeRecognition",
    "name": "条码识别",
    "english_name": "Code Recognition",
    "category": "识别",
    "description": "一维码/二维码识别，支持Code128、QR、DataMatrix等多种码制。",
    "keywords": ["条码识别", "二维码", "QR码", "barcode", "QR", "DataMatrix"],
    "inputs": [
      {
        "port_name": "Image",
        "display_name": "输入图像",
        "data_type": "Image",
        "required": true,
        "description": "包含条码的图像"
      }
    ],
    "outputs": [
      {
        "port_name": "Image",
        "display_name": "结果图像",
        "data_type": "Image",
        "description": "标记识别结果的图像"
      },
      {
        "port_name": "Text",
        "display_name": "识别内容",
        "data_type": "String",
        "description": "条码文本内容"
      },
      {
        "port_name": "CodeCount",
        "display_name": "识别数量",
        "data_type": "Integer",
        "description": "识别到的条码数量"
      },
      {
        "port_name": "CodeType",
        "display_name": "条码类型",
        "data_type": "String",
        "description": "条码类型"
      }
    ],
    "params": [
      {
        "param_name": "CodeType",
        "display_name": "码制类型",
        "type": "enum",
        "default": "All",
        "range": ["All", "QR", "Code128", "DataMatrix", "EAN13", "Code39"],
        "description": "要识别的条码类型"
      },
      {
        "param_name": "MaxResults",
        "display_name": "最大结果数",
        "type": "int",
        "default": 10,
        "range": [1, 100],
        "description": "最大识别数量"
      }
    ],
    "notes": "支持多码识别"
  },
  {
    "operator_id": "DeepLearning",
    "name": "深度学习",
    "english_name": "Deep Learning (YOLO)",
    "category": "AI检测",
    "description": "基于YOLO的深度学习缺陷检测，支持YOLOv5/v6/v8/v11。",
    "keywords": ["深度学习", "AI检测", "YOLO", "缺陷检测", "deep learning", "AI"],
    "inputs": [
      {
        "port_name": "Image",
        "display_name": "输入图像",
        "data_type": "Image",
        "required": true,
        "description": "待检测图像"
      }
    ],
    "outputs": [
      {
        "port_name": "Image",
        "display_name": "结果图像",
        "data_type": "Image",
        "description": "标注检测结果的图像"
      },
      {
        "port_name": "Defects",
        "display_name": "缺陷列表",
        "data_type": "Contour",
        "description": "检测到的缺陷区域"
      },
      {
        "port_name": "DefectCount",
        "display_name": "缺陷数量",
        "data_type": "Integer",
        "description": "检测到的缺陷数量"
      }
    ],
    "params": [
      {
        "param_name": "ModelPath",
        "display_name": "模型路径",
        "type": "file",
        "default": "",
        "description": "ONNX模型文件路径"
      },
      {
        "param_name": "Confidence",
        "display_name": "置信度阈值",
        "type": "double",
        "default": 0.5,
        "range": [0.0, 1.0],
        "description": "检测置信度阈值"
      },
      {
        "param_name": "ModelVersion",
        "display_name": "YOLO版本",
        "type": "enum",
        "default": "Auto",
        "range": ["Auto", "YOLOv5", "YOLOv6", "YOLOv8", "YOLOv11"],
        "description": "YOLO模型版本"
      },
      {
        "param_name": "InputSize",
        "display_name": "输入尺寸",
        "type": "int",
        "default": 640,
        "range": [320, 1280],
        "description": "模型输入尺寸"
      },
      {
        "param_name": "TargetClasses",
        "display_name": "目标类别",
        "type": "string",
        "default": "",
        "description": "要检测的类别(逗号分隔，如person,car)"
      },
      {
        "param_name": "LabelFile",
        "display_name": "标签文件路径",
        "type": "file",
        "default": "",
        "description": "自定义标签文件路径"
      }
    ],
    "notes": "需要ONNX格式的YOLO模型"
  },
  {
    "operator_id": "ResultOutput",
    "name": "结果输出",
    "english_name": "Result Output",
    "category": "输出",
    "description": "输出检测结果，支持JSON/CSV格式，可保存到文件。",
    "keywords": ["结果输出", "保存", "导出", "output", "export", "save"],
    "inputs": [
      {
        "port_name": "Image",
        "display_name": "图像",
        "data_type": "Image",
        "required": false,
        "description": "结果图像"
      },
      {
        "port_name": "Result",
        "display_name": "结果",
        "data_type": "Any",
        "required": false,
        "description": "任意结果数据"
      }
    ],
    "outputs": [
      {
        "port_name": "Output",
        "display_name": "输出",
        "data_type": "Any",
        "description": "透传输出"
      }
    ],
    "params": [
      {
        "param_name": "Format",
        "display_name": "输出格式",
        "type": "enum",
        "default": "JSON",
        "range": ["JSON", "CSV", "Text"],
        "description": "输出文件格式"
      },
      {
        "param_name": "SaveToFile",
        "display_name": "保存到文件",
        "type": "bool",
        "default": true,
        "description": "是否保存到文件"
      }
    ],
    "notes": "通常作为流程终点"
  },
  {
    "operator_id": "ContourDetection",
    "name": "轮廓检测",
    "english_name": "Contour Detection",
    "category": "特征提取",
    "description": "检测图像中的轮廓，支持多种检索模式和近似方法。",
    "keywords": ["轮廓检测", "轮廓查找", "contour", "边缘"],
    "inputs": [
      {
        "port_name": "Image",
        "display_name": "图像",
        "data_type": "Image",
        "required": true,
        "description": "二值或边缘图像"
      }
    ],
    "outputs": [
      {
        "port_name": "Image",
        "display_name": "结果图像",
        "data_type": "Image",
        "description": "绘制轮廓的图像"
      },
      {
        "port_name": "Contours",
        "display_name": "轮廓数据",
        "data_type": "Contour",
        "description": "轮廓点集数据"
      },
      {
        "port_name": "ContourCount",
        "display_name": "轮廓数量",
        "data_type": "Integer",
        "description": "检测到的轮廓数量"
      }
    ],
    "params": [
      {
        "param_name": "Mode",
        "display_name": "检索模式",
        "type": "enum",
        "default": "External",
        "range": ["External", "List", "Tree", "CComp", "FloodFill"],
        "description": "轮廓检索模式"
      },
      {
        "param_name": "Method",
        "display_name": "近似方法",
        "type": "enum",
        "default": "Simple",
        "range": ["None", "Simple", "TC89_L1", "TC89_KCOS"],
        "description": "轮廓近似方法"
      },
      {
        "param_name": "MinArea",
        "display_name": "最小面积",
        "type": "int",
        "default": 100,
        "description": "最小轮廓面积过滤"
      },
      {
        "param_name": "MaxArea",
        "display_name": "最大面积",
        "type": "int",
        "default": 100000,
        "description": "最大轮廓面积过滤"
      }
    ],
    "notes": ""
  },
  {
    "operator_id": "MedianBlur",
    "name": "中值滤波",
    "english_name": "Median Blur",
    "category": "预处理",
    "description": "中值滤波，有效去除椒盐噪声同时保留边缘。",
    "keywords": ["中值滤波", "椒盐噪声", "median", "blur", "noise"],
    "inputs": [
      {
        "port_name": "Image",
        "display_name": "图像",
        "data_type": "Image",
        "required": true,
        "description": "输入图像"
      }
    ],
    "outputs": [
      {
        "port_name": "Image",
        "display_name": "图像",
        "data_type": "Image",
        "description": "滤波后的图像"
      }
    ],
    "params": [
      {
        "param_name": "KernelSize",
        "display_name": "核大小",
        "type": "int",
        "default": 5,
        "range": [1, 31],
        "description": "滤波核大小，必须是奇数"
      }
    ],
    "notes": ""
  },
  {
    "operator_id": "BilateralFilter",
    "name": "双边滤波",
    "english_name": "Bilateral Filter",
    "category": "预处理",
    "description": "双边滤波，边缘保留的平滑滤波器。",
    "keywords": ["双边滤波", "边缘保留", "bilateral", "filter"],
    "inputs": [
      {
        "port_name": "Image",
        "display_name": "图像",
        "data_type": "Image",
        "required": true,
        "description": "输入图像"
      }
    ],
    "outputs": [
      {
        "port_name": "Image",
        "display_name": "图像",
        "data_type": "Image",
        "description": "滤波后的图像"
      }
    ],
    "params": [
      {
        "param_name": "Diameter",
        "display_name": "直径",
        "type": "int",
        "default": 9,
        "range": [1, 25],
        "description": "滤波直径"
      },
      {
        "param_name": "SigmaColor",
        "display_name": "色彩Sigma",
        "type": "double",
        "default": 75.0,
        "range": [1.0, 255.0],
        "description": "颜色空间滤波sigma"
      },
      {
        "param_name": "SigmaSpace",
        "display_name": "空间Sigma",
        "type": "double",
        "default": 75.0,
        "range": [1.0, 255.0],
        "description": "坐标空间滤波sigma"
      }
    ],
    "notes": ""
  },
  {
    "operator_id": "ImageResize",
    "name": "图像缩放",
    "english_name": "Image Resize",
    "category": "预处理",
    "description": "调整图像尺寸，支持绝对尺寸和比例缩放。",
    "keywords": ["图像缩放", "尺寸调整", "resize", "scale"],
    "inputs": [
      {
        "port_name": "Image",
        "display_name": "图像",
        "data_type": "Image",
        "required": true,
        "description": "输入图像"
      }
    ],
    "outputs": [
      {
        "port_name": "Image",
        "display_name": "图像",
        "data_type": "Image",
        "description": "缩放后的图像"
      }
    ],
    "params": [
      {
        "param_name": "Width",
        "display_name": "目标宽度",
        "type": "int",
        "default": 640,
        "range": [1, 8192],
        "description": "输出图像宽度"
      },
      {
        "param_name": "Height",
        "display_name": "目标高度",
        "type": "int",
        "default": 480,
        "range": [1, 8192],
        "description": "输出图像高度"
      },
      {
        "param_name": "ScaleFactor",
        "display_name": "缩放比例",
        "type": "double",
        "default": 1.0,
        "range": [0.01, 10.0],
        "description": "缩放倍数"
      },
      {
        "param_name": "Interpolation",
        "display_name": "插值方法",
        "type": "enum",
        "default": "Linear",
        "range": ["Nearest", "Linear", "Cubic", "Area", "Lanczos4"],
        "description": "插值算法"
      },
      {
        "param_name": "UseScale",
        "display_name": "使用比例",
        "type": "bool",
        "default": false,
        "description": "是否使用比例而非绝对尺寸"
      }
    ],
    "notes": ""
  },
  {
    "operator_id": "ImageCrop",
    "name": "图像裁剪",
    "english_name": "Image Crop",
    "category": "预处理",
    "description": "ROI区域裁剪提取。",
    "keywords": ["图像裁剪", "ROI", "区域提取", "crop"],
    "inputs": [
      {
        "port_name": "Image",
        "display_name": "图像",
        "data_type": "Image",
        "required": true,
        "description": "输入图像"
      }
    ],
    "outputs": [
      {
        "port_name": "Image",
        "display_name": "图像",
        "data_type": "Image",
        "description": "裁剪后的图像"
      }
    ],
    "params": [
      {
        "param_name": "X",
        "display_name": "起始X",
        "type": "int",
        "default": 0,
        "range": [0, 99999],
        "description": "裁剪区域左上角X坐标"
      },
      {
        "param_name": "Y",
        "display_name": "起始Y",
        "type": "int",
        "default": 0,
        "range": [0, 99999],
        "description": "裁剪区域左上角Y坐标"
      },
      {
        "param_name": "Width",
        "display_name": "宽度",
        "type": "int",
        "default": 100,
        "range": [1, 99999],
        "description": "裁剪宽度"
      },
      {
        "param_name": "Height",
        "display_name": "高度",
        "type": "int",
        "default": 100,
        "range": [1, 99999],
        "description": "裁剪高度"
      }
    ],
    "notes": ""
  },
  {
    "operator_id": "ImageRotate",
    "name": "图像旋转",
    "english_name": "Image Rotate",
    "category": "预处理",
    "description": "任意角度旋转图像。",
    "keywords": ["图像旋转", "旋转", "角度", "rotate", "rotation"],
    "inputs": [
      {
        "port_name": "Image",
        "display_name": "图像",
        "data_type": "Image",
        "required": true,
        "description": "输入图像"
      }
    ],
    "outputs": [
      {
        "port_name": "Image",
        "display_name": "图像",
        "data_type": "Image",
        "description": "旋转后的图像"
      }
    ],
    "params": [
      {
        "param_name": "Angle",
        "display_name": "旋转角度",
        "type": "double",
        "default": 0.0,
        "range": [-360.0, 360.0],
        "description": "旋转角度(度)，正值为顺时针"
      },
      {
        "param_name": "CenterX",
        "display_name": "中心X",
        "type": "int",
        "default": -1,
        "description": "旋转中心X(-1表示图像中心)"
      },
      {
        "param_name": "CenterY",
        "display_name": "中心Y",
        "type": "int",
        "default": -1,
        "description": "旋转中心Y(-1表示图像中心)"
      },
      {
        "param_name": "Scale",
        "display_name": "缩放比例",
        "type": "double",
        "default": 1.0,
        "range": [0.1, 10.0],
        "description": "旋转后缩放比例"
      },
      {
        "param_name": "AutoResize",
        "display_name": "自动调整尺寸",
        "type": "bool",
        "default": true,
        "description": "是否自动调整画布大小以容纳完整图像"
      }
    ],
    "notes": ""
  },
  {
    "operator_id": "PerspectiveTransform",
    "name": "透视变换",
    "english_name": "Perspective Transform",
    "category": "预处理",
    "description": "四边形透视校正，用于纠正倾斜视角。",
    "keywords": ["透视变换", "透视校正", "四边形变换", "perspective", "transform"],
    "inputs": [
      {
        "port_name": "Image",
        "display_name": "图像",
        "data_type": "Image",
        "required": true,
        "description": "输入图像"
      }
    ],
    "outputs": [
      {
        "port_name": "Image",
        "display_name": "图像",
        "data_type": "Image",
        "description": "变换后的图像"
      }
    ],
    "params": [
      {
        "param_name": "SrcX1",
        "display_name": "源点1 X",
        "type": "double",
        "default": 0.0,
        "description": "源四边形第1点X"
      },
      {
        "param_name": "SrcY1",
        "display_name": "源点1 Y",
        "type": "double",
        "default": 0.0,
        "description": "源四边形第1点Y"
      },
      {
        "param_name": "SrcX2",
        "display_name": "源点2 X",
        "type": "double",
        "default": 100.0,
        "description": "源四边形第2点X"
      },
      {
        "param_name": "SrcY2",
        "display_name": "源点2 Y",
        "type": "double",
        "default": 0.0,
        "description": "源四边形第2点Y"
      },
      {
        "param_name": "SrcX3",
        "display_name": "源点3 X",
        "type": "double",
        "default": 100.0,
        "description": "源四边形第3点X"
      },
      {
        "param_name": "SrcY3",
        "display_name": "源点3 Y",
        "type": "double",
        "default": 100.0,
        "description": "源四边形第3点Y"
      },
      {
        "param_name": "SrcX4",
        "display_name": "源点4 X",
        "type": "double",
        "default": 0.0,
        "description": "源四边形第4点X"
      },
      {
        "param_name": "SrcY4",
        "display_name": "源点4 Y",
        "type": "double",
        "default": 100.0,
        "description": "源四边形第4点Y"
      },
      {
        "param_name": "DstX1",
        "display_name": "目标点1 X",
        "type": "double",
        "default": 0.0,
        "description": "目标四边形第1点X"
      },
      {
        "param_name": "DstY1",
        "display_name": "目标点1 Y",
        "type": "double",
        "default": 0.0,
        "description": "目标四边形第1点Y"
      },
      {
        "param_name": "DstX2",
        "display_name": "目标点2 X",
        "type": "double",
        "default": 640.0,
        "description": "目标四边形第2点X"
      },
      {
        "param_name": "DstY2",
        "display_name": "目标点2 Y",
        "type": "double",
        "default": 0.0,
        "description": "目标四边形第2点Y"
      },
      {
        "param_name": "DstX3",
        "display_name": "目标点3 X",
        "type": "double",
        "default": 640.0,
        "description": "目标四边形第3点X"
      },
      {
        "param_name": "DstY3",
        "display_name": "目标点3 Y",
        "type": "double",
        "default": 480.0,
        "description": "目标四边形第3点Y"
      },
      {
        "param_name": "DstX4",
        "display_name": "目标点4 X",
        "type": "double",
        "default": 0.0,
        "description": "目标四边形第4点X"
      },
      {
        "param_name": "DstY4",
        "display_name": "目标点4 Y",
        "type": "double",
        "default": 480.0,
        "description": "目标四边形第4点Y"
      }
    ],
    "notes": "需要定义源四边形和目标四边形的4个顶点坐标"
  },
  {
    "operator_id": "CircleMeasurement",
    "name": "圆测量",
    "english_name": "Circle Measurement",
    "category": "测量",
    "description": "霍夫圆检测与测量，检测图像中的圆形并测量半径。",
    "keywords": ["圆测量", "霍夫圆", "圆检测", "circle", "hough"],
    "inputs": [
      {
        "port_name": "Image",
        "display_name": "输入图像",
        "data_type": "Image",
        "required": true,
        "description": "输入图像"
      }
    ],
    "outputs": [
      {
        "port_name": "Image",
        "display_name": "结果图像",
        "data_type": "Image",
        "description": "标注检测结果的图像"
      },
      {
        "port_name": "Radius",
        "display_name": "半径",
        "data_type": "Float",
        "description": "检测到的圆半径"
      },
      {
        "port_name": "Center",
        "display_name": "圆心",
        "data_type": "Point",
        "description": "圆心坐标"
      },
      {
        "port_name": "CircleCount",
        "display_name": "圆数量",
        "data_type": "Integer",
        "description": "检测到的圆数量"
      }
    ],
    "params": [
      {
        "param_name": "Method",
        "display_name": "检测方法",
        "type": "enum",
        "default": "HoughCircle",
        "range": ["HoughCircle", "FitEllipse"],
        "description": "圆检测算法"
      },
      {
        "param_name": "MinRadius",
        "display_name": "最小半径",
        "type": "int",
        "default": 10,
        "range": [0, 9999],
        "description": "最小圆半径"
      },
      {
        "param_name": "MaxRadius",
        "display_name": "最大半径",
        "type": "int",
        "default": 200,
        "range": [0, 9999],
        "description": "最大圆半径"
      },
      {
        "param_name": "Dp",
        "display_name": "分辨率比",
        "type": "double",
        "default": 1.0,
        "range": [0.5, 4.0],
        "description": "累加器分辨率与图像分辨率的反比"
      },
      {
        "param_name": "MinDist",
        "display_name": "最小圆距",
        "type": "double",
        "default": 50.0,
        "range": [1.0, 9999],
        "description": "检测到的圆心之间的最小距离"
      },
      {
        "param_name": "Param1",
        "display_name": "Canny阈值",
        "type": "double",
        "default": 100.0,
        "range": [0.0, 255.0],
        "description": "Canny边缘检测的高阈值"
      },
      {
        "param_name": "Param2",
        "display_name": "累加器阈值",
        "type": "double",
        "default": 30.0,
        "range": [0.0, 255.0],
        "description": "累加器阈值(越小检测到的圆越多)"
      }
    ],
    "notes": ""
  },
  {
    "operator_id": "LineMeasurement",
    "name": "直线测量",
    "english_name": "Line Measurement",
    "category": "测量",
    "description": "霍夫直线检测与测量。",
    "keywords": ["直线测量", "霍夫直线", "线检测", "line", "hough"],
    "inputs": [
      {
        "port_name": "Image",
        "display_name": "输入图像",
        "data_type": "Image",
        "required": true,
        "description": "输入图像(边缘或二值)"
      }
    ],
    "outputs": [
      {
        "port_name": "Image",
        "display_name": "结果图像",
        "data_type": "Image",
        "description": "标注直线的图像"
      },
      {
        "port_name": "Angle",
        "display_name": "角度",
        "data_type": "Float",
        "description": "直线角度(度)"
      },
      {
        "port_name": "Length",
        "display_name": "长度",
        "data_type": "Float",
        "description": "线段长度"
      },
      {
        "port_name": "LineCount",
        "display_name": "直线数量",
        "data_type": "Integer",
        "description": "检测到的直线数量"
      }
    ],
    "params": [
      {
        "param_name": "Method",
        "display_name": "检测方法",
        "type": "enum",
        "default": "HoughLine",
        "range": ["HoughLine", "FitLine"],
        "description": "直线检测算法"
      },
      {
        "param_name": "Threshold",
        "display_name": "累加阈值",
        "type": "int",
        "default": 100,
        "range": [1, 999],
        "description": "霍夫累加器阈值"
      },
      {
        "param_name": "MinLength",
        "display_name": "最小长度",
        "type": "double",
        "default": 50.0,
        "range": [0.0, 9999],
        "description": "最小线段长度"
      },
      {
        "param_name": "MaxGap",
        "display_name": "最大间隙",
        "type": "double",
        "default": 10.0,
        "range": [0.0, 9999],
        "description": "线段间最大间隙"
      }
    ],
    "notes": ""
  }
]
```

---

*文档待续 - 由于算子数量较多(46个)，以上为部分算子示例。完整算子列表包含以下分类：*

**已包含算子(部分示例)**:
- 图像源: ImageAcquisition
- 预处理: Filtering, MedianBlur, BilateralFilter, ImageResize, ImageCrop, ImageRotate, PerspectiveTransform, Thresholding, ColorConversion, AdaptiveThreshold, HistogramEqualization, CLAHE, GaussianBlur, LaplacianSharpen
- 边缘检测: EdgeDetection(Canny), SubpixelEdgeDetection
- 形态学: Morphology, MorphologicalOperation
- Blob分析: BlobAnalysis
- 模板匹配: TemplateMatching, ShapeMatching
- 特征提取: ContourDetection, ContourDetection
- 测量: Measurement, CircleMeasurement, LineMeasurement, ContourMeasurement, AngleMeasurement, GeometricTolerance
- 几何拟合: GeometricFitting
- 标定: CameraCalibration, Undistort, CoordinateTransform
- 识别: CodeRecognition, DeepLearning
- 通信: ModbusCommunication, TcpCommunication, SerialCommunication, SiemensS7Communication, MitsubishiMcCommunication, OmronFinsCommunication, DatabaseWrite
- 流程控制: ConditionalBranch, TryCatch, VariableRead, VariableWrite, VariableIncrement, CycleCounter
- 图像运算: ImageAdd, ImageSubtract, ImageBlend
- 其他: RoiManager, ColorDetection, ResultOutput, ResultJudgment

**注**: 完整的46个算子定义请参考 `Acme.Product.Infrastructure/Services/OperatorFactory.cs` 中的 `InitializeDefaultOperators()` 方法。

## 第四章：工程/项目文件格式

### 4.1 序列化格式

**JSON格式** (camelCase命名规范)

序列化代码位置: `Acme.Product.Infrastructure/Services/ProjectJsonSerializer.cs`

```csharp
public class ProjectJsonSerializer : IProjectSerializer
{
    private readonly JsonSerializerOptions _options = new()
    {
        WriteIndented = true,        // 格式化缩进
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase  // 小驼峰命名
    };

    public Task<byte[]> SerializeAsync(ProjectDto project)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(project, _options);
        return Task.FromResult(bytes);
    }

    public Task<ProjectDto?> DeserializeAsync(byte[] data)
    {
        using var stream = new MemoryStream(data);
        var project = JsonSerializer.Deserialize<ProjectDto>(stream, _options);
        return Task.FromResult(project);
    }
}
```

存储位置: `Acme.Product.Infrastructure/Services/JsonFileProjectFlowStorage.cs`

```csharp
public class JsonFileProjectFlowStorage : IProjectFlowStorage
{
    private readonly string _basePath;

    public JsonFileProjectFlowStorage()
    {
        // 存储在 App_Data/ProjectFlows 目录下
        _basePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "App_Data", "ProjectFlows");
    }

    public async Task SaveFlowJsonAsync(Guid projectId, string flowJson)
    {
        var filePath = GetFilePath(projectId);
        await File.WriteAllTextAsync(filePath, flowJson);
    }

    public async Task<string?> LoadFlowJsonAsync(Guid projectId)
    {
        var filePath = GetFilePath(projectId);
        if (!File.Exists(filePath)) return null;
        return await File.ReadAllTextAsync(filePath);
    }

    private string GetFilePath(Guid projectId)
    {
        return Path.Combine(_basePath, $"{projectId}.json");
    }
}
```

### 4.2 工程文件结构Schema

```json
{
  "$schema": "ClearVision Project Schema",
  "description": "ClearVision工程文件完整结构定义",
  
  "id": {
    "type": "string",
    "format": "uuid",
    "description": "工程唯一标识符(GUID)"
  },
  
  "name": {
    "type": "string",
    "description": "工程名称"
  },
  
  "description": {
    "type": "string",
    "nullable": true,
    "description": "工程描述"
  },
  
  "version": {
    "type": "string",
    "default": "1.0.0",
    "description": "工程版本号"
  },
  
  "createdAt": {
    "type": "string",
    "format": "date-time",
    "description": "创建时间(ISO 8601格式)"
  },
  
  "modifiedAt": {
    "type": "string",
    "format": "date-time",
    "nullable": true,
    "description": "最后修改时间"
  },
  
  "lastOpenedAt": {
    "type": "string",
    "format": "date-time",
    "nullable": true,
    "description": "最后打开时间"
  },
  
  "globalSettings": {
    "type": "object",
    "description": "全局配置参数",
    "additionalProperties": {
      "type": "string"
    }
  },
  
  "flow": {
    "type": "object",
    "description": "算子流程定义",
    "properties": {
      "id": {
        "type": "string",
        "format": "uuid",
        "description": "流程ID"
      },
      "name": {
        "type": "string",
        "default": "默认流程",
        "description": "流程名称"
      },
      
      "operators": {
        "type": "array",
        "description": "算子节点列表",
        "items": {
          "type": "object",
          "properties": {
            "id": {
              "type": "string",
              "format": "uuid",
              "description": "算子唯一ID"
            },
            "name": {
              "type": "string",
              "description": "算子显示名称"
            },
            "type": {
              "type": "string",
              "enum": ["ImageAcquisition", "Filtering", "EdgeDetection", "Thresholding", "Morphology", "BlobAnalysis", "TemplateMatching", "Measurement", "CodeRecognition", "DeepLearning", "ResultOutput", "ContourDetection", "MedianBlur", "BilateralFilter", "ImageResize", "ImageCrop", "ImageRotate", "PerspectiveTransform", "CircleMeasurement", "LineMeasurement", "ContourMeasurement", "AngleMeasurement", "GeometricTolerance", "CameraCalibration", "Undistort", "CoordinateTransform", "ModbusCommunication", "TcpCommunication", "DatabaseWrite", "ConditionalBranch", "ColorConversion", "AdaptiveThreshold", "HistogramEqualization", "GeometricFitting", "RoiManager", "ShapeMatching", "SubpixelEdgeDetection", "ColorDetection", "SerialCommunication", "ResultJudgment", "SiemensS7Communication", "MitsubishiMcCommunication", "OmronFinsCommunication", "ModbusRtuCommunication", "ClaheEnhancement", "MorphologicalOperation", "GaussianBlur", "LaplacianSharpen", "OnnxInference", "ImageAdd", "ImageSubtract", "ImageBlend", "VariableRead", "VariableWrite", "VariableIncrement", "TryCatch", "CycleCounter", "AkazeFeatureMatch", "OrbFeatureMatch", "GradientShapeMatch", "PyramidShapeMatch", "DualModalVoting"],
              "description": "算子类型枚举"
            },
            "x": {
              "type": "number",
              "description": "画布X坐标"
            },
            "y": {
              "type": "number",
              "description": "画布Y坐标"
            },
            "isEnabled": {
              "type": "boolean",
              "default": true,
              "description": "是否启用"
            },
            "executionStatus": {
              "type": "integer",
              "enum": [0, 1, 2, 3, 4],
              "description": "执行状态: 0=未执行, 1=执行中, 2=成功, 3=失败, 4=跳过"
            },
            "executionTimeMs": {
              "type": "integer",
              "nullable": true,
              "description": "执行耗时(毫秒)"
            },
            "errorMessage": {
              "type": "string",
              "nullable": true,
              "description": "错误信息"
            },
            
            "inputPorts": {
              "type": "array",
              "description": "输入端口列表",
              "items": {
                "type": "object",
                "properties": {
                  "id": {
                    "type": "string",
                    "format": "uuid",
                    "description": "端口唯一ID"
                  },
                  "name": {
                    "type": "string",
                    "description": "端口名称"
                  },
                  "dataType": {
                    "type": "integer",
                    "enum": [0, 1, 2, 3, 4, 5, 6, 7, 99],
                    "description": "数据类型: 0=Image, 1=Integer, 2=Float, 3=Boolean, 4=String, 5=Point, 6=Rectangle, 7=Contour, 99=Any"
                  },
                  "direction": {
                    "type": "integer",
                    "enum": [0],
                    "description": "端口方向: 0=Input"
                  },
                  "isRequired": {
                    "type": "boolean",
                    "default": true,
                    "description": "是否必需"
                  }
                },
                "required": ["id", "name", "dataType", "direction"]
              }
            },
            
            "outputPorts": {
              "type": "array",
              "description": "输出端口列表",
              "items": {
                "type": "object",
                "properties": {
                  "id": {
                    "type": "string",
                    "format": "uuid",
                    "description": "端口唯一ID"
                  },
                  "name": {
                    "type": "string",
                    "description": "端口名称"
                  },
                  "dataType": {
                    "type": "integer",
                    "enum": [0, 1, 2, 3, 4, 5, 6, 7, 99],
                    "description": "数据类型"
                  },
                  "direction": {
                    "type": "integer",
                    "enum": [1],
                    "description": "端口方向: 1=Output"
                  },
                  "isRequired": {
                    "type": "boolean",
                    "default": false,
                    "description": "是否必需"
                  }
                },
                "required": ["id", "name", "dataType", "direction"]
              }
            },
            
            "parameters": {
              "type": "array",
              "description": "参数列表",
              "items": {
                "type": "object",
                "properties": {
                  "id": {
                    "type": "string",
                    "format": "uuid"
                  },
                  "name": {
                    "type": "string",
                    "description": "参数名"
                  },
                  "displayName": {
                    "type": "string",
                    "description": "显示名称"
                  },
                  "description": {
                    "type": "string",
                    "nullable": true,
                    "description": "参数描述"
                  },
                  "dataType": {
                    "type": "string",
                    "enum": ["int", "double", "float", "bool", "string", "enum", "file", "cameraBinding"],
                    "description": "参数数据类型"
                  },
                  "value": {
                    "description": "当前值(任意类型)"
                  },
                  "defaultValue": {
                    "description": "默认值(任意类型)"
                  },
                  "minValue": {
                    "description": "最小值(数值类型)"
                  },
                  "maxValue": {
                    "description": "最大值(数值类型)"
                  },
                  "isRequired": {
                    "type": "boolean",
                    "default": true
                  },
                  "options": {
                    "type": "array",
                    "nullable": true,
                    "description": "枚举选项列表",
                    "items": {
                      "type": "object",
                      "properties": {
                        "label": {
                          "type": "string",
                          "description": "显示标签"
                        },
                        "value": {
                          "type": "string",
                          "description": "实际值"
                        }
                      }
                    }
                  }
                },
                "required": ["id", "name", "displayName", "dataType"]
              }
            }
          },
          "required": ["id", "name", "type", "x", "y"]
        }
      },
      
      "connections": {
        "type": "array",
        "description": "算子连接列表",
        "items": {
          "type": "object",
          "properties": {
            "id": {
              "type": "string",
              "format": "uuid",
              "description": "连接唯一ID"
            },
            "sourceOperatorId": {
              "type": "string",
              "format": "uuid",
              "description": "源算子ID"
            },
            "sourcePortId": {
              "type": "string",
              "format": "uuid",
              "description": "源端口ID"
            },
            "targetOperatorId": {
              "type": "string",
              "format": "uuid",
              "description": "目标算子ID"
            },
            "targetPortId": {
              "type": "string",
              "format": "uuid",
              "description": "目标端口ID"
            }
          },
          "required": ["id", "sourceOperatorId", "sourcePortId", "targetOperatorId", "targetPortId"]
        }
      }
    },
    "required": ["id", "name", "operators", "connections"]
  }
}
```

### 4.3 示例工程文件

#### 示例1：简单线性流程（图像采集→边缘检测→结果输出）

```json
{
  "id": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "name": "简单边缘检测",
  "description": "基础图像边缘检测示例",
  "version": "1.0.0",
  "createdAt": "2026-02-19T10:00:00Z",
  "modifiedAt": "2026-02-19T10:30:00Z",
  "globalSettings": {},
  "flow": {
    "id": "f1a2b3c4-d5e6-7890-abcd-ef1234567891",
    "name": "默认流程",
    "operators": [
      {
        "id": "op-001",
        "name": "图像采集",
        "type": "ImageAcquisition",
        "x": 100,
        "y": 150,
        "isEnabled": true,
        "inputPorts": [],
        "outputPorts": [
          {
            "id": "port-001-out",
            "name": "Image",
            "dataType": 0,
            "direction": 1,
            "isRequired": false
          }
        ],
        "parameters": [
          {
            "id": "param-001",
            "name": "sourceType",
            "displayName": "采集源",
            "dataType": "enum",
            "value": "file",
            "defaultValue": "file",
            "options": [
              { "label": "文件", "value": "file" },
              { "label": "相机", "value": "camera" }
            ]
          },
          {
            "id": "param-002",
            "name": "filePath",
            "displayName": "文件路径",
            "dataType": "file",
            "value": "C:/Images/sample.jpg",
            "defaultValue": ""
          }
        ]
      },
      {
        "id": "op-002",
        "name": "边缘检测",
        "type": "EdgeDetection",
        "x": 350,
        "y": 150,
        "isEnabled": true,
        "inputPorts": [
          {
            "id": "port-002-in",
            "name": "Image",
            "dataType": 0,
            "direction": 0,
            "isRequired": true
          }
        ],
        "outputPorts": [
          {
            "id": "port-002-out",
            "name": "Image",
            "dataType": 0,
            "direction": 1,
            "isRequired": false
          }
        ],
        "parameters": [
          {
            "id": "param-003",
            "name": "Threshold1",
            "displayName": "低阈值",
            "dataType": "double",
            "value": 50.0,
            "defaultValue": 50.0,
            "minValue": 0.0,
            "maxValue": 255.0
          },
          {
            "id": "param-004",
            "name": "Threshold2",
            "displayName": "高阈值",
            "dataType": "double",
            "value": 150.0,
            "defaultValue": 150.0,
            "minValue": 0.0,
            "maxValue": 255.0
          }
        ]
      },
      {
        "id": "op-003",
        "name": "结果输出",
        "type": "ResultOutput",
        "x": 600,
        "y": 150,
        "isEnabled": true,
        "inputPorts": [
          {
            "id": "port-003-in",
            "name": "Image",
            "dataType": 0,
            "direction": 0,
            "isRequired": false
          }
        ],
        "outputPorts": [],
        "parameters": [
          {
            "id": "param-005",
            "name": "Format",
            "displayName": "输出格式",
            "dataType": "enum",
            "value": "JSON",
            "defaultValue": "JSON",
            "options": [
              { "label": "JSON", "value": "JSON" },
              { "label": "CSV", "value": "CSV" }
            ]
          }
        ]
      }
    ],
    "connections": [
      {
        "id": "conn-001",
        "sourceOperatorId": "op-001",
        "sourcePortId": "port-001-out",
        "targetOperatorId": "op-002",
        "targetPortId": "port-002-in"
      },
      {
        "id": "conn-002",
        "sourceOperatorId": "op-002",
        "sourcePortId": "port-002-out",
        "targetOperatorId": "op-003",
        "targetPortId": "port-003-in"
      }
    ]
  }
}
```

#### 示例2：中等复杂度（带分支的缺陷检测流程）

```json
{
  "id": "b2c3d4e5-f6a7-8901-bcde-f23456789012",
  "name": "缺陷检测与分类",
  "description": "使用深度学习和传统算法进行缺陷检测",
  "version": "1.1.0",
  "createdAt": "2026-02-19T11:00:00Z",
  "globalSettings": {
    "InspectionMode": "HighAccuracy",
    "SaveDebugImages": "true"
  },
  "flow": {
    "id": "flow-002",
    "name": "缺陷检测流程",
    "operators": [
      {
        "id": "acq-001",
        "name": "图像采集",
        "type": "ImageAcquisition",
        "x": 50,
        "y": 200,
        "inputPorts": [],
        "outputPorts": [{ "id": "acq-out", "name": "Image", "dataType": 0, "direction": 1 }],
        "parameters": [
          { "id": "p1", "name": "sourceType", "displayName": "采集源", "dataType": "enum", "value": "camera" }
        ]
      },
      {
        "id": "pre-001",
        "name": "预处理-滤波",
        "type": "MedianBlur",
        "x": 250,
        "y": 200,
        "inputPorts": [{ "id": "pre-in", "name": "Image", "dataType": 0, "direction": 0, "isRequired": true }],
        "outputPorts": [{ "id": "pre-out", "name": "Image", "dataType": 0, "direction": 1 }],
        "parameters": [{ "id": "p2", "name": "KernelSize", "displayName": "核大小", "dataType": "int", "value": 5 }]
      },
      {
        "id": "ai-001",
        "name": "AI深度学习检测",
        "type": "DeepLearning",
        "x": 450,
        "y": 100,
        "inputPorts": [{ "id": "ai-in", "name": "Image", "dataType": 0, "direction": 0, "isRequired": true }],
        "outputPorts": [
          { "id": "ai-out-img", "name": "Image", "dataType": 0, "direction": 1 },
          { "id": "ai-out-count", "name": "DefectCount", "dataType": 1, "direction": 1 }
        ],
        "parameters": [
          { "id": "p3", "name": "ModelPath", "displayName": "模型路径", "dataType": "file", "value": "models/defect.onnx" },
          { "id": "p4", "name": "Confidence", "displayName": "置信度", "dataType": "double", "value": 0.7 }
        ]
      },
      {
        "id": "trad-001",
        "name": "传统Blob分析",
        "type": "BlobAnalysis",
        "x": 450,
        "y": 300,
        "inputPorts": [{ "id": "trad-in", "name": "Image", "dataType": 0, "direction": 0, "isRequired": true }],
        "outputPorts": [
          { "id": "trad-out-img", "name": "Image", "dataType": 0, "direction": 1 },
          { "id": "trad-out-count", "name": "BlobCount", "dataType": 1, "direction": 1 }
        ],
        "parameters": [
          { "id": "p5", "name": "MinArea", "displayName": "最小面积", "dataType": "int", "value": 50 },
          { "id": "p6", "name": "MaxArea", "displayName": "最大面积", "dataType": "int", "value": 5000 }
        ]
      },
      {
        "id": "vote-001",
        "name": "双模态投票",
        "type": "DualModalVoting",
        "x": 700,
        "y": 200,
        "inputPorts": [
          { "id": "vote-in-1", "name": "AIResult", "dataType": 1, "direction": 0 },
          { "id": "vote-in-2", "name": "TradResult", "dataType": 1, "direction": 0 }
        ],
        "outputPorts": [
          { "id": "vote-out", "name": "FinalResult", "dataType": 3, "direction": 1 }
        ],
        "parameters": []
      },
      {
        "id": "branch-001",
        "name": "合格判定",
        "type": "ConditionalBranch",
        "x": 900,
        "y": 200,
        "inputPorts": [{ "id": "branch-in", "name": "Value", "dataType": 3, "direction": 0 }],
        "outputPorts": [
          { "id": "branch-true", "name": "True", "dataType": 3, "direction": 1 },
          { "id": "branch-false", "name": "False", "dataType": 3, "direction": 1 }
        ],
        "parameters": [
          { "id": "p7", "name": "Condition", "displayName": "条件", "dataType": "enum", "value": "Equal" },
          { "id": "p8", "name": "CompareValue", "displayName": "比较值", "dataType": "string", "value": "true" }
        ]
      },
      {
        "id": "out-ok",
        "name": "OK输出",
        "type": "ResultOutput",
        "x": 1100,
        "y": 150,
        "inputPorts": [{ "id": "ok-in", "name": "Result", "dataType": 99, "direction": 0 }],
        "outputPorts": [],
        "parameters": [{ "id": "p9", "name": "Format", "displayName": "格式", "dataType": "enum", "value": "JSON" }]
      },
      {
        "id": "out-ng",
        "name": "NG输出",
        "type": "ResultOutput",
        "x": 1100,
        "y": 250,
        "inputPorts": [{ "id": "ng-in", "name": "Result", "dataType": 99, "direction": 0 }],
        "outputPorts": [],
        "parameters": [{ "id": "p10", "name": "Format", "displayName": "格式", "dataType": "enum", "value": "JSON" }]
      }
    ],
    "connections": [
      { "id": "c1", "sourceOperatorId": "acq-001", "sourcePortId": "acq-out", "targetOperatorId": "pre-001", "targetPortId": "pre-in" },
      { "id": "c2", "sourceOperatorId": "pre-001", "sourcePortId": "pre-out", "targetOperatorId": "ai-001", "targetPortId": "ai-in" },
      { "id": "c3", "sourceOperatorId": "pre-001", "sourcePortId": "pre-out", "targetOperatorId": "trad-001", "targetPortId": "trad-in" },
      { "id": "c4", "sourceOperatorId": "ai-001", "sourcePortId": "ai-out-count", "targetOperatorId": "vote-001", "targetPortId": "vote-in-1" },
      { "id": "c5", "sourceOperatorId": "trad-001", "sourcePortId": "trad-out-count", "targetOperatorId": "vote-001", "targetPortId": "vote-in-2" },
      { "id": "c6", "sourceOperatorId": "vote-001", "sourcePortId": "vote-out", "targetOperatorId": "branch-001", "targetPortId": "branch-in" },
      { "id": "c7", "sourceOperatorId": "branch-001", "sourcePortId": "branch-true", "targetOperatorId": "out-ok", "targetPortId": "ok-in" },
      { "id": "c8", "sourceOperatorId": "branch-001", "sourcePortId": "branch-false", "targetOperatorId": "out-ng", "targetPortId": "ng-in" }
    ]
  }
}
```

#### 示例3：复杂流程（带循环的多点测量）

```json
{
  "id": "c3d4e5f6-a7b8-9012-cdef-345678901234",
  "name": "多点精密测量",
  "description": "带循环和条件分支的复杂测量流程",
  "version": "2.0.0",
  "createdAt": "2026-02-19T12:00:00Z",
  "flow": {
    "id": "flow-003",
    "name": "精密测量流程",
    "operators": [
      {
        "id": "init-001",
        "name": "初始化计数器",
        "type": "VariableWrite",
        "x": 50,
        "y": 100,
        "inputPorts": [],
        "outputPorts": [{ "id": "init-out", "name": "Value", "dataType": 1, "direction": 1 }],
        "parameters": [
          { "id": "p1", "name": "VariableName", "displayName": "变量名", "dataType": "string", "value": "measureCount" },
          { "id": "p2", "name": "Value", "displayName": "初始值", "dataType": "int", "value": 0 }
        ]
      },
      {
        "id": "acq-002",
        "name": "图像采集",
        "type": "ImageAcquisition",
        "x": 50,
        "y": 200,
        "inputPorts": [],
        "outputPorts": [{ "id": "acq2-out", "name": "Image", "dataType": 0, "direction": 1 }]
      },
      {
        "id": "calib-001",
        "name": "畸变校正",
        "type": "Undistort",
        "x": 200,
        "y": 200,
        "inputPorts": [{ "id": "calib-in", "name": "Image", "dataType": 0, "direction": 0 }],
        "outputPorts": [{ "id": "calib-out", "name": "Image", "dataType": 0, "direction": 1 }],
        "parameters": [{ "id": "p3", "name": "CalibrationFile", "displayName": "标定文件", "dataType": "file", "value": "calib.json" }]
      },
      {
        "id": "edge-001",
        "name": "边缘检测",
        "type": "EdgeDetection",
        "x": 350,
        "y": 200,
        "inputPorts": [{ "id": "edge-in", "name": "Image", "dataType": 0, "direction": 0 }],
        "outputPorts": [{ "id": "edge-out", "name": "Edges", "dataType": 0, "direction": 1 }]
      },
      {
        "id": "subpx-001",
        "name": "亚像素边缘",
        "type": "SubpixelEdgeDetection",
        "x": 500,
        "y": 200,
        "inputPorts": [{ "id": "subpx-in", "name": "Image", "dataType": 0, "direction": 0 }],
        "outputPorts": [{ "id": "subpx-out", "name": "Points", "dataType": 5, "direction": 1 }]
      },
      {
        "id": "measure-001",
        "name": "距离测量",
        "type": "Measurement",
        "x": 650,
        "y": 200,
        "inputPorts": [
          { "id": "meas-in-img", "name": "Image", "dataType": 0, "direction": 0 },
          { "id": "meas-in-pt", "name": "Points", "dataType": 5, "direction": 0 }
        ],
        "outputPorts": [{ "id": "meas-out", "name": "Distance", "dataType": 2, "direction": 1 }]
      },
      {
        "id": "inc-001",
        "name": "计数器+1",
        "type": "VariableIncrement",
        "x": 800,
        "y": 200,
        "inputPorts": [{ "id": "inc-in", "name": "Value", "dataType": 1, "direction": 0 }],
        "outputPorts": [{ "id": "inc-out", "name": "Value", "dataType": 1, "direction": 1 }],
        "parameters": [{ "id": "p4", "name": "VariableName", "displayName": "变量名", "dataType": "string", "value": "measureCount" }]
      },
      {
        "id": "check-001",
        "name": "检查是否完成",
        "type": "ConditionalBranch",
        "x": 950,
        "y": 200,
        "inputPorts": [{ "id": "check-in", "name": "Value", "dataType": 1, "direction": 0 }],
        "outputPorts": [
          { "id": "check-true", "name": "True", "dataType": 3, "direction": 1 },
          { "id": "check-false", "name": "False", "dataType": 3, "direction": 1 }
        ],
        "parameters": [
          { "id": "p5", "name": "Condition", "displayName": "条件", "dataType": "enum", "value": "GreaterThan" },
          { "id": "p6", "name": "CompareValue", "displayName": "比较值", "dataType": "string", "value": "4" }
        ]
      },
      {
        "id": "comm-001",
        "name": "Modbus发送",
        "type": "ModbusCommunication",
        "x": 1100,
        "y": 150,
        "inputPorts": [{ "id": "comm-in", "name": "Data", "dataType": 99, "direction": 0 }],
        "outputPorts": [{ "id": "comm-out", "name": "Status", "dataType": 3, "direction": 1 }],
        "parameters": [
          { "id": "p7", "name": "IpAddress", "displayName": "IP地址", "dataType": "string", "value": "192.168.1.100" },
          { "id": "p8", "name": "Port", "displayName": "端口", "dataType": "int", "value": 502 }
        ]
      },
      {
        "id": "db-001",
        "name": "数据库记录",
        "type": "DatabaseWrite",
        "x": 1100,
        "y": 250,
        "inputPorts": [{ "id": "db-in", "name": "Data", "dataType": 99, "direction": 0 }],
        "outputPorts": [{ "id": "db-out", "name": "Status", "dataType": 3, "direction": 1 }]
      }
    ],
    "connections": [
      { "id": "c1", "sourceOperatorId": "acq-002", "sourcePortId": "acq2-out", "targetOperatorId": "calib-001", "targetPortId": "calib-in" },
      { "id": "c2", "sourceOperatorId": "calib-001", "sourcePortId": "calib-out", "targetOperatorId": "edge-001", "targetPortId": "edge-in" },
      { "id": "c3", "sourceOperatorId": "edge-001", "sourcePortId": "edge-out", "targetOperatorId": "subpx-001", "targetPortId": "subpx-in" },
      { "id": "c4", "sourceOperatorId": "subpx-001", "sourcePortId": "subpx-out", "targetOperatorId": "measure-001", "targetPortId": "meas-in-pt" },
      { "id": "c5", "sourceOperatorId": "calib-001", "sourcePortId": "calib-out", "targetOperatorId": "measure-001", "targetPortId": "meas-in-img" },
      { "id": "c6", "sourceOperatorId": "measure-001", "sourcePortId": "meas-out", "targetOperatorId": "inc-001", "targetPortId": "inc-in" },
      { "id": "c7", "sourceOperatorId": "inc-001", "sourcePortId": "inc-out", "targetOperatorId": "check-001", "targetPortId": "check-in" },
      { "id": "c8", "sourceOperatorId": "check-001", "sourcePortId": "check-true", "targetOperatorId": "comm-001", "targetPortId": "comm-in" },
      { "id": "c9", "sourceOperatorId": "check-001", "sourcePortId": "check-false", "targetOperatorId": "db-001", "targetPortId": "db-in" }
    ]
  }
}
```

### 4.4 加载入口

**从JSON字符串加载工程的入口函数**:

```csharp
// 文件: Acme.Product.Infrastructure/Services/ProjectJsonSerializer.cs
public Task<ProjectDto?> DeserializeAsync(byte[] data)
{
    using var stream = new MemoryStream(data);
    var project = JsonSerializer.Deserialize<ProjectDto>(stream, _options);
    return Task.FromResult(project);
}
```

**DTO到实体的转换入口** (`OperatorFlowDto.cs` 第38-100行):

```csharp
public OperatorFlow ToEntity()
{
    // 创建空流程
    var flow = new OperatorFlow(Name);

    // 添加算子
    foreach (var op in Operators)
    {
        var operatorEntity = new Operator(op.Id, op.Name, op.Type, op.X, op.Y);
        
        // 加载输入端口
        foreach (var port in op.InputPorts)
        {
            operatorEntity.LoadInputPort(port.Id, port.Name, port.DataType, port.IsRequired);
        }
        
        // 加载输出端口
        foreach (var port in op.OutputPorts)
        {
            operatorEntity.LoadOutputPort(port.Id, port.Name, port.DataType);
        }
        
        // 添加参数
        foreach (var p in op.Parameters)
        {
            var param = new Parameter(
                p.Id, p.Name, p.DisplayName, p.Description ?? "", 
                p.DataType, p.DefaultValue, p.MinValue, p.MaxValue, 
                p.IsRequired, p.Options
            );
            if (p.Value != null)
            {
                param.SetValue(p.Value);
            }
            operatorEntity.AddParameter(param);
        }
        
        flow.AddOperator(operatorEntity);
    }

    // 添加连接
    foreach (var c in Connections)
    {
        var connection = new OperatorConnection(
            c.SourceOperatorId, c.SourcePortId, 
            c.TargetOperatorId, c.TargetPortId
        );
        flow.AddConnection(connection);
    }

    return flow;
}
```

**完整调用链路**:
```
1. 读取JSON文件 -> byte[]
2. ProjectJsonSerializer.DeserializeAsync(byte[]) -> ProjectDto
3. ProjectDto.Flow.ToEntity() -> OperatorFlow
4. FlowExecutionService.ExecuteFlowAsync(OperatorFlow) -> 执行流程
```

**前端画布加载入口** (`flowCanvas.js` 第1170-1277行):
```javascript
deserialize(data) {
    if (!data) return;
    this.clear();
    
    // 支持多种嵌套结构
    const flowData = data.project?.flow || data.flow || data;
    
    // 处理列表属性
    const operators = flowData.operators || flowData.Operators || flowData.nodes || [];
    const connections = flowData.connections || flowData.Connections || [];
    
    // 重建节点和连接...
    operators.forEach(op => {
        const node = {
            id: op.id || op.Id,
            type: op.type || op.Type,
            x: op.x || op.X || 0,
            y: op.y || op.Y || 0,
            title: op.name || op.Name || op.title || type,
            inputs: (op.inputPorts || op.InputPorts || op.inputs || []).map(normalizePort),
            outputs: (op.outputPorts || op.OutputPorts || op.outputs || []).map(normalizePort),
            // ...
        };
        this.nodes.set(node.id, node);
    });
    
    // 重建连接...
    connections.forEach(conn => {
        // 适配后端DTO (PascalCase) 或前端 (camelCase)
        // ...
    });
    
    this.render();
}
```

---

## 第五章：典型使用场景与工作流模式

### 场景1：基础缺陷检测

**自然语言描述**: "我要检测产品表面是否有划痕或污渍缺陷"

**所需算子序列**:
```
图像采集 → 高斯滤波 → 边缘检测 → Blob分析 → 条件分支 → 结果输出
```

**连线关系**:
- 图像采集.Image → 高斯滤波.Image
- 高斯滤波.Image → 边缘检测.Image
- 边缘检测.Edges → Blob分析.Image
- Blob分析.BlobCount → 条件分支.Value
- 条件分支.True → 结果输出(Result=OK)
- 条件分支.False → 结果输出(Result=NG)

**关键参数配置**:
- 高斯滤波: KernelSize=5, SigmaX=1.0
- 边缘检测: Threshold1=50, Threshold2=150
- Blob分析: MinArea=100, MaxArea=10000
- 条件分支: Condition=GreaterThan, CompareValue=0

---

### 场景2：尺寸精密测量

**自然语言描述**: "我需要测量工件上两个孔之间的距离，精度要求0.01mm"

**所需算子序列**:
```
图像采集 → 畸变校正 → 圆测量 → 圆测量 → 测量(距离) → 结果输出
```

**连线关系**:
- 图像采集.Image → 畸变校正.Image
- 畸变校正.Image → 圆测量#1.Image
- 畸变校正.Image → 圆测量#2.Image
- 圆测量#1.Center → 测量.Point1
- 圆测量#2.Center → 测量.Point2
- 测量.Distance → 结果输出.Result

**关键参数配置**:
- 畸变校正: CalibrationFile="calibration_result.json"
- 圆测量#1: MinRadius=20, MaxRadius=100
- 圆测量#2: MinRadius=20, MaxRadius=100
- 测量: MeasureType=PointToPoint

---

### 场景3：条码识别与数据上传

**自然语言描述**: "扫描产品上的二维码，识别内容后通过Modbus发送给PLC"

**所需算子序列**:
```
图像采集 → 条码识别 → Modbus通信
```

**连线关系**:
- 图像采集.Image → 条码识别.Image
- 条码识别.Text → Modbus通信.Data

**关键参数配置**:
- 图像采集: sourceType="camera", triggerMode="Hardware"
- 条码识别: CodeType="QR", MaxResults=1
- Modbus通信: Protocol="TCP", IpAddress="192.168.1.10", Port=502, FunctionCode="WriteMultiple"

---

### 场景4：AI+传统算法双模态检测

**自然语言描述**: "用深度学习检测缺陷，同时用传统算法验证，只有两者都判断OK才算合格"

**所需算子序列**:
```
图像采集 → 预处理 → 深度学习检测
                      ↓
              传统Blob分析
                      ↓
              双模态投票 → 结果输出
```

**连线关系**:
- 图像采集.Image → 预处理.Image
- 预处理.Image → 深度学习检测.Image
- 预处理.Image → 传统Blob分析.Image
- 深度学习检测.DefectCount → 双模态投票.AIResult
- 传统Blob分析.BlobCount → 双模态投票.TradResult
- 双模态投票.FinalResult → 结果输出.Result

**关键参数配置**:
- 预处理: MedianBlur, KernelSize=5
- 深度学习: ModelPath="defect_model.onnx", Confidence=0.6
- 传统Blob: MinArea=50, MaxArea=5000

---

### 场景5：多点循环测量

**自然语言描述**: "在一张大图上测量5个不同位置的特征尺寸"

**所需算子序列**:
```
图像采集 → ROI裁剪#1 → 测量#1
       ↓
      ROI裁剪#2 → 测量#2
       ↓
      ROI裁剪#3 → 测量#3
       ↓
      ROI裁剪#4 → 测量#4
       ↓
      ROI裁剪#5 → 测量#5
       ↓
      结果汇总 → 结果输出
```

**关键参数配置**:
- 每个ROI裁剪设置不同的X,Y坐标
- 测量算子根据需求选择类型(距离/圆/直线)

---

### 场景6：带条件分支的柔性检测

**自然语言描述**: "根据产品型号选择不同的检测流程，大产品测尺寸，小产品测缺陷"

**所需算子序列**:
```
图像采集 → 条码识别(获取型号) → 条件分支
                                    ↓
                          型号=Large? ──Yes──→ 尺寸测量流程
                                    ↓
                                   No
                                    ↓
                            缺陷检测流程
```

**关键参数配置**:
- 条件分支: Condition=Equal, FieldName="识别内容"
- CompareValue根据实际型号设置

---

### 场景7：数据记录与追溯

**自然语言描述**: "每次检测结果都要保存到数据库，同时生成CSV报表"

**所需算子序列**:
```
图像采集 → [检测流程] → 结果输出(JSON)
                          ↓
                    数据库写入
                          ↓
                    CSV导出
```

**连线关系**:
- 检测流程的输出 → 结果输出.Result
- 检测流程的输出 → 数据库写入.Data
- 检测流程的输出 → CSV导出.Data

**关键参数配置**:
- 数据库写入: ConnectionString="Server=...", TableName="InspectionResults"
- CSV导出: FilePath="D:/Reports/result.csv"

---

### 场景8：相机标定与坐标转换

**自然语言描述**: "新相机需要进行标定，然后用标定结果进行物理坐标测量"

**所需算子序列(标定流程)**:
```
图像采集(多张标定板图像) → 相机标定 → 保存标定文件
```

**所需算子序列(测量流程)**:
```
图像采集 → 畸变校正(使用标定文件) → 圆测量 → 坐标转换(像素→物理) → 结果输出
```

**关键参数配置**:
- 相机标定: PatternType="Chessboard", BoardWidth=9, BoardHeight=6
- 坐标转换: CalibrationFile="calibration_result.json", PixelSize=0.01

---

## 第六章：API/接口层

### 6.1 是否有对外API

**当前状态**: 未找到 REST API / gRPC / WebSocket 接口

项目为纯桌面应用，通过WebView2进行前后端通信，而非对外暴露HTTP接口。

### 6.2 SDK/脚本接口

**当前状态**: 未找到对外SDK或脚本接口

但可以通过以下内部方法实现编程式控制:

### 6.3 关键内部接口

**算子创建** (`IOperatorFactory.cs`):
```csharp
Operator CreateOperator(OperatorType type, string name, double x, double y);
```

**流程执行** (`IFlowExecutionService.cs`):
```csharp
Task<FlowExecutionResult> ExecuteFlowAsync(
    OperatorFlow flow,
    Dictionary<string, object>? inputData = null,
    bool enableParallel = false,
    CancellationToken cancellationToken = default);
```

**工程序列化** (`IProjectSerializer.cs`):
```csharp
Task<byte[]> SerializeAsync(ProjectDto project);
Task<ProjectDto?> DeserializeAsync(byte[] data);
```

### 6.4 前端画布API

**创建节点** (`flowCanvas.js`):
```javascript
addNode(type, x, y, config = {}) => node
```

**创建连接**:
```javascript
addConnection(sourceId, sourcePort, targetId, targetPort) => connection
```

**删除节点**:
```javascript
removeNode(nodeId)
```

**删除连接**:
```javascript
removeConnection(connectionId)
```

**序列化/反序列化**:
```javascript
serialize() => { operators: [], connections: [] }
deserialize(data)
```

---

## 第七章：其他重要信息

### 7.1 模板/预设工程

**当前状态**: 未找到显式的模板系统

但在 `OperatorFactory.cs` 中，算子元数据包含了默认参数值，可以视为一种"参数模板"。

### 7.2 算子分组/子流程

**当前状态**: 未找到子流程或复合算子功能

所有算子都是扁平结构，暂不支持将多个算子封装为一个复合算子。

### 7.3 全局变量/上下文

**支持** - 通过 `IVariableContext` 接口

```csharp
public interface IVariableContext
{
    void SetVariable(string name, object value);
    object? GetVariable(string name);
    T? GetVariable<T>(string name);
    bool HasVariable(string name);
    void IncrementCycleCount();
    long CycleCount { get; }
}
```

相关算子:
- `VariableRead` - 读取全局变量
- `VariableWrite` - 写入全局变量
- `VariableIncrement` - 变量自增
- `CycleCounter` - 获取当前循环次数

### 7.4 控制流算子

**支持**:

| 算子 | 功能 |
|------|------|
| `ConditionalBranch` | 条件分支(True/False路径) |
| `TryCatch` | 异常捕获 |
| `CycleCounter` | 循环计数器 |

**注意**: 显式循环结构(如While/For)未找到，但可以通过条件分支+变量实现循环。

### 7.5 撤销/重做机制

**前端支持** - `flowCanvas.js` 中与 `store.js` 配合

实现方式: 通过状态快照保存操作历史

### 7.6 自动布局算法

**当前状态**: 未找到自动布局功能

算子位置完全由用户手动拖拽确定。

### 7.7 调试功能

**支持** - `FlowExecutionService.cs` 中的调试模式

```csharp
Task<FlowDebugExecutionResult> ExecuteFlowDebugAsync(
    OperatorFlow flow,
    DebugOptions options,  // 包含断点列表
    Dictionary<string, object>? inputData = null,
    CancellationToken cancellationToken = default);
```

支持:
- 断点设置
- 单步执行
- 中间结果缓存

### 7.8 重要文件路径汇总

| 用途 | 文件路径 |
|------|----------|
| 算子类型枚举 | `Acme.Product.Core/Enums/OperatorEnums.cs` |
| 算子元数据注册 | `Acme.Product.Infrastructure/Services/OperatorFactory.cs` |
| 算子执行器基类 | `Acme.Product.Infrastructure/Operators/OperatorBase.cs` |
| 流程执行服务 | `Acme.Product.Infrastructure/Services/FlowExecutionService.cs` |
| 工程序列化 | `Acme.Product.Infrastructure/Services/ProjectJsonSerializer.cs` |
| 前端画布 | `Acme.Product.Desktop/wwwroot/src/core/canvas/flowCanvas.js` |
| 工程DTO | `Acme.Product.Application/DTOs/ProjectDto.cs` |
| 流程DTO | `Acme.Product.Application/DTOs/OperatorFlowDto.cs` |
| 端口/参数值对象 | `Acme.Product.Core/ValueObjects/VisionValueObjects.cs` |

### 7.9 AI生成工作流的建议

基于以上分析，为AI自动生成工作流提供以下建议:

1. **数据类型一致性**: 确保连线时类型匹配，使用`checkTypeCompatibility`逻辑
2. **拓扑合法性**: 避免环路，使用`WouldCreateCycle`检测
3. **输入端口唯一性**: 一个输入端口只能有一个连接
4. **必要参数填充**: 所有`IsRequired=true`的参数必须设置值
5. **流程完整性**: 建议包含至少一个图像采集算子和一个结果输出算子
6. **坐标布局**: 算子位置建议从左到右排列，X坐标递增100-150像素

---

**文档生成完成**

> 本文档基于ClearVision项目代码分析生成，涵盖了技术栈、数据类型、算子定义、工程格式、使用场景等全部关键信息，可用于AI自动生成工作流的训练和推理。

