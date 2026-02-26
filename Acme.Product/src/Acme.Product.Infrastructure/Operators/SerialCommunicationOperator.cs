// SerialCommunicationOperator.cs
// 串口通信算子 - RS-232// 功能实现485 PLC 通信
// 作者：蘅芜君

using System.IO.Ports;
using System.Text;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Microsoft.Extensions.Logging;

using Acme.Product.Core.Attributes;
namespace Acme.Product.Infrastructure.Operators;

/// <summary>
/// 串口通信算子 - RS-232/485 PLC 通信
/// SerialCommunication = 46
/// </summary>
[OperatorMeta(
    DisplayName = "串口通信",
    Description = "RS-232/485 串口数据收发",
    Category = "通信",
    IconName = "serial"
)]
[InputPort("Data", "发送数据", PortDataType.Any, IsRequired = false)]
[OutputPort("Response", "接收数据", PortDataType.Any)]
[OperatorParam("PortName", "串口号", "string", DefaultValue = "COM1")]
[OperatorParam("BaudRate", "波特率", "enum", DefaultValue = "9600", Options = new[] { "9600|9600", "19200|19200", "38400|38400", "57600|57600", "115200|115200" })]
[OperatorParam("DataBits", "数据位", "int", DefaultValue = 8, Min = 5, Max = 8)]
[OperatorParam("StopBits", "停止位", "enum", DefaultValue = "One", Options = new[] { "One|1", "OnePointFive|1.5", "Two|2" })]
[OperatorParam("Parity", "校验位", "enum", DefaultValue = "None", Options = new[] { "None|无", "Odd|奇校验", "Even|偶校验" })]
[OperatorParam("SendData", "发送内容", "string", DefaultValue = "")]
[OperatorParam("Encoding", "编码", "enum", DefaultValue = "UTF8", Options = new[] { "UTF8|UTF-8", "ASCII|ASCII", "HEX|HEX" })]
[OperatorParam("TimeoutMs", "超时(毫秒)", "int", DefaultValue = 3000, Min = 100, Max = 30000)]
public class SerialCommunicationOperator : OperatorBase
{
    public override OperatorType OperatorType => OperatorType.SerialCommunication;

    public SerialCommunicationOperator(ILogger<SerialCommunicationOperator> logger) : base(logger) { }

