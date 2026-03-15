# ClearVision 对标 Halcon 算子深化路线图（Vibe Coding版）

> **文档编号**: roadmap-halcon-deep-dive  
> **版本**: V2.0-Vibe  
> **创建日期**: 2026-03-16  
> **目标**: AI辅助编程友好型深化路线，小步快跑，快速验证

---

## 一、Vibe Coding原则

### 1.1 核心原则

| 原则 | 说明 | 实践方式 |
|------|------|----------|
| **原子化任务** | 每个任务可独立完成，<2小时 | 按算子/功能拆分为最小单元 |
| **即时验证** | 每步都有自动化测试 | 单元测试先行，失败即停 |
| **Prompt就绪** | 每个任务附带AI生成提示 | 复制粘贴即可生成代码框架 |
| **迭代增量** | 从MVP到完善，逐步增强 | 先跑通，再优化 |
| **可复制复现** | 测试数据和预期结果明确 | 提供标准测试图和数值基准 |

### 1.2 文档使用方式

```
用户/AI助手使用流程：
1. 选择当前阶段任务
2. 复制任务对应的Prompt模板
3. AI生成代码框架
4. 填入测试数据验证
5. 通过→提交；失败→调整Prompt重试
```

---

## 二、能力差距全景分析

### 2.1 差距矩阵（AI优化优先级）

| 能力 | 差距 | AI实现难度 | 优先级 | 推荐实现顺序 |
|------|------|-----------|--------|-------------|
| **亚像素卡尺** | ⭐⭐⭐⭐⭐ | ⭐⭐☆☆☆ | P0 | **第1周** ✅ 算法成熟，OpenCV有参考 |
| **Blob特征** | ⭐⭐⭐☆☆ | ⭐☆☆☆☆ | P0 | **第1周** ✅ 纯计算，无依赖 |
| **ROI跟踪** | ⭐⭐⭐☆☆ | ⭐⭐☆☆☆ | P1 | **第2周** ✅ 坐标变换逻辑清晰 |
| **形状匹配** | ⭐⭐⭐⭐⭐ | ⭐⭐⭐☆☆ | P1 | **第3-4周** ⚠️ 需金字塔+梯度计算 |
| **纹理分析** | ⭐⭐⭐⭐☆ | ⭐⭐☆☆☆ | P2 | **第6周** ✅ Laws滤波有现成公式 |
| **3D点云** | ⭐⭐⭐⭐⭐ | ⭐⭐⭐⭐☆ | P2 | **第7-8周** ⚠️ 需PCL/Open3D参考 |
| **深度学习** | ⭐⭐⭐☆☆ | ⭐⭐☆☆☆ | P3 | **第10周** ✅ ONNX模型集成 |

### 2.2 AI友好任务特征

✅ **适合AI实现**：
- 算法原理明确（有论文/教程）
- OpenCV/PCL有参考实现
- 输入输出数据格式清晰
- 可单元测试验证

⚠️ **需要人工介入**：
- 性能优化（需Profiling）
- 硬件SDK对接（需设备）
- 复杂的UI交互设计

---

## 三、核心任务详解（含AI Prompt模板）

### 3.1 亚像素卡尺（P0，第1周）

#### 为什么先做这个？
- 算法成熟，Halcon原理公开
- OpenCV亚像素角点检测可参考
- 有明确精度指标可验证

#### AI Prompt模板

```markdown
【任务】实现亚像素边缘检测卡尺工具

【上下文】
- 基于OpenCvSharp4
- 已有Mat图像输入
- 需要实现灰度重心法和Zernike矩法

【要求】
1. 实现灰度重心法（快，精度0.1-0.3像素）
2. 实现Zernike矩法（精，精度0.01-0.05像素）
3. 支持边缘对测量（两个边缘间的距离）
4. 包含单元测试，验证精度

【输入输出规格】
- 输入：Mat roiLine（1xN的灰度线条）
- 输出：float subPixelPosition（亚像素位置）

【参考公式】
灰度重心法：
  position = Σ(i * gray[i]) / Σ(gray[i])

Zernike矩法：
  参考Ghosal & Mehrotra 1993论文
  计算Z00, Z11, Z20矩，推导边缘位置

【测试数据】
- 测试图：ISO 12233斜边测试图
- 期望精度：<0.05像素

【生成代码结构】
- SubPixelEdgeDetector.cs（检测器类）
- AdvancedCaliperOperator.cs（算子包装）
- SubPixelEdgeDetectorTests.cs（单元测试）
```

#### 验证Checklist

- [ ] 斜边测试精度≤0.05像素
- [ ] 1000次重复测量std<0.02像素
- [ ] 与OpenCV cornerSubPix结果对比，误差<1%

---

### 3.2 Blob特征扩展（P0，第1周，与上面并行）

#### AI Prompt模板

