# 阶段2 TODO：专业深化（Vibe Coding版 V2.1）

> **阶段目标**: 3D+纹理+颜色，形成专业工具链，Halcon 75%水平
> **时间周期**: 第6-11周（原5-8周，调整为6-11周，增加缓冲）
> **前置条件**: 阶段1里程碑达成（形状匹配精度<0.1px，W0-W5完成）
> **核心理念**: 复杂任务拆解，每个子任务<3小时，AI可独立完成

---

## 零、第5周：点云测试数据准备（新增，必做）

> **为什么先做这个？** 没有硬件或测试数据就无法验证3D算子

### 任务W5-0：合成点云生成器（2小时）⭐ 关键任务 ✅ 已完成（2026-03-17）

```markdown
【任务】实现标准3D测试点云生成工具

【为什么重要】
- 3D相机硬件采购周期长（可能延迟到W10+）
- 无法等硬件到位才开始开发
- 合成数据可覆盖大部分测试场景

【要求】
1. 生成几何体点云（平面、球体、圆柱体、立方体）
2. 添加噪声模拟传感器误差
3. 保存为PCD/PLY格式
4. 生成带颜色和法向量的点云

【AI Prompt】
```markdown
【任务】实现3D点云合成生成器

【要求】
1. 生成平面点云：
   - 参数：中心点、法向量、尺寸（长×宽）、点密度
   - 公式：在平面上均匀采样点
   - 添加高斯噪声（sigma=0.5mm）

2. 生成球体点云：
   - 参数：中心、半径、点数
   - 公式：球面参数方程
     x = r * sin(θ) * cos(φ)
     y = r * sin(θ) * sin(φ)
     z = r * cos(θ)
   - 添加高斯噪声

3. 生成圆柱体点云：
   - 参数：底面中心、轴向量、半径、高度、点数
   - 公式：圆柱面参数方程
   - 添加高斯噪声

4. 生成立方体点云：
   - 参数：中心、边长、点数
   - 表面采样

5. 添加离群点：
   - 随机添加5-10%的离群点
   - 用于测试统计滤波

【输出文件】
- SyntheticPointCloudGenerator.cs

【使用示例】
```csharp
var generator = new SyntheticPointCloudGenerator();

// 生成平面
var plane = generator.GeneratePlane(
    center: new Vector3(0, 0, 0),
    normal: new Vector3(0, 0, 1),
    size: (1.0f, 1.0f),
    density: 1000,
    noise: 0.0005f
);
plane.Save("test_plane.pcd");

// 生成球体
var sphere = generator.GenerateSphere(
    center: new Vector3(0, 0, 0),
    radius: 0.05f,
    numPoints: 2000,
    noise: 0.0005f
);
sphere.Save("test_sphere.pcd");

// 生成圆柱体
var cylinder = generator.GenerateCylinder(
    center: new Vector3(0, 0, 0),
    axis: new Vector3(0, 0, 1),
    radius: 0.03f,
    height: 0.1f,
    numPoints: 1500,
    noise: 0.0005f
);
cylinder.Save("test_cylinder.pcd");
```

【AI 编程专属上下文】
- **定位**：这是测试工具类，不需要继承OperatorBase
- **数学库**：使用System.Numerics.Vector3
- **随机数**：使用Random或MathNet.Numerics生成高斯噪声
- **保存格式**：调用W5-2的PointCloudIO.SavePCD

【测试】
- 生成平面，验证所有点到平面距离<1mm（除噪声外）
- 生成球体，验证所有点到中心距离≈半径（除噪声外）
- 生成圆柱体，验证几何正确性
- 使用Open3D或CloudCompare可视化验证
```

【验证标准】
□ 生成至少4种几何体点云
□ 噪声可控（sigma参数有效）
□ 保存的PCD文件可被Open3D加载
□ 点云可视化形状正确
```

【实现落地】
- 生成器：`Acme.Product/src/Acme.Product.Infrastructure/PointCloud/SyntheticPointCloudGenerator.cs`
- 点云数据结构：`Acme.Product/src/Acme.Product.Infrastructure/PointCloud/PointCloud.cs`
- 文件I/O：`Acme.Product/src/Acme.Product.Infrastructure/PointCloud/PointCloudIO.cs`
- 自动化测试：`Acme.Product/tests/Acme.Product.Tests/PointCloud/SyntheticPointCloudGeneratorTests.cs`、`Acme.Product/tests/Acme.Product.Tests/PointCloud/PointCloudIOTests.cs`

### W5验收标准

- ✅ 合成点云生成器可用
- ✅ 生成至少4种测试点云
- ✅ 点云文件可被第三方工具加载
- ✅ 为后续W6-W8提供测试数据

---



