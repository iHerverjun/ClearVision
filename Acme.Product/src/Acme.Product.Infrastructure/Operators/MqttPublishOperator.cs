// MqttPublishOperator.cs
// MQTT 发布算子 - Sprint 3 Task 3.5b
// 向数字孪生/IoT 平台推送检测状态
// 作者：蘅芜君

using System.Text;
using System.Text.Json;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Microsoft.Extensions.Logging;

using Acme.Product.Core.Attributes;
namespace Acme.Product.Infrastructure.Operators;

/// <summary>
/// MQTT 发布算子 - 向消息队列推送数据
/// 
/// 功能：
/// - 连接 MQTT Broker
/// - 发布消息到指定 Topic
/// - 支持 QoS 0/1/2
/// - 支持保留消息
/// 
/// 使用场景：
/// - 向数字孪生平台推送检测状态
/// - 向 IoT 平台上报设备数据
/// - 触发 MQTT 订阅者
/// 
/// 注意：此为基础框架，实际 MQTT 连接需要 MQTTnet 等库
/// </summary>
[OperatorMeta(
    DisplayName = "MQTT 发布",
    Description = "向消息队列推送数据",
    Category = "通信",
    IconName = "mqtt"
)]
[InputPort("Payload", "消息负载", PortDataType.Any, IsRequired = true)]
[InputPort("Message", "消息内容", PortDataType.String, IsRequired = false)]
[OutputPort("IsSuccess", "是否成功", PortDataType.Boolean)]
[OperatorParam("Broker", "Broker地址", "string", DefaultValue = "localhost")]
[OperatorParam("Port", "端口", "int", DefaultValue = 1883)]
[OperatorParam("Topic", "主题", "string", DefaultValue = "cv/results")]
[OperatorParam("Qos", "QoS", "int", DefaultValue = 1)]
[OperatorParam("Retain", "保留消息", "bool", DefaultValue = false)]
[OperatorParam("TimeoutMs", "超时(毫秒)", "int", DefaultValue = 5000)]
public class MqttPublishOperator : OperatorBase
{
    public override OperatorType OperatorType => OperatorType.MqttPublish;

    public MqttPublishOperator(ILogger<MqttPublishOperator> logger) : base(logger) { }

    protected override async Task<OperatorExecutionOutput> ExecuteCoreAsync(
        Operator @operator,
        Dictionary<string, object>? inputs,
        CancellationToken cancellationToken)
    {
        // 获取参数
        var broker = GetStringParam(@operator, "Broker", "localhost");
        var port = GetIntParam(@operator, "Port", 1883, 1, 65535);
        var topic = GetStringParam(@operator, "Topic", "");
        var qos = GetQosParam(@operator, 0);
        var retain = GetBoolParam(@operator, "Retain", false);
        var timeoutMs = GetIntParam(@operator, "TimeoutMs", 5000, 1000, 30000);

        if (string.IsNullOrWhiteSpace(topic))
        {
            return OperatorExecutionOutput.Failure("Topic 参数不能为空");
        }

        // 构建消息体
        string message;
        if (TryGetInputValue(inputs, "Payload", out var payloadObj) && payloadObj != null)
        {
            message = payloadObj is string payloadText
                ? payloadText
                : JsonSerializer.Serialize(payloadObj);
        }
        else if (TryGetInputValue(inputs, "Message", out var msgObj) && msgObj != null)
        {
            message = msgObj.ToString() ?? "";
        }
        else if (inputs != null && inputs.Count > 0)
        {
            // 将所有输入序列化为 JSON
            message = JsonSerializer.Serialize(inputs);
        }
        else
        {
            message = "{}";
        }

        Logger.LogWarning(
            "[MqttPublish] MQTT publish requested for {Broker}:{Port}/{Topic} with timeout {TimeoutMs}ms, but the runtime integration is not enabled in this build.",
            broker, port, topic, timeoutMs);

        return OperatorExecutionOutput.Failure(
            "MQTT 发布功能在当前构建中未启用，请先接入 MQTT 客户端实现后再使用该算子。");
    }

    private static bool TryGetInputValue(
        Dictionary<string, object>? inputs,
        string key,
        out object? value)
    {
        value = null;
        if (inputs == null)
        {
            return false;
        }

        if (inputs.TryGetValue(key, out value))
        {
            return true;
        }

        var match = inputs.FirstOrDefault(entry => string.Equals(entry.Key, key, StringComparison.OrdinalIgnoreCase));
        if (string.IsNullOrEmpty(match.Key))
        {
            return false;
        }

        value = match.Value;
        return true;
    }

    private static int GetQosParam(Operator @operator, int defaultValue)
    {
        var param = @operator.Parameters.FirstOrDefault(p =>
            string.Equals(p.Name, "Qos", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(p.Name, "QoS", StringComparison.OrdinalIgnoreCase));

        if (param?.GetValue() == null)
        {
            return defaultValue;
        }

        try
        {
            return Math.Clamp(Convert.ToInt32(param.GetValue()), 0, 2);
        }
        catch
        {
            return defaultValue;
        }
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        var broker = GetStringParam(@operator, "Broker", "");
        var topic = GetStringParam(@operator, "Topic", "");
        var qos = GetQosParam(@operator, 0);

        if (string.IsNullOrWhiteSpace(broker))
        {
            return ValidationResult.Invalid("Broker 不能为空");
        }

        if (string.IsNullOrWhiteSpace(topic))
        {
            return ValidationResult.Invalid("Topic 不能为空");
        }

        if (qos < 0 || qos > 2)
        {
            return ValidationResult.Invalid("QoS 必须是 0/1/2");
        }

        return ValidationResult.Valid();
    }
}
