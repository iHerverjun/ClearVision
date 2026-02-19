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
        var qos = GetIntParam(@operator, "QoS", 0, 0, 2);
        var retain = GetBoolParam(@operator, "Retain", false);
        var timeoutMs = GetIntParam(@operator, "TimeoutMs", 5000, 1000, 30000);

        if (string.IsNullOrWhiteSpace(topic))
        {
            return OperatorExecutionOutput.Failure("Topic 参数不能为空");
        }

        // 构建消息体
        string message;
        if (inputs != null && inputs.TryGetValue("Message", out var msgObj) && msgObj != null)
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

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(timeoutMs));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, cancellationToken);

            // 模拟 MQTT 发布（实际实现需要 MQTTnet）
            await PublishAsync(broker, port, topic, message, qos, retain, linkedCts.Token);

            Logger.LogInformation("[MqttPublish] 消息已发布: {Broker}:{Port}/{Topic}, QoS={QoS}, Retain={Retain}",
                broker, port, topic, qos, retain);

            return OperatorExecutionOutput.Success(new Dictionary<string, object>
            {
                { "Success", true },
                { "Broker", broker },
                { "Port", port },
                { "Topic", topic },
                { "QoS", qos },
                { "MessageLength", message.Length }
            });
        }
        catch (OperationCanceledException)
        {
            return OperatorExecutionOutput.Failure($"MQTT 发布超时 ({timeoutMs}ms)");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "[MqttPublish] 发布失败");
            return OperatorExecutionOutput.Failure($"MQTT 发布失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 模拟 MQTT 发布
    /// 
    /// 注意：此为占位实现，实际使用需要：
    /// 1. 添加 MQTTnet NuGet 包
    /// 2. 实现 MQTT 客户端连接管理
    /// 3. 处理连接池和重连逻辑
    /// </summary>
    private async Task PublishAsync(
        string broker, 
        int port, 
        string topic, 
        string message, 
        int qos, 
        bool retain,
        CancellationToken cancellationToken)
    {
        // TODO: 实现实际的 MQTT 发布逻辑
        // 示例代码（需要 MQTTnet）：
        /*
        var factory = new MqttFactory();
        using var client = factory.CreateMqttClient();
        
        var options = new MqttClientOptionsBuilder()
            .WithTcpServer(broker, port)
            .Build();
        
        await client.ConnectAsync(options, cancellationToken);
        
        var messageBuilder = new MqttApplicationMessageBuilder()
            .WithTopic(topic)
            .WithPayload(message)
            .WithQualityOfServiceLevel((MqttQualityOfServiceLevel)qos)
            .WithRetainFlag(retain);
        
        await client.PublishAsync(messageBuilder.Build(), cancellationToken);
        await client.DisconnectAsync();
        */

        // 模拟异步操作
        await Task.Delay(10, cancellationToken);
        
        Logger.LogDebug("[MqttPublish] 模拟发布到 {Broker}:{Port}/{Topic}", broker, port, topic);
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        var broker = GetStringParam(@operator, "Broker", "");
        var topic = GetStringParam(@operator, "Topic", "");
        var qos = GetIntParam(@operator, "QoS", 0);

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