```markdown
【任务】扩展Blob分析算子，增加工业级特征

【上下文】
- 现有基础BlobAnalysis算子
- 使用OpenCvSharp的ConnectedComponents

【要求】
1. 基于现有连通域结果，计算以下特征：
   - 圆度 Circularity = 4π*Area/Perimeter²
   - 凸性 Convexity = Area/ConvexHullArea
   - 矩形度 Rectangularity = Area/(Width*Height)
   - 离心率 Eccentricity
   - 欧拉数 EulerNumber
2. 支持灰度特征（需要原图）：MeanGray, GrayDeviation
3. 支持FeatureFilter表达式筛选

【输入】
- Mat binaryImage（二值图）
- Mat sourceImage（原图，可选）
- string featureFilter（筛选表达式，如"Area>100 AND Circularity>0.8"）

【输出】
- BlobFeature[] features（特征数组）

【参考】
- OpenCV: cv2.fitEllipse, cv2.convexHull
- Halcon: region_features文档

【测试数据】
- 圆形：圆度>0.99
- 矩形：矩形度>0.95，圆度<0.8
- 带孔洞区域：欧拉数=1-孔洞数
```

---

### 3.3 鲁棒形状匹配（P1，第3-4周）

#### AI Prompt模板

```markdown
【任务】实现基于边缘梯度的形状匹配（Halcon形状匹配简化版）

【核心算法】
1. 模板创建：
   - 提取模板边缘点（Canny/Sobel）
   - 计算每个边缘点的梯度方向
   - 构建金字塔（3-4层）

2. 搜索匹配：
   - 最粗层快速筛选候选位置
   - 逐层精修
   - 原始层最小二乘精修

3. 匹配评分：
   - 基于梯度方向差异（非灰度值）
   - score = Σ(1 - |Δangle|/π) / N

【要求】
- 支持旋转（-180~+180度）
- 支持缩放（0.8-1.2倍）
- 亚像素定位精度
- 部分遮挡鲁棒性（30%遮挡仍可匹配）

【类设计】
```csharp
class ShapeMatcher {
    void CreateTemplate(Mat template, int pyramidLevels);
    List<MatchResult> Search(Mat searchImage, double minScore);
}

struct MatchResult {
    Point2f Position;  // 亚像素
    float Angle;
    float Scale;
    float Score;
}
```

【参考实现】
- OpenCV shape module
- OpenCV matchTemplate（参考金字塔加速思路）
- Halcon Shape-Based Matching白皮书（原理）

【测试标准】
- 光照变化±50%：匹配分数>0.7
- 30%遮挡：匹配分数>0.6
- 精度：<0.1像素
```

---

### 3.4 3D点云基础（P2，第7-8周）

#### AI Prompt模板

```markdown
【任务】实现3D点云基础数据结构和滤波算子

【依赖】
- 使用Open3D.NET或自研（参考PCL）
- 先实现基础数据结构，再集成相机SDK

【要求】
1. PointCloud数据类：
   - Points: Nx3 float (x,y,z)
   - Colors: Nx3 byte (可选)
   - Normals: Nx3 float (可选)

2. 滤波算子：
   - 统计滤波（移除离群点）
   - 体素下采样
   - 直通滤波（按坐标范围裁剪）

3. 分割算子：
   - RANSAC平面分割
   - 欧氏聚类

【参考】
- PCL: StatisticalOutlierRemoval, VoxelGrid
- Open3D: point_cloud.py

【测试数据】
- 使用公开数据集：Stanford 3D Scanning Repository
- 或使用合成数据（立方体、球体、平面）

【注意】
- 先实现文件I/O（PCD/PLY），再对接相机
- 点云可视化可用Open3D或自研简易渲染
```

---

## 四、Vibe Coding迭代计划

### 4.1 迭代节奏

```
每日节奏：
├── 选择1-2个原子任务（<2小时）
├── AI生成代码框架
├── 填入测试数据
├── 运行测试
├── ✅通过 → Git提交 → 下一个任务
└── ❌失败 → 调整Prompt → 重试

每周节奏：
├── 周一：规划本周3-5个任务
├── 每日：迭代开发
├── 周五：集成测试，阶段验收
└── 周末：复盘，调整下周计划
```

### 4.2 Prompt工程最佳实践

#### 好的Prompt特征

✅ **好的示例**（Blob特征）：
```markdown
【任务】计算Blob圆度
【公式】Circularity = 4 * PI * Area / (Perimeter * Perimeter)
【输入】Mat binaryImage, int labelId
【输出】double circularity
【参考】OpenCV: contourArea, arcLength
【测试】正圆→0.99+，正方形→0.785
```

❌ **不好的示例**：
```markdown
实现一个Blob分析算子，要专业一点，像Halcon那样。
```

#### Prompt模板库

