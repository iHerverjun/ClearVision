// OmronFinsCommunicationOperator.cs
// 解析写入值：优先从上游输入获取，否则使用参数面板静态值
// 作者：蘅芜君

using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Acme.PlcComm;
using Acme.PlcComm.Interfaces;
using Microsoft.Extensions.Logging;

using Acme.Product.Core.Attributes;
namespace Acme.Product.Infrastructure.Operators;

/// <summary>
/// 欧姆龙FINS通信算子
/// 支持FINS/TCP协议，适用于CP1H/CJ2M/NJ/NX系列PLC
/// </summary>
[OperatorMeta(
    DisplayName = "欧姆龙FINS通信",
    Description = "欧姆龙FINS/TCP协议PLC读写通信（CP1H/CJ2M/NJ/NX）",
    Category = "通信",
    IconName = "fins"
)]
[InputPort("Data", "数据", PortDataType.Any, IsRequired = false)]
[OutputPort("Response", "响应", PortDataType.String)]
[OutputPort("Status", "状态", PortDataType.Boolean)]
[OperatorParam("IpAddress", "IP地址", "string", DefaultValue = "192.168.250.1")]
[OperatorParam("Port", "端口", "int", DefaultValue = 9600, Min = 1, Max = 65535)]
[OperatorParam("Address", "PLC地址", "string", DefaultValue = "DM100")]
[OperatorParam("Length", "读取长度", "int", DefaultValue = 1, Min = 1, Max = 999)]
[OperatorParam("DataType", "数据类型", "enum", DefaultValue = "Word", Options = new[] { "Bit|位 (Bool)", "Word|字 (Word/UInt16)", "Int16|短整型 (Int16)", "DWord|双字 (DWord/UInt32)", "Int32|整型 (Int32)", "Float|浮点 (Float)", "Double|双精度 (Double)" })]
[OperatorParam("Operation", "操作", "enum", DefaultValue = "Read", Options = new[] { "Read|读取", "Write|写入" })]
[OperatorParam("WriteValue", "写入值", "string", DefaultValue = "")]
[OperatorParam("PollingMode", "轮询模式", "enum", Description = "读取时是否启用轮询等待", DefaultValue = "None", Options = new[] { "None|不等待", "WaitForValue|等待指定值" })]
[OperatorParam("PollingCondition", "等待条件", "enum", Description = "等待的条件类型", DefaultValue = "Equal", Options = new[] { "Equal|等于", "NotEqual|不等于", "GreaterThan|大于", "LessThan|小于", "GreaterOrEqual|大于等于", "LessOrEqual|小于等于" })]
[OperatorParam("PollingValue", "等待值", "string", Description = "等待的目标值（如触发信号值）", DefaultValue = "1")]
[OperatorParam("PollingTimeout", "等待超时(ms)", "int", Description = "最长等待时间（毫秒）", DefaultValue = 30000, Min = 100, Max = 300000)]
[OperatorParam("PollingInterval", "轮询间隔(ms)", "int", Description = "每次读取间隔（毫秒）", DefaultValue = 50, Min = 10, Max = 5000)]
public class OmronFinsCommunicationOperator : PlcCommunicationOperatorBase
{
    public override OperatorType OperatorType => OperatorType.OmronFinsCommunication;

    public OmronFinsCommunicationOperator(ILogger<OmronFinsCommunicationOperator> logger) : base(logger) { }

