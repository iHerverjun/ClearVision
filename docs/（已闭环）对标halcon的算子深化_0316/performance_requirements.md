# 性能要求与预算规范

> **文档编号**: performance-requirements
> **版本**: V1.0
> **创建日期**: 2026-03-16
> **目标**: 确保所有算子满足工业实时性要求

---

## 一、总体性能目标

### 1.1 实时性要求

工业视觉系统的典型周期时间要求：
- **高速检测**：<100ms（10 FPS）
- **标准检测**：<500ms（2 FPS）
- **精密测量**：<1000ms（1 FPS）

### 1.2 资源限制

- **内存**：单个流程总内存<2GB
- **CPU**：单核利用率<80%（留20%给系统）
- **GPU**：深度学习推理时GPU利用率<90%

---

## 二、算子类别性能预算

### 2.1 预处理算子（Preprocessing）

| 算子 | 目标延迟 | 内存限制 | 测试条件 |
|------|---------|---------|---------|
| 高斯滤波 | <5ms | 50MB | 512x512, kernel=5x5 |
| 中值滤波 | <8ms | 50MB | 512x512, kernel=5x5 |
| 双边滤波 | <15ms | 50MB | 512x512, d=9 |
| 形态学操作 | <10ms | 50MB | 512x512, kernel=5x5 |
| 直方图均衡化 | <5ms | 50MB | 512x512 |
| CLAHE增强 | <10ms | 50MB | 512x512, clipLimit=2.0 |
| 图像缩放 | <3ms | 50MB | 2048x2048 → 512x512 |
| 图像旋转 | <5ms | 50MB | 512x512, 任意角度 |
| 仿射变换 | <5ms | 50MB | 512x512 |
| 透视变换 | <8ms | 50MB | 512x512 |
| 镜头畸变校正 | <10ms | 100MB | 512x512 |

**性能优化建议**：
- 使用OpenCV内置函数（已高度优化）
- 避免逐像素循环
- 复用Mat对象，减少内存分配

### 2.2 测量算子（Measurement）

| 算子 | 目标延迟 | 内存限制 | 测试条件 |
|------|---------|---------|---------|
| 卡尺工具 | <50ms | 50MB | 10个卡尺，512x512图 |
| 亚像素边缘检测 | <1ms | 1MB | 1xN灰度线，N=100 |
| 圆测量 | <30ms | 50MB | 512x512图，拟合1个圆 |
| 直线测量 | <20ms | 50MB | 512x512图，拟合1条直线 |
| 轮廓测量 | <40ms | 50MB | 512x512图，1个轮廓 |
| 角度测量 | <10ms | 10MB | 3个点或2条线 |
| 间隙测量 | <30ms | 50MB | 2个边缘 |
| 宽度测量 | <30ms | 50MB | 边缘对测量 |
| 几何拟合 | <50ms | 50MB | 100个点拟合 |
| 几何公差 | <20ms | 20MB | 公差检查 |

**性能优化建议**：
- 亚像素计算使用查找表
- 拟合算法使用最小二乘法（避免迭代优化）
- ROI裁剪后再处理

### 2.3 匹配定位算子（Matching）

| 算子 | 目标延迟 | 内存限制 | 测试条件 |
|------|---------|---------|---------|
| 模板匹配 | <100ms | 100MB | 512x512搜索图，64x64模板 |
| 形状匹配（无遮挡） | <200ms | 200MB | 512x512搜索图，64x64模板 |
| 形状匹配（50%遮挡） | <250ms | 200MB | 同上 |
| 金字塔形状匹配 | <150ms | 200MB | 3层金字塔 |
| AKAZE特征匹配 | <300ms | 150MB | 512x512图，提取500个特征 |
| ORB特征匹配 | <200ms | 150MB | 512x512图，提取500个特征 |

**性能优化建议**：
- 使用金字塔加速（粗层快速筛选）
- 限制搜索区域（基于先验知识）
- 角度步长自适应（粗层大步长）
- 早期终止（分数过低直接跳过）
- 考虑GPU加速（CUDA）

### 2.4 特征提取算子（Feature Extraction）

| 算子 | 目标延迟 | 内存限制 | 测试条件 |
|------|---------|---------|---------|
| 边缘检测（Canny） | <15ms | 50MB | 512x512图 |
| 轮廓检测 | <20ms | 50MB | 512x512二值图 |
| Blob分析 | <30ms | 50MB | 512x512二值图，10个Blob |
| 角点检测 | <25ms | 50MB | 512x512图，检测100个角点 |
| 连通域标注 | <20ms | 50MB | 512x512二值图 |