    protected override Task<OperatorExecutionOutput> ExecuteCoreAsync(
        Operator @operator,
        Dictionary<string, object>? inputs,
        CancellationToken cancellationToken)
    {
        // 获取参数
        var portName = GetStringParam(@operator, "PortName", "COM1");
        var baudRateStr = GetStringParam(@operator, "BaudRate", "9600");
        var dataBits = GetIntParam(@operator, "DataBits", 8);
        var stopBitsStr = GetStringParam(@operator, "StopBits", "One");
        var parityStr = GetStringParam(@operator, "Parity", "None");
        var timeoutMs = GetIntParam(@operator, "TimeoutMs", 3000);
        var sendData = GetStringParam(@operator, "SendData", "");
        var encoding = GetStringParam(@operator, "Encoding", "UTF8");

        // 解析波特率
        if (!int.TryParse(baudRateStr, out var baudRate))
        {
            baudRate = 9600;
        }

        // 解析停止位
        if (!Enum.TryParse<StopBits>(stopBitsStr, out var stopBits))
        {
            stopBits = StopBits.One;
        }

        // 解析校验位
        if (!Enum.TryParse<Parity>(parityStr, out var parity))
        {
            parity = Parity.None;
        }

        // 验证数据位范围
        if (dataBits < 5 || dataBits > 8)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("数据位必须在 5-8 之间"));
        }

        using var port = new SerialPort(portName, baudRate, parity, dataBits, stopBits)
        {
            ReadTimeout = timeoutMs,
            WriteTimeout = timeoutMs
        };

        try
        {
            port.Open();
            
            // 发送数据
            if (!string.IsNullOrEmpty(sendData))
            {
                byte[] bytes;
                if (encoding.Equals("HEX", StringComparison.OrdinalIgnoreCase))
                {
                    // HEX 模式：将十六进制字符串转换为字节数组
                    var hexString = sendData.Replace(" ", "").Replace("-", "");
                    if (hexString.Length % 2 != 0)
                    {
                        return Task.FromResult(OperatorExecutionOutput.Failure("HEX 数据长度必须是偶数"));
                    }
                    
                    bytes = new byte[hexString.Length / 2];
                    for (int i = 0; i < hexString.Length; i += 2)
                    {
                        bytes[i / 2] = Convert.ToByte(hexString.Substring(i, 2), 16);
                    }
                }
                else
                {
                    // 文本模式
                    var textEncoding = encoding.ToUpper() switch
                    {
                        "ASCII" => Encoding.ASCII,
                        "UTF8" => Encoding.UTF8,
                        _ => Encoding.UTF8
                    };
                    bytes = textEncoding.GetBytes(sendData);
                }
                
                port.Write(bytes, 0, bytes.Length);
                Logger.LogInformation("[SerialCommunication] 已发送 {Bytes} 字节到 {Port}", bytes.Length, portName);
            }
            
            // 接收响应
            Thread.Sleep(100); // 等待设备响应
            
            string response = "";
            if (port.BytesToRead > 0)
            {
                byte[] buffer = new byte[port.BytesToRead];
                int bytesRead = port.Read(buffer, 0, buffer.Length);
                
                if (encoding.Equals("HEX", StringComparison.OrdinalIgnoreCase))
                {
                    response = BitConverter.ToString(buffer, 0, bytesRead).Replace("-", " ");
                }
                else
                {
                    var textEncoding = encoding.ToUpper() switch
                    {
                        "ASCII" => Encoding.ASCII,
                        "UTF8" => Encoding.UTF8,
                        _ => Encoding.UTF8
                    };
                    response = textEncoding.GetString(buffer, 0, bytesRead);
                }
                
                Logger.LogInformation("[SerialCommunication] 从 {Port} 接收 {Bytes} 字节", portName, bytesRead);
            }
            
            var output = new Dictionary<string, object>
            {
                { "Response", response },
                { "BytesReceived", response.Length },
                { "Port", portName },
                { "BaudRate", baudRate },
                { "Success", true }
            };
            
            return Task.FromResult(OperatorExecutionOutput.Success(output));
        }
        catch (UnauthorizedAccessException ex)
        {
            Logger.LogError(ex, "[SerialCommunication] 串口 {Port} 访问被拒绝", portName);
            return Task.FromResult(OperatorExecutionOutput.Failure($"串口 {portName} 访问被拒绝，请检查串口是否被其他程序占用"));
        }
        catch (IOException ex)
        {
            Logger.LogError(ex, "[SerialCommunication] 串口 {Port} IO 错误", portName);
            return Task.FromResult(OperatorExecutionOutput.Failure($"串口 {portName} IO 错误: {ex.Message}"));
        }
        catch (TimeoutException ex)
        {
            Logger.LogError(ex, "[SerialCommunication] 串口 {Port} 操作超时", portName);
            return Task.FromResult(OperatorExecutionOutput.Failure($"串口 {portName} 操作超时"));
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "[SerialCommunication] 串口通信失败: {Port}", portName);
            return Task.FromResult(OperatorExecutionOutput.Failure($"串口通信失败: {ex.Message}"));
        }
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        var portName = GetStringParam(@operator, "PortName", "COM1");
        var baudRateStr = GetStringParam(@operator, "BaudRate", "9600");
        var dataBits = GetIntParam(@operator, "DataBits", 8);

        if (string.IsNullOrWhiteSpace(portName))
        {
            return ValidationResult.Invalid("串口号不能为空");
        }

        if (!int.TryParse(baudRateStr, out var baudRate))
        {
            return ValidationResult.Invalid("波特率必须是数字");
        }

        if (baudRate <= 0)
        {
            return ValidationResult.Invalid("波特率必须大于 0");
        }

        if (dataBits < 5 || dataBits > 8)
        {
            return ValidationResult.Invalid("数据位必须在 5-8 之间");
        }

        return ValidationResult.Valid();
    }
}
