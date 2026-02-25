// AIWorkflowService.cs
// AI 编排接入服务 - Sprint 5
// 整合 PromptBuilder、FlowParser、Linter、Dry-Run 的完整工作流
// 作者：蘅芜君

using System.Diagnostics;
using Acme.Product.Core.Entities;
using Acme.Product.Infrastructure.AI.DryRun;
using Acme.Product.Infrastructure.Services;
using Microsoft.Extensions.Logging;

namespace Acme.Product.Infrastructure.AI;

/// <summary>
/// AI 编排服务工作流
/// 提供从自然语言需求到可执行流程的完整转换管道
/// </summary>
public class AIWorkflowService
{
    private readonly PromptBuilder _promptBuilder;
    private readonly AIGeneratedFlowParser _flowParser;
    private readonly FlowLinter _linter;
    private readonly DryRunService _dryRunService;
    private readonly ILLMConnector _llmConnector;
    private readonly IPromptVersionManager _promptVersionManager;
    private readonly IAIGeneratedFlowVersionManager _flowVersionManager;
    private readonly Microsoft.Extensions.Logging.ILogger<AIWorkflowService> _logger;

    public AIWorkflowService(
        PromptBuilder promptBuilder,
        AIGeneratedFlowParser flowParser,
        FlowLinter linter,
        DryRunService dryRunService,
        ILLMConnector llmConnector,
        IPromptVersionManager promptVersionManager,
        IAIGeneratedFlowVersionManager flowVersionManager,
        Microsoft.Extensions.Logging.ILogger<AIWorkflowService> logger)
    {
        _promptBuilder = promptBuilder;
        _flowParser = flowParser;
        _linter = linter;
        _dryRunService = dryRunService;
        _llmConnector = llmConnector;
        _promptVersionManager = promptVersionManager;
        _flowVersionManager = flowVersionManager;
        _logger = logger;
    }