## 一、本阶段任务总览

```
Week 5:   点云测试数据准备（新增）
Week 6:   3D点云基础（数据结构+I/O+滤波）
Week 7:   点云分割（RANSAC+聚类）
Week 8:   点云匹配（简化为PPF特征）
Week 9:   纹理分析（Laws+GLCM）
Week 10:  颜色测量（CIE Lab+DeltaE）
Week 11:  集成测试与优化
```

### 时间调整说明

**原计划**：第5-8周（4周）
**调整后**：第6-11周（6周）
**原因**：
1. 新增W5点云测试数据准备（硬件依赖缓解）
2. W6-2 FPFH改为PPF（降低复杂度，节省2小时）
3. 新增W11集成测试与优化（确保质量）
4. 为3D算子增加缓冲时间（首次实现，风险较高）

### AI友好度评估

| 任务 | AI友好度 | 难点 | 建议 |
|------|---------|------|------|
| 合成点云生成 | ⭐⭐⭐⭐⭐ | 无 | 参数方程清晰 |
| 3D数据结构 | ⭐⭐⭐⭐⭐ | 无 | 直接生成 |
| 点云滤波 | ⭐⭐⭐⭐⭐ | 无 | PCL有参考实现 |
| RANSAC分割 | ⭐⭐⭐⭐☆ | 迭代逻辑 | 伪代码清晰即可 |
| PPF特征（简化） | ⭐⭐⭐⭐☆ | 4D描述子 | 比FPFH简单很多 |
| 纹理分析 | ⭐⭐⭐⭐⭐ | 无 | 数学公式明确 |
| 颜色测量 | ⭐⭐⭐⭐⭐ | 无 | CIE标准公式 |

---

## 二、Week 6：3D点云基础

### 任务W6-1：PointCloud数据结构（2小时）✅ 已完成（2026-03-17）

```markdown
【任务】设计点云数据类
【参考】PCL PointCloud<T>, Open3D PointCloud

【要求】
```csharp
public class PointCloud : IDisposable
{
    // 核心数据（必需）
    public Mat Points { get; }  // Nx3 float (x,y,z)
    
    // 可选数据
    public Mat Colors { get; }   // Nx3 byte RGB
    public Mat Normals { get; }  // Nx3 float (nx,ny,nz)
    
    // 元数据
    public int Count => Points.Rows;
    public bool IsOrganized { get; }  // 是否有序（图像结构）
    public int Width { get; }         // 有序点云的宽度
    public int Height { get; }        // 有序点云的高度
    
    // 工厂方法
    public static PointCloud FromDepthMap(Mat depth, Mat cameraMatrix);
    public static PointCloud Load(string path);  // 支持PCD/PLY
    
    // 空间操作
    public PointCloud Transform(Matrix4x4 transform);
    public PointCloud Crop(AxisAlignedBoundingBox box);
    
    // AABB包围盒
    public AxisAlignedBoundingBox GetAABB();
}

public struct AxisAlignedBoundingBox
{
    public Vector3 Min;
    public Vector3 Max;
    public Vector3 Center => (Min + Max) / 2;
    public Vector3 Extent => Max - Min;
}
```

【AI 编程专属上下文】
- **内存安全**: `PointCloud` 类作为核心实体，其内部维护的 `Mat` 对象必须在 `IDisposable.Dispose()` 中安全释放或归还至 `MatPool.Shared`。
- **算子通讯**: 应当将其封装为特定的 `PortDataType.Object` 或者在平台侧扩充 `PortDataType.PointCloud` 枚举类型，以便跨算子节点流转。

【测试】
- 创建1000个随机点云
- 验证AABB计算正确
- 验证Transform后坐标正确
```

【实现落地】
- `Acme.Product/src/Acme.Product.Infrastructure/PointCloud/PointCloud.cs`
- `Acme.Product/src/Acme.Product.Infrastructure/PointCloud/AxisAlignedBoundingBox.cs`

---

### 任务W6-2：PCD/PLY文件读写（2小时）✅ 已完成（2026-03-17）

```markdown
【任务】实现点云文件I/O
【参考】PCD文件格式文档，PLY格式文档

【PCD格式示例】
```
# .PCD v0.7 - Point Cloud Data file format
VERSION 0.7
FIELDS x y z rgb
SIZE 4 4 4 4
TYPE F F F U
COUNT 1 1 1 1
WIDTH 213
HEIGHT 1
VIEWPOINT 0 0 0 1 0 0 0
POINTS 213
DATA ascii
0.93773 0.33763 0 4.2108e+06
0.90805 0.35641 0 4.2108e+06
...
```

【生成代码】
```csharp
public static class PointCloudIO
{
    public static PointCloud LoadPCD(string path);
    public static void SavePCD(string path, PointCloud cloud, bool binary = false);
    