    protected override async Task<OperatorExecutionOutput> ExecuteCoreAsync(
        Operator @operator,
        Dictionary<string, object>? inputs,
        CancellationToken cancellationToken)
    {
        // 获取参数
        var operatorIpAddress = GetStringParam(@operator, "IpAddress", "");
        var operatorPort = GetIntParam(@operator, "Port", 0);
        var (ipAddress, port, _) = ResolveConnectionSettings(operatorIpAddress, operatorPort, "FINS");
        var address = GetStringParam(@operator, "Address", "DM100");
        var length = GetIntParam(@operator, "Length", 1, 1, 999);
        var dataType = GetStringParam(@operator, "DataType", "Word");
        var operation = GetStringParam(@operator, "Operation", "Read");
        
        // 【增强】支持从上游输入动态获取写入值
        var writeValue = ResolveWriteValue(@operator, inputs);

        // 构建连接键
        var connectionKey = $"FINS:{ipAddress}:{port}";

        try
        {
            // 获取或创建连接
            var (client, isNew) = await GetOrCreateConnectionAsync(connectionKey, () =>
            {
                var finsClient = PlcClientFactory.CreateOmronFins(ipAddress);
                ((Acme.PlcComm.Omron.OmronFinsClient)finsClient).Port = port;
                return finsClient;
            });

            if (operation.Equals("Read", StringComparison.OrdinalIgnoreCase))
            {
                return await ExecuteReadAsync(client, address, dataType, (ushort)length, cancellationToken);
            }
            else
            {
                return await ExecuteWriteAsync(client, address, dataType, writeValue, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "[OmronFINS] 通信错误: {IP}:{Port} - {Message}", ipAddress, port, ex.Message);
            return CreateFailureOutput($"FINS通信错误: {ex.Message}");
        }
    }

    private async Task<OperatorExecutionOutput> ExecuteReadAsync(
        IPlcClient client, string address, string dataType, ushort length, CancellationToken ct)
    {
        var result = await client.ReadAsync(address, length, ct);

        if (!result.IsSuccess)
            return CreateFailureOutput($"读取失败: {result.Message}");

        var value = ConvertBytesToValue(client, result.Content!, dataType);
        Logger.LogInformation("[OmronFINS] 读取成功: {Address} = {Value}", address, value);
        return CreateSuccessOutput(value, dataType);
    }

    private async Task<OperatorExecutionOutput> ExecuteWriteAsync(
        IPlcClient client, string address, string dataType, string writeValue, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(writeValue))
            return CreateFailureOutput("写入值不能为空");

        var bytes = ConvertValueToBytes(client, writeValue, dataType);
        var result = await client.WriteAsync(address, bytes, ct);

        if (!result.IsSuccess)
            return CreateFailureOutput($"写入失败: {result.Message}");

        Logger.LogInformation("[OmronFINS] 写入成功: {Address} = {Value}", address, writeValue);
        return CreateSuccessOutput(writeValue, dataType);
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        var operatorIpAddress = GetStringParam(@operator, "IpAddress", "");
        var operatorPort = GetIntParam(@operator, "Port", 0);
        var address = GetStringParam(@operator, "Address", "");
        var length = GetIntParam(@operator, "Length", 1);

        try
        {
            ResolveConnectionSettings(operatorIpAddress, operatorPort, "FINS");
        }
        catch (Exception ex)
        {
            return ValidationResult.Invalid(ex.Message);
        }

        if (string.IsNullOrWhiteSpace(address))
            return ValidationResult.Invalid("PLC地址不能为空");

        if (length < 1 || length > 999)
            return ValidationResult.Invalid("读取长度必须在 1-999 之间");

        return ValidationResult.Valid();
    }

    /// <summary>
    /// 解析写入值：优先从上游输入获取，否则使用参数面板静态值
    /// </summary>
    private string ResolveWriteValue(Operator @operator, Dictionary<string, object>? inputs)
    {
        // 获取参数面板中的静态值（作为fallback）
        var staticValue = GetStringParam(@operator, "WriteValue", "");

        if (inputs == null || inputs.Count == 0)
            return staticValue;

        // 按优先级顺序尝试从inputs获取动态值
        // 优先级：JudgmentValue > Value > Data > 静态值
        var priorityKeys = new[] { "JudgmentValue", "Value", "Data" };

        foreach (var key in priorityKeys)
        {
            if (inputs.TryGetValue(key, out var value) && value != null)
            {
                var stringValue = value.ToString() ?? "";
                if (!string.IsNullOrWhiteSpace(stringValue))
                {
                    Logger.LogDebug("[OmronFINS] 从上游获取动态值: Key={Key}, Value={Value}", key, stringValue);
                    return stringValue;
                }
            }
        }

        return staticValue;
    }
}