    /// <summary>
    /// 执行完整的 AI 工作流
    /// </summary>
    public async Task<AIWorkflowResult> GenerateFlowAsync(
        string userRequirement,
        AIWorkflowOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new AIWorkflowOptions();
        var stopwatch = Stopwatch.StartNew();
        var telemetry = new WorkflowTelemetry();
        PromptVersion? activeVersion = null;

        try
        {
            _logger.LogInformation("开始 AI 工作流，用户需求: {Requirement}", userRequirement);

            // Step 1: 构建提示词
            var promptStart = stopwatch.ElapsedMilliseconds;
            activeVersion = await _promptVersionManager.GetActiveVersionAsync();
            var prompt = _promptBuilder.BuildSystemPrompt(userRequirement);
            telemetry.PromptBuildTimeMs = stopwatch.ElapsedMilliseconds - promptStart;
            _logger.LogDebug("提示词构建完成，耗时 {TimeMs}ms", telemetry.PromptBuildTimeMs);

            // Step 2: 调用 LLM
            var llmStart = stopwatch.ElapsedMilliseconds;
            var llmResponse = await _llmConnector.GenerateAsync(prompt, cancellationToken);
            telemetry.LLMCallTimeMs = stopwatch.ElapsedMilliseconds - llmStart;
            telemetry.LLMTokenUsage = llmResponse.TokenUsage;
            _logger.LogDebug("LLM 调用完成，耗时 {TimeMs}ms", telemetry.LLMCallTimeMs);

            if (activeVersion != null)
            {
                await _promptVersionManager.RecordMetricsAsync(activeVersion.Id, true, telemetry.LLMTokenUsage, telemetry.LLMCallTimeMs);
            }

            // Step 3: 解析生成的流程
            var parseStart = stopwatch.ElapsedMilliseconds;
            var parseResult = _flowParser.Parse(llmResponse.Content);
            telemetry.ParseTimeMs = stopwatch.ElapsedMilliseconds - parseStart;
            _logger.LogDebug("流程解析完成，耗时 {TimeMs}ms", telemetry.ParseTimeMs);

            if (!parseResult.IsSuccessful)
            {
                _logger.LogWarning("流程解析失败: {Error}", parseResult.ErrorMessage);
                return AIWorkflowResult.Failure(parseResult.ErrorMessage, telemetry);
            }

            var flow = parseResult.Flow!;
            var warnings = parseResult.Warnings;

            // Step 4: Linter 验证（已在 parser 中运行，获取更详细结果）
            var lintStart = stopwatch.ElapsedMilliseconds;
            var lintResult = _linter.Lint(flow);
            telemetry.LintTimeMs = stopwatch.ElapsedMilliseconds - lintStart;

            if (lintResult.HasErrors && options.StrictMode)
            {
                _logger.LogWarning("流程存在错误且启用了严格模式");
                return AIWorkflowResult.Failure(
                    $"生成的流程存在错误:\n{string.Join("\n", lintResult.Issues.Where(i => i.Severity == LintSeverity.Error).Select(e => $"- [{e.Code}] {e.Message}"))}",
                    telemetry,
                    flow
                );
            }

            // Step 5: 双向 Dry-Run（如启用）
            DryRunResult? dryRunResult = null;
            if (options.EnableDryRun)
            {
                var dryRunStart = stopwatch.ElapsedMilliseconds;

                // 构建 Stub 注册表
                var stubBuilder = new StubRegistryBuilder(new DryRunStubRegistry());
                var operatorTypes = flow.Operators.Select(o => o.Type).ToList();
                var stubRegistry = stubBuilder.BuildForFlow(operatorTypes);

                // 执行 Dry-Run
                var testInputs = new Dictionary<string, object> { ["Image"] = "TestImage" };
                dryRunResult = await _dryRunService.RunAsync(flow, testInputs, stubRegistry, cancellationToken);

                telemetry.DryRunTimeMs = stopwatch.ElapsedMilliseconds - dryRunStart;
                _logger.LogDebug("Dry-Run 完成，耗时 {TimeMs}ms", telemetry.DryRunTimeMs);

                if (!dryRunResult.IsSuccess)
                {
                    _logger.LogWarning("Dry-Run 失败");
                    return AIWorkflowResult.Failure(
                        "Dry-Run 验证失败",
                        telemetry,
                        flow
                    );
                }
            }

            telemetry.TotalTimeMs = stopwatch.ElapsedMilliseconds;
            _logger.LogInformation("AI 工作流完成，总耗时 {TotalMs}ms", telemetry.TotalTimeMs);

            if (activeVersion != null)
            {
                var promptInfo = new PromptVersionInfo
                {
                    VersionId = activeVersion.Id,
                    Name = activeVersion.Name
                };

                await _flowVersionManager.SaveVersionAsync(
                    flow,
                    userRequirement,
                    promptInfo,
                    llmResponse.Provider ?? "Auto",
                    telemetry
                );
            }

            return AIWorkflowResult.Success(
                flow,
                telemetry,
                warnings,
                lintResult.Issues.Where(i => i.Severity == LintSeverity.Warning).ToList(),
                dryRunResult
            );
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("AI 工作流已取消");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AI 工作流执行失败");
            telemetry.TotalTimeMs = stopwatch.ElapsedMilliseconds;

            if (activeVersion != null)
            {
                long latencyToRecord = telemetry.LLMCallTimeMs > 0 ? telemetry.LLMCallTimeMs : stopwatch.ElapsedMilliseconds;
                await _promptVersionManager.RecordMetricsAsync(activeVersion.Id, false, telemetry.LLMTokenUsage, latencyToRecord);
            }

            return AIWorkflowResult.Failure($"工作流执行失败: {ex.Message}", telemetry);
        }
    }

    /// <summary>
    /// 快速验证流程（无需 LLM，用于测试）
    /// </summary>
    public AIWorkflowResult ValidateFlow(OperatorFlow flow, bool enableDryRun = true)
    {
        var stopwatch = Stopwatch.StartNew();
        var telemetry = new WorkflowTelemetry();

        // Linter 验证
        var lintResult = _linter.Lint(flow);
        telemetry.LintTimeMs = stopwatch.ElapsedMilliseconds;

        if (lintResult.HasErrors)
        {
            return AIWorkflowResult.Failure(
                $"流程验证失败:\n{string.Join("\n", lintResult.Issues.Where(i => i.Severity == LintSeverity.Error).Select(e => e.Message))}",
                telemetry,
                flow
            );
        }

        // Dry-Run（同步执行）
        DryRunResult? dryRunResult = null;
        if (enableDryRun)
        {
            var stubBuilder = new StubRegistryBuilder(new DryRunStubRegistry());
            var operatorTypes = flow.Operators.Select(o => o.Type).ToList();
            var stubRegistry = stubBuilder.BuildForFlow(operatorTypes);
            var testInputs = new Dictionary<string, object> { ["Image"] = "TestImage" };

            dryRunResult = _dryRunService.RunAsync(flow, testInputs, stubRegistry).GetAwaiter().GetResult();
            telemetry.DryRunTimeMs = stopwatch.ElapsedMilliseconds - telemetry.LintTimeMs;

            if (!dryRunResult.IsSuccess)
            {
                return AIWorkflowResult.Failure(
                    "Dry-Run 验证失败",
                    telemetry,
                    flow
                );
            }
        }

        telemetry.TotalTimeMs = stopwatch.ElapsedMilliseconds;
        return AIWorkflowResult.Success(flow, telemetry, new List<LintIssue>(), lintResult.Issues.Where(i => i.Severity == LintSeverity.Warning).ToList(), dryRunResult);
    }
}