    public static PointCloud LoadPLY(string path);
    public static void SavePLY(string path, PointCloud cloud);
}
```

【测试数据】
- 下载Stanford bunny PCD文件
- 测试加载和保存一致性
```

【实现落地】
- `Acme.Product/src/Acme.Product.Infrastructure/PointCloud/PointCloudIO.cs`
- 自动化测试：`Acme.Product/tests/Acme.Product.Tests/PointCloud/PointCloudIOTests.cs`

---

### 任务W6-3：体素下采样（2小时）✅ 已完成（2026-03-17）

```markdown
【任务】实现点云体素下采样（Voxel Grid Filter）
【算法】
1. 计算点云AABB
2. 将空间划分为体素网格（leaf size = voxel size）
3. 每个体素内只保留一个点（中心点或随机点）

【伪代码】
```
voxel_grid = empty hash map
for each point p in cloud:
    voxel_x = floor(p.x / leaf_size)
    voxel_y = floor(p.y / leaf_size)
    voxel_z = floor(p.z / leaf_size)
    key = (voxel_x, voxel_y, voxel_z)
    
    if key not in voxel_grid:
        voxel_grid[key] = p
```

【生成代码】
```csharp
public class VoxelGridFilter
{
    public PointCloud Downsample(PointCloud input, float leafSize)
    {
        // 实现上述算法
    }
}
```

【AI 编程专属上下文】
- **封装算子**: 需要创建一个 `VoxelDownsampleOperator : OperatorBase` 来包裹此算法核心类。
- **后台耗时计算**: 由于点云过滤是 CPU 密集型运算，必须在 `ExecuteCoreAsync` 内使用包装好的父类方法 `RunCpuBoundWork` 搭配 `CancellationToken` 进行线程让渡。

【测试】
- 100万点 → 0.01m体素 → 应减少90%以上
- 验证下采样后点云形状保持
```

【实现落地】
- 算法：`Acme.Product/src/Acme.Product.Infrastructure/PointCloud/Filters/VoxelGridFilter.cs`
- 算子封装：`Acme.Product/src/Acme.Product.Infrastructure/Operators/VoxelDownsampleOperator.cs`
- 自动化测试：`Acme.Product/tests/Acme.Product.Tests/PointCloud/VoxelGridFilterTests.cs`、`Acme.Product/tests/Acme.Product.Tests/Operators/VoxelDownsampleOperatorTests.cs`

---

### 任务W6-4：统计滤波（2小时）✅ 已完成（2026-03-17）

```markdown
【任务】实现统计离群点移除（Statistical Outlier Removal）
【算法】
1. 对每个点，找K个最近邻
2. 计算K邻域的平均距离mean_dist
3. 计算所有mean_dist的全局均值global_mean和标准差global_std
4. 移除mean_dist > global_mean + std_mul * global_std的点

【生成代码】
```csharp
public class StatisticalOutlierRemoval
{
    public PointCloud Filter(
        PointCloud input,
        int meanK = 50,           // K近邻数量
        double stddevMul = 1.0    // 标准差倍数阈值
    )
    {
        // 使用KDTree加速K近邻搜索
        // 或使用暴力搜索（点数量<10万时可行）
    }
}
```

【优化建议】
- 先实现暴力搜索版本（简单）
- 后期用KDTree优化（复杂）

【测试】
- 合成点云加入随机离群点
- 验证滤波后离群点被移除
```

【实现落地】
- 算法：`Acme.Product/src/Acme.Product.Infrastructure/PointCloud/Filters/StatisticalOutlierRemoval.cs`
- 算子封装：`Acme.Product/src/Acme.Product.Infrastructure/Operators/StatisticalOutlierRemovalOperator.cs`
- 自动化测试：`Acme.Product/tests/Acme.Product.Tests/PointCloud/StatisticalOutlierRemovalTests.cs`、`Acme.Product/tests/Acme.Product.Tests/Operators/StatisticalOutlierRemovalOperatorTests.cs`

---

## 三、Week 7：点云分割

### 任务W7-1：RANSAC平面分割（3小时）

```markdown
【任务】实现RANSAC平面分割
【算法】
```
输入: 点云, 距离阈值d, 最大迭代N, 最小内点数MinPoints
输出: 平面系数(a,b,c,d), 内点集合

for i = 1 to N:
    // 1. 随机采样3个点
    idx1, idx2, idx3 = random
    p1, p2, p3 = points[idx1], points[idx2], points[idx3]
    
    // 2. 计算平面方程
    normal = cross(p2-p1, p3-p1)
    normalize(normal)
    d = -dot(normal, p1)
    
    // 3. 统计内点
    inliers = []
    for each point p:
        distance = abs(dot(normal, p) + d)
        if distance < threshold:
            inliers.append(p)
    
    // 4. 记录最佳模型
    if len(inliers) > best_inliers:
        best_model = (normal, d)
        best_inliers = inliers

