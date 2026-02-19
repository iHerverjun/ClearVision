// DryRunService.cs
// 仿真执行服务 - Sprint 4 Task 4.2
// 支持双向仿真，可注入 Stub 响应
// 作者：蘅芜君

using Acme.Product.Core.Entities;
using Acme.Product.Core.Services;

namespace Acme.Product.Infrastructure.AI.DryRun;

/// <summary>
/// 仿真执行服务
/// 在 DryRun 模式下执行流程，使用 StubRegistry 模拟外部设备响应
/// </summary>
public class DryRunService
{
    private readonly IFlowExecutionService _flowExecutionService;

    public DryRunService(IFlowExecutionService flowExecutionService)
    {
        _flowExecutionService = flowExecutionService;
    }

    /// <summary>
    /// 执行仿真运行
    /// </summary>
    /// <param name="flow">要仿真的流程</param>
    /// <param name="testInputs">测试输入数据（图像等）</param>
    /// <param name="stubRegistry">数据挡板注册表</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>仿真结果，包含分支覆盖率信息</returns>
    public async Task<DryRunResult> RunAsync(
        OperatorFlow flow,
        Dictionary<string, object> testInputs,
        DryRunStubRegistry stubRegistry,
        CancellationToken cancellationToken = default)
    {
        var result = new DryRunResult
        {
            StartTime = DateTime.UtcNow,
            FlowId = flow.Id,
            FlowName = flow.Name
        };

        // 设置全局 DryRun 上下文
        DryRunContext.Current = new DryRunContext
        {
            IsDryRun = true,
            StubRegistry = stubRegistry,
            BranchExecutionCounts = new Dictionary<string, int>()
        };

        try
        {
            // 执行流程
            var flowResult = await _flowExecutionService.ExecuteFlowAsync(
                flow,
                testInputs,
                enableParallel: false,
                cancellationToken);

            result.FlowResult = flowResult;
            result.IsSuccess = flowResult.IsSuccess;
            
            // 收集分支覆盖信息
            result.BranchExecutionCounts = DryRunContext.Current.BranchExecutionCounts;
            result.TotalBranches = result.BranchExecutionCounts.Count;
            result.CoveredBranches = result.BranchExecutionCounts.Count(x => x.Value > 0);
            result.CoveragePercentage = result.TotalBranches > 0
                ? (double)result.CoveredBranches / result.TotalBranches * 100
                : 100;
        }
        finally
        {
            DryRunContext.Current = null;
        }

        result.EndTime = DateTime.UtcNow;
        result.DurationMs = (result.EndTime - result.StartTime).TotalMilliseconds;

        return result;
    }

    /// <summary>
    /// 批量执行多组测试用例
    /// </summary>
    public async Task<DryRunBatchResult> RunBatchAsync(
        OperatorFlow flow,
        List<Dictionary<string, object>> testCases,
        DryRunStubRegistry stubRegistry,
        CancellationToken cancellationToken = default)
    {
        var results = new List<DryRunResult>();
        var allBranches = new HashSet<string>();

        foreach (var testCase in testCases)
        {
            var result = await RunAsync(flow, testCase, stubRegistry, cancellationToken);
            results.Add(result);
            
            foreach (var branch in result.BranchExecutionCounts.Keys)
            {
                allBranches.Add(branch);
            }
        }

        // 汇总覆盖率
        var totalExecutions = new Dictionary<string, int>();
        foreach (var branch in allBranches)
        {
            totalExecutions[branch] = results.Sum(r => r.BranchExecutionCounts.GetValueOrDefault(branch, 0));
        }

        return new DryRunBatchResult
        {
            TotalTestCases = testCases.Count,
            Results = results,
            CombinedBranchExecutionCounts = totalExecutions,
            TotalBranches = allBranches.Count,
            CoveredBranches = totalExecutions.Count(x => x.Value > 0),
            CoveragePercentage = allBranches.Count > 0
                ? (double)totalExecutions.Count(x => x.Value > 0) / allBranches.Count * 100
                : 100
        };
    }
}

/// <summary>
/// DryRun 全局上下文
/// </summary>
public class DryRunContext
{
    [ThreadStatic]
    private static DryRunContext? _current;

    public static DryRunContext? Current
    {
        get => _current;
        set => _current = value;
    }

    public bool IsDryRun { get; set; }
    public DryRunStubRegistry? StubRegistry { get; set; }
    public Dictionary<string, int> BranchExecutionCounts { get; set; } = new();

    /// <summary>
    /// 记录分支执行
    /// </summary>
    public void RecordBranchExecution(string branchId)
    {
        if (BranchExecutionCounts.ContainsKey(branchId))
            BranchExecutionCounts[branchId]++;
        else
            BranchExecutionCounts[branchId] = 1;
    }
}

/// <summary>
/// 单次仿真结果
/// </summary>
public class DryRunResult
{
    public Guid FlowId { get; set; }
    public string FlowName { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public double DurationMs { get; set; }
    public bool IsSuccess { get; set; }
    public FlowExecutionResult? FlowResult { get; set; }
    public Dictionary<string, int> BranchExecutionCounts { get; set; } = new();
    public int TotalBranches { get; set; }
    public int CoveredBranches { get; set; }
    public double CoveragePercentage { get; set; }
}

/// <summary>
/// 批量仿真结果
/// </summary>
public class DryRunBatchResult
{
    public int TotalTestCases { get; set; }
    public List<DryRunResult> Results { get; set; } = new();
    public Dictionary<string, int> CombinedBranchExecutionCounts { get; set; } = new();
    public int TotalBranches { get; set; }
    public int CoveredBranches { get; set; }
    public double CoveragePercentage { get; set; }

    /// <summary>
    /// 生成覆盖率报告
    /// </summary>
    public string GenerateReport()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"=== DryRun 批量仿真报告 ===");
        sb.AppendLine($"测试用例数: {TotalTestCases}");
        sb.AppendLine($"分支覆盖率: {CoveragePercentage:F1}% ({CoveredBranches}/{TotalBranches})");
        sb.AppendLine();
        sb.AppendLine("分支执行情况:");
        foreach (var (branch, count) in CombinedBranchExecutionCounts.OrderBy(x => x.Key))
        {
            var status = count > 0 ? "✅" : "❌";
            sb.AppendLine($"  {status} {branch}: {count} 次");
        }
        return sb.ToString();
    }
}