每个任务类别都有标准Prompt模板，复制即用：
- `[Template] 图像滤波算子`
- `[Template] 特征测量算子`
- `[Template] 几何变换算子`
- `[Template] 点云处理算子`

---

## 五、分阶段里程碑

### 阶段1：基础补强（第1-4周）

**目标**：核心测量能力达到工业可用

| 周次 | 任务 | 验证标准 | Prompt就绪 |
|------|------|----------|-----------|
| W1 | 亚像素卡尺 + Blob特征 | 精度<0.05px | ✅ |
| W2 | ROI自动跟踪机制 | 延迟<1ms | ✅ |
| W3 | 形状匹配MVP（基础版） | 无遮挡匹配 | ✅ |
| W4 | 形状匹配增强（金字塔+遮挡） | 30%遮挡OK | ✅ |

### 阶段2：专业深化（第5-8周）

**目标**：3D+纹理+颜色，形成专业工具链

| 周次 | 任务 | 验证标准 | Prompt就绪 |
|------|------|----------|-----------|
| W5-W6 | 3D点云I/O + 滤波 | 加载PCD<1s | ✅ |
| W7 | 点云分割 + 表面匹配MVP | 平面分割误差<1mm | ⚠️ |
| W8 | 纹理分析（Laws/GLCM） + CIE颜色 | 纹理分类准确率>90% | ✅ |

### 阶段3：生态扩展（第9-12周）

**目标**：AI+3D融合，差异化能力

| 周次 | 任务 | 验证标准 | Prompt就绪 |
|------|------|----------|-----------|
| W9-W10 | 深度学习算子（分割/异常检测） | mIoU>0.85 | ✅ |
| W11 | 手眼标定向导 | 标定误差<0.5mm | ⚠️ |
| W12 | GPU加速 + 集成优化 | 关键算子5x加速 | ⚠️ |

---

## 六、测试数据集（预置）

### 6.1 2D测试图

| 名称 | 用途 | 获取方式 |
|------|------|----------|
| `iso12233_slant_edge.png` | 亚像素精度测试 | ISO标准图，网络下载 |
| `circle_square_triangle.png` | Blob特征测试 | 程序生成 |
| `template_matching_test.png` | 形状匹配测试 | 工业样本或合成 |
| `texture_brochette.png` | 纹理分析测试 | 公开纹理数据集 |

### 6.2 3D测试数据

| 名称 | 用途 | 获取方式 |
|------|------|----------|
| `plane_100k.pcd` | 平面分割测试 | 程序生成 |
| `bunny.pcd` | 匹配测试 | Stanford兔子 |
| `scene_with_box.ply` | 场景测试 | 合成或扫描 |

### 6.3 测试数据生成脚本

```csharp
// 生成标准测试图
public static class TestDataGenerator
{
    // 生成斜边图（用于亚像素测试）
    public static Mat GenerateSlantEdge(int width, int height, double angle)
    {
        // AI生成实现...
    }
    
    // 生成带噪声的几何形状
    public static Mat GenerateShapeWithNoise(string shape, double noiseLevel)
    {
        // AI生成实现...
    }
}
```

---

## 七、AI编程Checklist

### 每个任务的检查项

```markdown
□ 任务理解
  □ 输入输出数据类型明确
  □ 算法原理/公式清晰
  □ 有参考实现（OpenCV/PCL/Halcon原理）

□ 代码生成
  □ Prompt包含完整上下文
  □ 生成代码可直接编译
  □ 类/方法命名符合项目规范

□ 测试验证
  □ 单元测试用例≥3个
  □ 有预期结果数值
  □ 测试通过/失败标准明确

□ 集成
  □ 符合现有算子基类规范
  □ 元数据（端口/参数）完整
  □ 文档注释完整
```

---

## 八、快速开始指南

### 今天就能开始

1. **选择第一个任务**：Blob特征扩展（最简单）
2. **复制Prompt**：从3.2节复制
3. **AI生成代码**：粘贴到ChatGPT/Claude
4. **填入项目**：复制到Acme.Product.Infrastructure
5. **运行测试**：使用提供的测试数据
6. **提交代码**：Git commit

### 本周目标

> 完成亚像素卡尺 + Blob特征扩展，精度达到Halcon 60%水平

---

## 九、总结

### Vibe Coding核心理念

> **不是"要4个人做6个月"，而是"AI+人，每周迭代，每周有成果"**

### 成功指标

| 时间 | 可演示成果 | 验证方式 |
|------|-----------|----------|
| 第1周末 | 亚像素测量精度<0.05px | 斜边测试图 |
| 第4周末 | 形状匹配鲁棒性OK | 遮挡测试视频 |
| 第8周末 | 3D点云处理可用 | 点云可视化截图 |
| 第12周末 | AI+3D融合演示 | 端到端检测Demo |

---

*本文档持续更新，每个任务完成后补充实际Prompt和踩坑记录*
