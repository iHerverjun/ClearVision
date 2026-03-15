# 阶段2 TODO：专业深化（Vibe Coding版）

> **阶段目标**: 3D+纹理+颜色，形成专业工具链，Halcon 75%水平  
> **时间周期**: 第5-8周  
> **前置条件**: 阶段1里程碑达成（形状匹配精度<0.1px）  
> **核心理念**: 复杂任务拆解，每个子任务<3小时，AI可独立完成

---

## 一、本阶段任务总览

```
Week 5-6: 3D点云基础（6个原子任务）
Week 7:   点云分割与匹配（4个原子任务）
Week 8:   纹理分析 + 颜色测量（4个原子任务）
```

### AI友好度评估

| 任务 | AI友好度 | 难点 | 建议 |
|------|---------|------|------|
| 3D数据结构 | ⭐⭐⭐⭐⭐ | 无 | 直接生成 |
| 点云滤波 | ⭐⭐⭐⭐⭐ | 无 | PCL有参考实现 |
| RANSAC分割 | ⭐⭐⭐⭐☆ | 迭代逻辑 | 伪代码清晰即可 |
| 表面匹配 | ⭐⭐⭐☆☆ | 特征描述子 | 先用FPFH简化版 |
| 纹理分析 | ⭐⭐⭐⭐⭐ | 无 | 数学公式明确 |
| 颜色测量 | ⭐⭐⭐⭐⭐ | 无 | CIE标准公式 |

---

## 二、Week 5-6：3D点云基础

### 任务W5-1：PointCloud数据结构（2小时）

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

【测试】
- 创建1000个随机点云
- 验证AABB计算正确
- 验证Transform后坐标正确
```

---

### 任务W5-2：PCD/PLY文件读写（2小时）

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

---

### 任务W5-3：体素下采样（2小时）

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

【测试】
- 100万点 → 0.01m体素 → 应减少90%以上
- 验证下采样后点云形状保持
```

---

### 任务W5-4：统计滤波（2小时）

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

---

### 任务W6-1：RANSAC平面分割（3小时）

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

【测试】
- 合成点云：平面 + 噪声
- 验证分割出的平面法向量正确
- 验证内点比例>80%
```

---

### 任务W6-2：FPFH特征描述子（4小时）

```markdown
【任务】实现FPFH（Fast Point Feature Histograms）特征
【参考】Rusu et al. "Fast Point Feature Histograms (FPFH) for 3D Registration"
【简化版实现】

步骤1: 计算每个点的法线（PCA分析邻域）
步骤2: 计算SPFH（Simple Point Feature Histograms）
步骤3: 加权邻域SPFH得到FPFH

【生成代码】
```csharp
public class FPFHFeature
{
    public float[] Histogram { get; }  // 33维直方图
}

public class FPFHEstimation
{
    public FPFHFeature[] Compute(
        PointCloud cloud,
        float normalRadius = 0.03f,   // 法线估计半径
        float featureRadius = 0.05f   // 特征估计半径
    )
    {
        // 1. 计算法线
        var normals = EstimateNormals(cloud, normalRadius);
        
        // 2. 计算FPFH
        var features = new FPFHFeature[cloud.Count];
        for each point:
            features[i] = ComputeFPFH(cloud, normals, i, featureRadius);
        
        return features;
    }
}
```

【注意】
- 这是简化版，完整FPFH较复杂
- 可用ISS关键点+FPFH减少计算量
- 测试时先用小点云（<10000点）

【测试】
- 同一点云不同视角的特征匹配
- 验证对应点特征相似度高
```

---

## 三、Week 7：点云分割与匹配

### 任务W7-1：欧氏聚类分割（2小时）

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

### 任务W7-2：基于FPFH的表面匹配（4小时）

```markdown
【任务】实现3D表面匹配（简化版）
【算法流程】
1. 场景和模型分别提取ISS关键点 + FPFH特征
2. 特征匹配（最近邻搜索）
3. RANSAC验证几何一致性
4. 返回6DOF位姿

【简化策略】
- 先不做关键点提取，用全部点（点云<5000时可行）
- 特征匹配用暴力搜索（后期优化用KDTree）
- RANSAC位姿估计用3点法

【生成代码框架】
```csharp
public class SurfaceMatcher
{
    public Pose3D Match(
        PointCloud scene,
        PointCloud model,
        float featureRadius = 0.05f
    )
    {
        // 1. 计算FPFH特征
        var sceneFeatures = FPFHEstimation.Compute(scene, featureRadius);
        var modelFeatures = FPFHEstimation.Compute(model, featureRadius);
        
        // 2. 特征匹配，找对应点
        var correspondences = MatchFeatures(sceneFeatures, modelFeatures);
        
        // 3. RANSAC验证，估计位姿
        var pose = RansacPoseEstimation(scene, model, correspondences);
        
        return pose;
    }
}
```

【测试数据】
- 下载LineMOD数据集或合成数据
- 测试匹配精度和召回率

【评估指标】
- 精度：匹配位姿与真值误差<5mm/<5度
- 召回率：>80%
```

---

## 四、Week 8：纹理分析 + 颜色测量

### 任务W8-1：Laws纹理滤波（2小时）

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

### 任务W8-2：GLCM纹理特征（3小时）

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

### 任务W8-3：CIE Lab颜色转换（2小时）

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

### 任务W8-4：DeltaE色差计算（2小时）

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

## 五、阶段2验收标准

### 必完成项

- [ ] PointCloud类完整，支持I/O和基础操作
- [ ] 体素下采样和统计滤波可用
- [ ] RANSAC平面分割精度<1mm
- [ ] Laws或GLCM至少一个纹理算子可用
- [ ] CIE Lab + DeltaE色差计算准确

### 可选增强（时间充裕时）

- [ ] FPFH特征完整实现
- [ ] 表面匹配可用（精度<5mm即可）
- [ ] 3D相机SDK对接（如有硬件）

---

## 六、Prompt速查卡

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

*本阶段完成后，ClearVision将具备基础3D处理能力*