/// <summary>
/// AI 工作流结果
/// </summary>
public class AIWorkflowResult
{
    public bool IsSuccessful { get; }
    public OperatorFlow? Flow { get; }
    public string ErrorMessage { get; }
    public WorkflowTelemetry Telemetry { get; }
    public List<LintIssue> ParseWarnings { get; }
    public List<LintIssue> LintWarnings { get; }
    public DryRunResult? DryRunResult { get; }

    private AIWorkflowResult(
        bool success,
        OperatorFlow? flow,
        string errorMessage,
        WorkflowTelemetry telemetry,
        List<LintIssue> parseWarnings,
        List<LintIssue> lintWarnings,
        DryRunResult? dryRunResult)
    {
        IsSuccessful = success;
        Flow = flow;
        ErrorMessage = errorMessage;
        Telemetry = telemetry;
        ParseWarnings = parseWarnings;
        LintWarnings = lintWarnings;
        DryRunResult = dryRunResult;
    }

    public static AIWorkflowResult Success(
        OperatorFlow flow,
        WorkflowTelemetry telemetry,
        List<LintIssue> parseWarnings,
        List<LintIssue> lintWarnings,
        DryRunResult? dryRunResult)
    {
        return new AIWorkflowResult(true, flow, string.Empty, telemetry, parseWarnings, lintWarnings, dryRunResult);
    }

    public static AIWorkflowResult Failure(string errorMessage, WorkflowTelemetry telemetry, OperatorFlow? partialFlow = null)
    {
        return new AIWorkflowResult(false, partialFlow, errorMessage, telemetry, new List<LintIssue>(), new List<LintIssue>(), null);
    }
}

/// <summary>
/// 工作流遥测数据
/// </summary>
public class WorkflowTelemetry
{
    public long PromptBuildTimeMs { get; set; }
    public long LLMCallTimeMs { get; set; }
    public long ParseTimeMs { get; set; }
    public long LintTimeMs { get; set; }
    public long DryRunTimeMs { get; set; }
    public long TotalTimeMs { get; set; }
    public int LLMTokenUsage { get; set; }
}

/// <summary>
/// AI 工作流选项
/// </summary>
public class AIWorkflowOptions
{
    /// <summary>
    /// 启用严格模式（Linter 错误将导致失败）
    /// </summary>
    public bool StrictMode { get; set; } = true;

    /// <summary>
    /// 启用 Dry-Run 验证
    /// </summary>
    public bool EnableDryRun { get; set; } = true;

    /// <summary>
    /// 最大重试次数
    /// </summary>
    public int MaxRetries { get; set; } = 2;
}

/// <summary>
/// LLM 连接器接口
/// </summary>
public interface ILLMConnector
{
    Task<LLMResponse> GenerateAsync(string prompt, CancellationToken cancellationToken = default);
    System.Collections.Generic.IAsyncEnumerable<LLMStreamChunk> GenerateStreamAsync(string prompt, CancellationToken cancellationToken = default);
}

/// <summary>
/// LLM 响应
/// </summary>
public class LLMResponse
{
    /// <summary>响应内容</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>Token 使用量</summary>
    public int TokenUsage { get; set; }

    /// <summary>使用的模型</summary>
    public string Model { get; set; } = string.Empty;

    /// <summary>LLM 提供商</summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>完成原因</summary>
    public string FinishReason { get; set; } = string.Empty;

    /// <summary>延迟（毫秒）</summary>
    public long LatencyMs { get; set; }
}

/// <summary>
/// 简单日志接口
/// </summary>
public interface ILogger<T>
{
    void LogInformation(string message, params object[] args);
    void LogWarning(string message, params object[] args);
    void LogError(Exception? exception, string message, params object[] args);
    void LogDebug(string message, params object[] args);
}

/// <summary>
/// 简单日志实现
/// </summary>
public class SimpleLogger<T> : ILogger<T>
{
    public void LogInformation(string message, params object[] args)
    {
        Console.WriteLine($"[INFO] {string.Format(message, args)}");
    }

    public void LogWarning(string message, params object[] args)
    {
        Console.WriteLine($"[WARN] {string.Format(message, args)}");
    }

    public void LogError(Exception? exception, string message, params object[] args)
    {
        Console.WriteLine($"[ERROR] {string.Format(message, args)}");
        if (exception != null)
        {
            Console.WriteLine($"       {exception.Message}");
        }
    }

    public void LogDebug(string message, params object[] args)
    {
        // Debug 日志默认不输出
    }
}