**性能优化建议**：
- Canny使用OpenCV优化版本
- Blob特征计算并行化（Parallel.For）

### 2.5 3D处理算子（3D Processing）

| 算子 | 目标延迟 | 内存限制 | 测试条件 |
|------|---------|---------|---------|
| 点云加载 | <100ms | 500MB | 100万点PCD文件 |
| 统计滤波 | <200ms | 500MB | 100万点 |
| 体素下采样 | <150ms | 500MB | 100万点 → 10万点 |
| 直通滤波 | <50ms | 500MB | 100万点 |
| RANSAC平面分割 | <300ms | 500MB | 100万点 |
| 欧氏聚类 | <400ms | 500MB | 10万点 |
| ICP配准 | <500ms | 500MB | 2个点云，各5万点 |
| 法向量估计 | <300ms | 500MB | 10万点 |

**性能优化建议**：
- 使用KD-Tree加速邻域搜索
- 下采样后再处理
- 考虑PCL库的优化实现
- 多线程并行处理

### 2.6 深度学习算子（Deep Learning）

| 算子 | 目标延迟（GPU） | 内存限制 | 测试条件 |
|------|---------------|---------|---------|
| YOLO目标检测 | <100ms | 2GB | 640x640输入，YOLOv8n |
| 语义分割 | <150ms | 2GB | 512x512输入，UNet |
| 异常检测（PatchCore） | <200ms | 2GB | 224x224输入 |
| 分类 | <50ms | 1GB | 224x224输入，ResNet50 |

**性能优化建议**：
- 使用TensorRT优化ONNX模型
- 批处理推理（batch size > 1）
- 使用FP16精度（精度损失<1%）
- 模型剪枝和量化

### 2.7 通信算子（Communication）

| 算子 | 目标延迟 | 内存限制 | 测试条件 |
|------|---------|---------|---------|
| Modbus通信 | <20ms | 10MB | 读取10个寄存器 |
| TCP通信 | <20ms | 10MB | 发送100字节 |
| 串口通信 | <30ms | 10MB | 发送100字节 |
| HTTP请求 | <100ms | 10MB | GET请求 |
| MQTT发布 | <50ms | 10MB | 发布100字节消息 |

**性能优化建议**：
- 使用连接池（避免重复建立连接）
- 异步通信（非阻塞）
- 超时设置合理（避免长时间等待）

---

## 三、性能测试方法

### 3.1 测试工具

使用W0-1开发的性能分析工具：

```csharp
using (var profiler = new PerformanceProfiler("OperatorName"))
{
    // 算子执行代码
    var result = @operator.Execute(input);
}
// 自动记录到 performance_log.csv
```

### 3.2 测试流程

1. **准备测试数据**：使用W0-3生成的标准测试图
2. **预热运行**：先运行10次预热（JIT编译）
3. **正式测试**：运行100次，记录每次执行时间
4. **统计分析**：计算平均值、最大值、最小值、标准差
5. **对比预算**：与性能预算表对比
6. **生成报告**：记录到性能报告

### 3.3 测试环境

**标准测试环境**：
- CPU: Intel i7-10700 或同等性能
- 内存: 16GB DDR4
- GPU: NVIDIA GTX 1660 或同等性能（深度学习算子）
- 操作系统: Windows 10/11 或 Ubuntu 20.04
- .NET版本: .NET 6.0 或更高

### 3.4 性能报告格式

```json
{
  "operator_name": "AdvancedCaliperOperator",
  "test_date": "2026-03-16",
  "test_condition": "512x512图像，10个卡尺",
  "runs": 100,
  "statistics": {
    "mean_ms": 45.2,
    "max_ms": 52.1,
    "min_ms": 42.3,
    "std_dev_ms": 2.1
  },
  "budget_ms": 50,
  "pass": true,
  "margin_percent": 9.6
}
```

---

## 四、性能优化指南

### 4.1 常见性能瓶颈

1. **内存分配过多**
   - 问题：频繁new Mat对象
   - 解决：复用Mat对象，使用对象池

2. **逐像素循环**
   - 问题：C#循环访问像素慢
   - 解决：使用OpenCV内置函数或unsafe指针