// 5. 可选：用所有内点重新拟合平面（最小二乘）
```

【生成代码】
```csharp
public class RansacPlaneSegmentation
{
    public (Vector3 normal, float d, int[] inliers) Segment(
        PointCloud cloud,
        float distanceThreshold = 0.01f,
        int maxIterations = 1000,
        int minInliers = 100
    )
    {
        // 实现上述算法
    }
}
```

【AI 编程专属上下文】
- **外部依赖**: 运算使用的 `Vector3` 可引入 `System.Numerics`。
- **算子暴露参数**: `distanceThreshold`，`maxIterations` 和 `minInliers` 必须暴露为算子配置参数 `[OperatorParam]` 并在 `ValidateParameters` 方法中验证不得小于等于 0。

【测试】
- 合成点云：平面 + 噪声
- 验证分割出的平面法向量正确
- 验证内点比例>80%
```

---

### 任务W7-2：欧氏聚类分割（2小时）

```markdown
【任务】实现欧氏距离聚类（Euclidean Cluster Extraction）
【算法】连通区域分析，3D版本

```
输入: 点云, 聚类容差cluster_tolerance, 最小/最大点数
输出: 聚类列表

已访问标记 = false
聚类列表 = []

for each point i:
    if 已访问[i]: continue

    // BFS搜索连通区域
    当前聚类 = []
    队列 = [i]
    已访问[i] = true

    while 队列不为空:
        current = 队列.Dequeue()
        当前聚类.Add(current)

        // 查找cluster_tolerance内的邻居
        neighbors = FindNeighbors(current, cluster_tolerance)
        for each neighbor:
            if not 已访问[neighbor]:
                已访问[neighbor] = true
                队列.Enqueue(neighbor)

    if MinSize < len(当前聚类) < MaxSize:
        聚类列表.Add(当前聚类)
```

【优化】
- 先用KDTree加速邻域搜索
- 或先用体素下采样减少点数

【生成代码】略（按上述伪代码实现）
```

---

## 四、Week 8：点云匹配（简化为PPF）

### 任务W8-1：PPF点对特征（3小时）⭐ 修订任务

```markdown
【任务】实现PPF（Point Pair Features）特征描述子
【参考】Drost et al. "Model Globally, Match Locally: Efficient and Robust 3D Object Recognition"

【为什么改为PPF？】
- FPFH是33维特征，实现复杂度高（原计划4小时不够）
- PPF是4维特征，更简单但足够有效
- 适合Vibe Coding快速迭代原则

【PPF定义】
对于点对(p1, p2)及其法向量(n1, n2)，PPF特征为4维：
- F1 = ||d||  （点间距离）
- F2 = ∠(n1, d)  （n1与连线的角度）
- F3 = ∠(n2, d)  （n2与连线的角度）
- F4 = ∠(n1, n2)  （两法向量的角度）

其中 d = p2 - p1

【算法流程】
1. 计算点云法向量（PCA或邻域拟合）
2. 对每个参考点，计算与邻域点的PPF特征
3. 构建PPF哈希表用于快速匹配

【生成代码】
```csharp
public struct PPFFeature
{
    public float Distance;      // F1
    public float Angle1;        // F2
    public float Angle2;        // F3
    public float AngleNormals;  // F4

    public static PPFFeature Compute(
        Vector3 p1, Vector3 n1,
        Vector3 p2, Vector3 n2
    )
    {
        var d = p2 - p1;
        var dist = d.Length();
        d = Vector3.Normalize(d);

        return new PPFFeature
        {
            Distance = dist,
            Angle1 = (float)Math.Acos(Vector3.Dot(n1, d)),
            Angle2 = (float)Math.Acos(Vector3.Dot(n2, d)),
            AngleNormals = (float)Math.Acos(Vector3.Dot(n1, n2))
        };
    }
}

