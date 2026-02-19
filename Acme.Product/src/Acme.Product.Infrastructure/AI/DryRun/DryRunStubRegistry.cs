// DryRunStubRegistry.cs
// 仿真数据挡板注册表 - Sprint 4 Task 4.2
// 支持双向仿真，可预设多种设备响应场景
// 作者：蘅芜君

namespace Acme.Product.Infrastructure.AI.DryRun;

/// <summary>
/// 仿真数据挡板注册表。
/// 允许为特定的通信目标（设备地址 + 数据地址）预设返回报文，
/// 使离线仿真能够模拟不同的设备响应场景（正常、超时、错误、特定状态字），
/// 从而激活 DAG 中的各条件分支，验证 AI 生成的完整逻辑树。
/// </summary>
public class DryRunStubRegistry
{
    // key = StubKey（设备地址 + 目标地址），value = 有序的响应序列
    private readonly Dictionary<StubKey, Queue<StubResponse>> _stubs = new();

    /// <summary>
    /// 注册一个数据挡板。
    /// 支持响应序列（第 1 次调用返回 response[0]，第 2 次返回 response[1]...）
    /// 序列耗尽后循环使用最后一个响应，模拟稳定状态。
    /// </summary>
    /// <param name="deviceAddress">设备地址，如 "192.168.1.10:502" 或 "https://mes.factory.com"</param>
    /// <param name="targetAddress">目标地址，如 "40001"（Modbus寄存器）或 "/api/quality/check"</param>
    /// <param name="responses">预设响应序列</param>
    public DryRunStubRegistry Register(
        string deviceAddress,
        string targetAddress,
        params StubResponse[] responses)
    {
        var key = new StubKey(deviceAddress, targetAddress);
        var queue = new Queue<StubResponse>(responses);
        _stubs[key] = queue;
        return this;
    }

    /// <summary>
    /// 获取下一个预设响应。
    /// 如果没有为此目标注册挡板，返回默认的成功响应（向后兼容 V3 行为）。
    /// </summary>
    public StubResponse GetNextResponse(string deviceAddress, string targetAddress)
    {
        var key = new StubKey(deviceAddress, targetAddress);
        if (_stubs.TryGetValue(key, out var queue) && queue.Count > 0)
        {
            var response = queue.Dequeue();
            // 循环：将用过的响应追回队尾（模拟稳定状态）
            queue.Enqueue(response);
            return response;
        }
        // 无挡板注册：返回默认成功（兼容不需要双向验证的简单场景）
        return StubResponse.DefaultSuccess;
    }

    /// <summary>
    /// 检查是否为已注册的挡板目标
    /// </summary>
    public bool HasStub(string deviceAddress, string targetAddress)
    {
        var key = new StubKey(deviceAddress, targetAddress);
        return _stubs.ContainsKey(key);
    }

    /// <summary>
    /// 获取所有已注册的挡板键
    /// </summary>
    public IEnumerable<string> GetRegisteredTargets()
    {
        return _stubs.Keys.Select(k => $"{k.DeviceAddress}/{k.TargetAddress}");
    }

    /// <summary>
    /// 清除所有挡板
    /// </summary>
    public void Clear()
    {
        _stubs.Clear();
    }

    private record StubKey(string DeviceAddress, string TargetAddress);
}

/// <summary>
/// 仿真响应定义
/// </summary>
public record StubResponse(
    bool IsSuccess,
    string Payload,
    int DelayMs = 0,
    string? ErrorMessage = null)
{
    /// <summary>
    /// 默认成功响应
    /// </summary>
    public static StubResponse DefaultSuccess =>
        new(true, "{\"status\":\"OK\"}", DelayMs: 5);

    /// <summary>
    /// 超时响应
    /// </summary>
    public static StubResponse Timeout =>
        new(false, "", DelayMs: 30000, ErrorMessage: "Connection timed out");

    /// <summary>
    /// 错误响应
    /// </summary>
    public static StubResponse Error(string message) =>
        new(false, "", ErrorMessage: message);

    /// <summary>
    /// Modbus 寄存器响应（Hex 字符串）
    /// </summary>
    public static StubResponse ModbusResponse(string hexValue, int delayMs = 5) =>
        new(true, hexValue, DelayMs: delayMs);

    /// <summary>
    /// HTTP JSON 响应
    /// </summary>
    public static StubResponse JsonResponse(object data, int delayMs = 5) =>
        new(true, System.Text.Json.JsonSerializer.Serialize(data), DelayMs: delayMs);
}