3. **未使用SIMD**
   - 问题：标量计算慢
   - 解决：使用System.Numerics.Vector

4. **同步阻塞**
   - 问题：等待I/O或通信
   - 解决：使用异步async/await

5. **缓存未命中**
   - 问题：数据访问模式不友好
   - 解决：按行访问图像，提高局部性

### 4.2 优化优先级

1. **算法优化**（最高优先级）
   - 选择更高效的算法
   - 减少计算复杂度

2. **数据结构优化**
   - 使用合适的数据结构
   - 减少内存拷贝

3. **代码优化**
   - 使用OpenCV优化函数
   - 避免不必要的计算

4. **并行优化**
   - 多线程并行
   - GPU加速

### 4.3 优化案例

#### 案例1：Blob特征计算优化

**优化前**：
```csharp
// 逐个Blob串行计算特征
foreach (var blob in blobs)
{
    blob.Circularity = CalculateCircularity(blob);
    blob.Convexity = CalculateConvexity(blob);
    // ...
}
// 耗时: 150ms (10个Blob)
```

**优化后**：
```csharp
// 并行计算
Parallel.ForEach(blobs, blob =>
{
    blob.Circularity = CalculateCircularity(blob);
    blob.Convexity = CalculateConvexity(blob);
    // ...
});
// 耗时: 45ms (10个Blob)
// 加速比: 3.3x
```

#### 案例2：形状匹配角度搜索优化

**优化前**：
```csharp
// 固定1度步长，搜索360度
for (int angle = 0; angle < 360; angle += 1)
{
    var score = MatchAtAngle(template, searchImage, angle);
    // ...
}
// 耗时: 1800ms
```

**优化后**：
```csharp
// 粗层10度步长，细层1度步长
// 粗层筛选
var candidates = new List<int>();
for (int angle = 0; angle < 360; angle += 10)
{
    var score = MatchAtAngle(template, searchImage, angle);
    if (score > threshold) candidates.Add(angle);
}
// 细层精修
foreach (var coarseAngle in candidates)
{
    for (int angle = coarseAngle - 10; angle <= coarseAngle + 10; angle++)
    {
        var score = MatchAtAngle(template, searchImage, angle);
        // ...
    }
}
// 耗时: 250ms
// 加速比: 7.2x
```

---

## 五、性能监控与告警

### 5.1 持续监控

在CI/CD管道中集成性能测试：
- 每次提交自动运行性能测试
- 对比基线性能（W0-4建立）
- 性能回归>20%则失败

### 5.2 告警阈值

| 告警级别 | 条件 | 处理方式 |
|---------|------|---------|
| 🟢 正常 | 性能在预算内 | 无需处理 |
| 🟡 警告 | 超出预算<20% | 记录日志，下次优化 |
| 🔴 严重 | 超出预算≥20% | 阻止合并，必须优化 |

### 5.3 性能仪表盘

建议使用Grafana或类似工具可视化：
- 各算子平均执行时间趋势
- 性能预算达标率
- 性能回归检测

---

## 六、验收标准

### 6.1 阶段1算子验收

所有阶段1算子（W0-W5）必须满足：
- ✅ 执行时间≤性能预算
- ✅ 内存使用≤内存限制
- ✅ 1000次运行无内存泄漏（增长<50MB）
- ✅ 性能报告已生成并归档

### 6.2 性能回归检测

新版本算子性能不得低于基线版本：
- 允许波动范围：±5%
- 超过5%需要说明原因
- 超过20%必须优化

---

## 七、附录

### 7.1 性能测试脚本示例

```csharp
public class PerformanceBenchmark
{
    [Benchmark]
    public void BenchmarkAdvancedCaliper()
    {
        var testImage = TestDataGenerator.GenerateTestImage();
        var roi = new Rect(100, 100, 300, 50);

        using (var profiler = new PerformanceProfiler("AdvancedCaliper"))
        {
            var result = caliperOperator.Execute(testImage, roi);
        }
    }
}
```

### 7.2 参考资料

- OpenCV性能优化指南：https://docs.opencv.org/master/dc/d71/tutorial_py_optimization.html
- .NET性能最佳实践：https://docs.microsoft.com/en-us/dotnet/standard/performance/
- PCL性能优化：http://pointclouds.org/documentation/tutorials/

---

*本文档随项目进展持续更新*