public class PPFEstimation
{
    public Dictionary<int, List<PPFFeature>> ComputeModel(
        PointCloud model,
        float normalRadius = 0.03f,
        float featureRadius = 0.05f
    )
    {
        // 1. 计算法线
        var normals = EstimateNormals(model, normalRadius);

        // 2. 对每个参考点，计算PPF特征
        var ppfMap = new Dictionary<int, List<PPFFeature>>();

        for (int i = 0; i < model.Count; i++)
        {
            var features = new List<PPFFeature>();
            var neighbors = FindNeighbors(model, i, featureRadius);

            foreach (var j in neighbors)
            {
                var ppf = PPFFeature.Compute(
                    model.Points[i], normals[i],
                    model.Points[j], normals[j]
                );
                features.Add(ppf);
            }

            ppfMap[i] = features;
        }

        return ppfMap;
    }
}
```

【AI 编程专属上下文】
- **数学库**：使用System.Numerics.Vector3
- **法向量估计**：可使用PCA或平面拟合
- **邻域搜索**：先用暴力搜索，后期优化用KDTree
- **角度计算**：使用Math.Acos(Vector3.Dot(a, b))

【测试】
- 同一点云不同视角的PPF特征应相似
- 验证旋转不变性
- 对比点对的PPF特征一致性
```

【验证标准】
□ 编译通过
□ PPF特征计算正确（4维向量）
□ 旋转不变性测试通过
□ 性能：10000点<5秒
```

---

### 任务W8-2：基于PPF的表面匹配（3小时）

```markdown
【任务】实现基于PPF的3D表面匹配（简化版）
【算法流程】
1. 离线：计算模型点云的PPF特征，构建哈希表
2. 在线：
   - 在场景中采样参考点
   - 计算参考点的PPF特征
   - 在哈希表中查找匹配
   - RANSAC验证几何一致性
   - 返回6DOF位姿

【简化策略】
- 不做完整的全局投票，只做局部匹配
- 参考点采样而非全部点（减少计算量）
- RANSAC用3点法估计位姿

【生成代码框架】
```csharp
public class PPFMatcher
{
    private Dictionary<int, List<PPFFeature>> modelPPF;

    public void SetModel(PointCloud model, float featureRadius)
    {
        var estimator = new PPFEstimation();
        modelPPF = estimator.ComputeModel(model, featureRadius);
    }

    public Pose3D Match(
        PointCloud scene,
        float featureRadius = 0.05f,
        int numSamples = 100
    )
    {
        // 1. 在场景中采样参考点
        var refPoints = SamplePoints(scene, numSamples);

        // 2. 对每个参考点，计算PPF并匹配
        var correspondences = new List<(int sceneIdx, int modelIdx)>();

        foreach (var refIdx in refPoints)
        {
            var scenePPF = ComputePPF(scene, refIdx, featureRadius);
            var matches = FindMatches(scenePPF, modelPPF);
            correspondences.AddRange(matches);
        }

        // 3. RANSAC验证，估计位姿
        var pose = RansacPoseEstimation(scene, model, correspondences);

        return pose;
    }
}
```

【测试数据】
- 使用W5-0生成的合成点云
- 测试匹配精度和召回率

【评估指标】
- 精度：匹配位姿与真值误差<5mm/<5度
- 召回率：>70%（比FPFH略低但可接受）
- 性能：<3秒（10000点场景）
```

【验证标准】
□ 编译通过
□ 匹配测试通过（精度<5mm）
□ 性能满足预算
□ 鲁棒性测试（30%遮挡）通过
```

---

## 五、Week 9：纹理分析

### 任务W9-1：Laws纹理滤波（2小时）

```markdown
【任务】实现Laws纹理能量滤波
【参考】Laws, K.I. "Rapid Texture Identification"

【Laws核】（5x5）
- L5 (Level) = [1, 4, 6, 4, 1]
- E5 (Edge) = [-1, -2, 0, 2, 1]
- S5 (Spot) = [-1, 0, 2, 0, -1]
- W5 (Wave) = [-1, 2, 0, -2, 1]
- R5 (Ripple) = [1, -4, 6, -4, 1]

【组合】
25种组合，常用：E5L5, L5E5, S5S5等

【算法】
1. 选择两个1D核（如E5和L5）
2. 外积生成2D核：K = E5' * L5
3. 图像与核卷积
4. 计算纹理能量（窗口内平方和平均）

【生成代码】
```csharp
public class LawsTextureFilter
{
    public Mat ApplyFilter(Mat image, string kernelCombo)
    {
        // 解析kernelCombo（如"E5L5"）
        // 生成2D核
        // 卷积
        // 返回滤波结果或能量图
    }
    
    public Mat ComputeEnergy(Mat filteredImage, int windowSize)
    {
        // 计算局部能量
        // 返回能量图
    }
}
```

【测试】
- Brodatz纹理数据集
- 验证不同纹理的能量差异
```

---

### 任务W9-2：GLCM纹理特征（3小时）

