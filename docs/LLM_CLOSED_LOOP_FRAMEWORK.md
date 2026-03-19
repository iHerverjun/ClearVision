# Phase 4: LLM 闭环验证框架

**日期**: 2026-03-18  
**状态**: 设计完成，待实施  

---

## 架构设计

```
用户上传图片
    │
    ▼
┌─────────────────┐
│  AutoTuneService │  ← 编排迭代循环
└────────┬────────┘
         │
    ┌────┴────┐
    ▼         ▼
Preview   Metrics   ← 执行+分析
    │         │
    └────┬────┘
         ▼
   LLM Feedback    ← 生成下一轮参数
         │
    ┌────┴────┐
    ▼         ▼
Success   MaxIter? → Done
```

---

## 核心组件

### 1. AutoTuneService

```csharp
public interface IAutoTuneService
{
    Task<AutoTuneResult> AutoTuneOperatorAsync(
        OperatorType type,
        byte[] inputImage,
        Dictionary<string, object> initialParams,
        AutoTuneGoal goal,
        int maxIterations = 5,
        CancellationToken ct = default);
}
```

### 2. PreviewMetricsAnalyzer

```csharp
public class PreviewMetricsAnalyzer
{
    public PreviewMetrics Analyze(Mat image, OperatorResult result)
    {
        return new PreviewMetrics
        {
            // 图像统计
            ImageStats = new ImageStats { ... },
            
            // Blob 统计
            BlobStats = result.Blobs?.Select(...).ToList(),
            
            // 诊断标签
            Diagnostics = GenerateDiagnostics(image, result),
            
            // 可优化目标
            OptimizationGoals = CalculateGoals(image, result)
        };
    }
}
```

### 3. 反馈数据结构

```csharp
public record PreviewFeedback
{
    public ImageStats ImageStats { get; init; } = null!;
    public List<BlobStat> BlobStats { get; init; } = new();
    public List<string> Diagnostics { get; init; } = new();
    public List<OptimizationGoal> Goals { get; init; } = new();
    public double OverallScore { get; init; }
}
```

---

## API 设计

```
POST /api/operators/{type}/auto-tune
Request:
{
    "inputImageBase64": "...",
    "initialParameters": { ... },
    "goal": {
        "targetBlobCount": 2,
        "tolerance": 0.1
    },
    "maxIterations": 5
}

Response:
{
    "success": true,
    "finalParameters": { ... },
    "iterations": [
        {
            "iteration": 1,
            "parameters": { ... },
            "metrics": { ... },
            "score": 0.7
        }
    ],
    "finalScore": 0.95
}
```

---

## 科学实验设计

### 数据集
- 包装带检测场景 70 张
- 开发集 30 张，验证集 **40 张**（满足统计检验力）

### 基线对比
1. 人工调参（工程师 5 年经验）
2. 现有 ParameterRecommender
3. LLM + 闭环反馈（待验证）

### 评估指标
- 准确率：BlobCount 误差 < 10%
- 调参耗时：从上传到可用工作流
- 迭代次数：达到目标的预览调用次数

### 验收阈值
1. 验证集准确率 ≥ 80%（vs 人工 ≥ 90%）
2. 平均调参耗时 ≤ 3 分钟
3. 相比无 LLM 基线，准确率提升 ≥ 15%
4. McNemar 检验 p < 0.05

---

## 排期

| 任务 | 时间 |
|------|------|
| PreviewMetricsAnalyzer | 3 天 |
| AutoTuneService | 3 天 |
| API 端点 | 2 天 |
| 实验验证 | 5 天 |

**总计**: ~13 工作日

---

## 风险

| 风险 | 概率 | 缓解 |
|------|------|------|
| LLM 调参收敛失败 | 中 | 设置 maxIterations 上限，人工兜底 |
| 反馈信息量过大 | 低 | 分级反馈：基础/详细/完整 |
| 计算成本过高 | 低 | 迭代次数限制 + 提前终止 |