```markdown
【任务】实现灰度共生矩阵（GLCM）纹理分析
【算法】
1. 灰度量化（如256级→16级）
2. 统计像素对(i,j)在给定方向和距离的出现次数
3. 归一化得到概率P(i,j)
4. 计算特征：
   - 对比度 Contrast = Σ(i-j)² * P(i,j)
   - 相关性 Correlation
   - 能量 Energy = ΣP(i,j)²
   - 同质性 Homogeneity = ΣP(i,j) / (1+|i-j|)
   - 熵 Entropy = -ΣP(i,j) * log(P(i,j))

【生成代码】
```csharp
public class GLCMFeatures
{
    public double Contrast { get; set; }
    public double Correlation { get; set; }
    public double Energy { get; set; }
    public double Homogeneity { get; set; }
    public double Entropy { get; set; }
}

public class GLCMTexture
{
    public GLCMFeatures Compute(
        Mat image,
        int distance = 1,
        string direction = "0_degree",  // 0,45,90,135或average
        int grayLevels = 256
    )
    {
        // 实现上述算法
    }
}
```

【测试】
- 验证各特征与纹理视觉特性的一致性
- 粗糙纹理：高对比度
- 平滑纹理：高能量，低对比度
```

---

## 六、Week 10：颜色测量

### 任务W10-1：CIE Lab颜色转换（2小时）

```markdown
【任务】实现RGB到CIE Lab颜色空间转换
【参考】CIE 1976 L*a*b*色彩空间

【转换步骤】
1. RGB → XYZ（使用D65白点）
   - 先线性化（gamma校正）
   - 矩阵变换
2. XYZ → Lab
   - L = 116 * f(Y/Yn) - 16
   - a = 500 * (f(X/Xn) - f(Y/Yn))
   - b = 200 * (f(Y/Yn) - f(Z/Zn))
   
   其中 f(t) = t^(1/3) if t > 0.008856 else 7.787*t + 16/116

【生成代码】
```csharp
public class ColorConverter
{
    public static (float L, float a, float b) RgbToLab(byte r, byte g, byte b);
    
    public static Mat RgbImageToLab(Mat rgbImage);
}
```

【验证】
- 纯黑(0,0,0) → L=0
- 纯白(255,255,255) → L=100
- 与OpenCV cvtColor结果对比
```

---

### 任务W10-2：DeltaE色差计算（2小时）

```markdown
【任务】实现CIE76/CIE94/CIEDE2000色差公式
【公式】

CIE76（最简单）:
DeltaE = sqrt((L2-L1)² + (a2-a1)² + (b2-b1)²)

CIE94（考虑色度和色调）:
较复杂，先实现简化版

CIEDE2000（最准确，工业标准）:
更复杂，可用现成公式实现

【生成代码】
```csharp
public class ColorDifference
{
    public static double DeltaE76(LabColor c1, LabColor c2);
    public static double DeltaE00(LabColor c1, LabColor c2);  // 重点实现
    
    public static double DeltaC(LabColor c1, LabColor c2);  // 色度差
    public static double DeltaH(LabColor c1, LabColor c2);  // 色调差
}
```

【应用】
- DeltaE < 1: 人眼不可察觉
- DeltaE 1-2: 轻微差异
- DeltaE 2-3.5: 可察觉差异
- DeltaE > 3.5: 明显差异

【测试】
- 标准色差数据集验证
```

---

## 七、Week 11：集成测试与优化（新增）

> **为什么需要集成测试周？** 3D算子是首次实现，需要验证端到端流程和性能

### 任务W11-1：3D处理流程集成测试（3小时）

```markdown
【任务】验证3D点云处理完整流程

【测试场景】
1. 场景1：点云加载 → 下采样 → 统计滤波 → 平面分割
   - 输入：W5-0生成的带噪声平面点云
   - 验证：分割出的平面法向量正确，内点>80%

2. 场景2：点云加载 → 聚类分割 → 保存结果
   - 输入：多个物体的点云
   - 验证：正确分割出各个物体

3. 场景3：模型匹配流程
   - 输入：场景点云 + 模型点云
   - 验证：PPF匹配精度<5mm

【性能验证】
- 使用W0-1性能分析工具
- 验证所有算子满足性能预算
- 记录瓶颈算子

【输出】
- 集成测试报告
- 性能分析报告
- 问题清单
```

### 任务W11-2：纹理+颜色流程集成测试（2小时）

```markdown
【任务】验证纹理和颜色测量流程

【测试场景】
1. 纹理分类：
   - 输入：Brodatz纹理数据集
   - 使用Laws或GLCM特征
   - 验证：不同纹理可区分

2. 颜色测量：
   - 输入：标准色卡
   - 计算Lab值和DeltaE
   - 验证：与标准值误差<2 DeltaE

【输出】
- 测试报告
- 精度分析
```

### 任务W11-3：性能优化（3小时）

```markdown
【任务】优化性能不达标的算子

【优化目标】
- 点云滤波：<200ms（100万点）
- RANSAC分割：<300ms（100万点）
- PPF匹配：<3秒（10000点）
- 纹理分析：<50ms（512x512图）

【优化手段】
1. 使用Profiler定位瓶颈
2. 算法优化（减少不必要计算）
3. 数据结构优化（KDTree代替暴力搜索）
4. 并行化（Parallel.For）

【输出】
- 优化前后性能对比
- 优化代码提交
```

### 任务W11-4：文档完善（2小时）

```markdown
【任务】完善阶段2所有算子文档

【要求】
1. 每个算子有完整的算法说明
2. 使用示例代码
3. 参数说明
4. 性能指标
5. 已知限制

【输出】
- 更新docs/operators/目录
- 更新CATALOG.md
```

---

## 八、性能预算检查清单

> **重要**：每个算子完成后必须验证性能是否满足预算

### 阶段2算子性能预算

| 算子 | 目标延迟 | 内存限制 | 验证方法 |
|------|---------|---------|---------|
| PointCloud加载 | <100ms | 500MB | 100万点PCD文件 |
| 体素下采样 | <150ms | 500MB | 100万点 → 10万点 |
| 统计滤波 | <200ms | 500MB | 100万点 |
| RANSAC平面分割 | <300ms | 500MB | 100万点 |
| 欧氏聚类 | <400ms | 500MB | 10万点 |
| PPF特征计算 | <2秒 | 200MB | 10000点 |
| PPF匹配 | <3秒 | 300MB | 10000点场景 + 5000点模型 |
| Laws纹理滤波 | <30ms | 50MB | 512x512图 |
| GLCM特征 | <50ms | 50MB | 512x512图 |
| CIE Lab转换 | <5ms | 50MB | 512x512图 |
| DeltaE计算 | <1ms | 1MB | 单个颜色对 |

### 性能验证流程

```markdown
【性能验证步骤】
1. 使用W0-1性能分析工具测试
2. 运行100次取平均值
3. 对比性能预算表
4. 如果超标，分析瓶颈并优化
5. 记录到性能报告
```

### 内存监控

**关键点**：
- 点云处理是内存密集型
- 必须监控内存使用
- 超过1.5GB告警

**监控方法**：
```csharp
var initialMemory = GC.GetTotalMemory(true);
// 算子执行
var finalMemory = GC.GetTotalMemory(false);
var memoryUsed = (finalMemory - initialMemory) / 1024 / 1024; // MB
if (memoryUsed > 500)
{
    _logger.LogWarning("内存使用超标: {MemoryMB}MB", memoryUsed);
}
```

---

## 九、错误处理标准

> **重要**：所有算子必须遵循统一的错误处理规范

### 3D算子特定错误处理

```csharp
// 点云算子错误处理示例
protected override Task<OperatorExecutionOutput> ExecuteCoreAsync(...)
{
    // 1. 验证点云输入
    var pointCloud = GetPointCloudInput(inputs, "PointCloud");
    if (pointCloud == null || pointCloud.Count == 0)
    {
        return Task.FromResult(OperatorExecutionOutput.Failure(
            "EmptyPointCloud",
            "输入点云为空或无效",
            "请检查上游算子是否正确输出点云"
        ));
    }

    // 2. 验证点云大小
    if (pointCloud.Count > 10_000_000)
    {
        return Task.FromResult(OperatorExecutionOutput.Failure(
            "PointCloudTooLarge",
            $"点云过大: {pointCloud.Count}点，超过1000万点限制",
            "请先使用体素下采样减少点数"
        ));
    }

    // 3. 验证参数
    var distanceThreshold = GetFloatParam(@operator, "DistanceThreshold", 0.01f);
    if (distanceThreshold <= 0)
    {
        return Task.FromResult(OperatorExecutionOutput.Failure(
            "InvalidParameter",
            $"距离阈值必须>0，当前值: {distanceThreshold}",
            "请设置合理的距离阈值（如0.01）"
        ));
    }

    try
    {
        // 4. 算法执行（CPU密集型，使用RunCpuBoundWork）
        var result = await RunCpuBoundWork(() =>
        {
            return ProcessPointCloud(pointCloud, distanceThreshold);
        }, cancellationToken);

        // 5. 验证输出
        if (result == null || result.Count == 0)
        {
            return OperatorExecutionOutput.Failure(
                "ProcessingFailed",
                "算法执行失败，输出为空",
                "请检查输入点云质量或调整参数"
            );
        }

        return OperatorExecutionOutput.Success(
            CreatePointCloudOutput(result, new Dictionary<string, object>
            {
                { "InputPoints", pointCloud.Count },
                { "OutputPoints", result.Count }
            })
        );
    }
    catch (OutOfMemoryException ex)
    {
        return OperatorExecutionOutput.Failure(
            "OutOfMemory",
            $"内存不足: {ex.Message}",
            "请减小点云大小或增加系统内存"
        );
    }
    catch (Exception ex)
    {
        return OperatorExecutionOutput.Failure(
            "UnexpectedError",
            $"未预期的错误: {ex.Message}",
            "请联系技术支持"
        );
    }
}
```

### 3D算子错误代码

| 错误代码 | 含义 | 恢复建议 |
|---------|------|---------|
| EmptyPointCloud | 点云为空 | 检查上游算子 |
| PointCloudTooLarge | 点云过大 | 使用体素下采样 |
| InsufficientPoints | 点数不足 | 增加点云密度 |
| NoPlaneFound | 未找到平面 | 调整距离阈值 |
| NoClustersFound | 未找到聚类 | 调整聚类参数 |
| MatchingFailed | 匹配失败 | 降低匹配阈值 |
| OutOfMemory | 内存不足 | 减小点云或增加内存 |

---

## 十、阶段2验收标准

### 必完成项

- [x] W5-0：合成点云生成器可用，生成至少4种测试点云
- [x] W6：PointCloud类完整，支持I/O和基础操作
- [x] W6：体素下采样和统计滤波可用，性能满足预算
- [ ] W7：RANSAC平面分割精度<1mm，性能<300ms
- [ ] W7：欧氏聚类可用
- [ ] W8：PPF特征计算正确，匹配精度<5mm
- [ ] W9：Laws或GLCM至少一个纹理算子可用
- [ ] W10：CIE Lab + DeltaE色差计算准确（误差<1%）
- [ ] W11：集成测试通过，所有算子性能满足预算

### 可选增强（时间充裕时）

- [ ] KDTree加速邻域搜索（优化性能）
- [ ] ISS关键点提取（减少PPF计算量）
- [ ] 3D相机SDK对接（如有硬件）
- [ ] ICP精配准（提升匹配精度）

### 质量标准

- ✅ 单元测试覆盖率>80%
- ✅ 所有算子性能满足预算
- ✅ 内存使用<500MB（单个算子）
- ✅ 1000次运行无内存泄漏
- ✅ 错误处理完整
- ✅ 文档完整

---

## 十一、Prompt速查卡

### 3D点云类任务Prompt

```markdown
【任务】实现[点云算子名]
【输入】PointCloud（Nx3点坐标）
【输出】[输出类型]
【算法】[伪代码或参考文献]
【参考实现】PCL [函数名] / Open3D [函数名]
【优化要求】先实现暴力搜索版，KDTree优化后续做
【测试】合成点云验证
```

### 纹理/颜色类任务Prompt

```markdown
【任务】实现[算法名]
【输入】Mat图像
【输出】[特征值/图像]
【公式】[数学公式]
【参考】OpenCV cv2.[函数名] / 论文[标题]
【验证】标准测试图，数值与参考实现误差<1%
```

---

## 十二、本阶段行动项

### 立即开始

**推荐路径**：
1. **第5周优先**：运行W5-0生成合成点云（2小时）
2. **验证工具**：使用Open3D或CloudCompare可视化点云
3. **选择第一个任务**：W6-1 PointCloud数据结构（最基础）
4. **复制Prompt** → AI生成代码
5. **性能验证** → 使用W0-1工具确认满足预算
6. **错误处理** → 按照第九节标准实现
7. **提交代码** → Git commit

### 本阶段目标（第6-11周）

> **W5完成**：合成点云生成器可用，为后续提供测试数据
> **W6-W8完成**：3D点云处理基础能力可用
> **W9-W10完成**：纹理和颜色测量可用
> **W11完成**：集成测试通过，性能达标

### 阶段2总体目标

| 周次 | 核心任务 | 新增/修订内容 | 验收标准 |
|------|---------|--------------|---------|
| W5 | 合成点云生成 | 新增W5-0 | 4种测试点云生成 |
| W6 | 点云基础 | 任务编号调整 | 数据结构+I/O+滤波可用 |
| W7 | 点云分割 | - | RANSAC+聚类可用 |
| W8 | 点云匹配 | FPFH改为PPF | 匹配精度<5mm |
| W9 | 纹理分析 | - | Laws或GLCM可用 |
| W10 | 颜色测量 | - | Lab+DeltaE准确 |
| W11 | 集成测试 | 新增整周 | 所有算子性能达标 |

---

*本文档每周更新，记录实际Prompt效果和踩坑点*

**版本记录**：
- V1.0 (2026-03-16)：初始版本
- V2.1 (2026-03-16)：新增W5合成点云、W6-2改为PPF、新增W11集成测试、增加性能预算和错误处理标准
