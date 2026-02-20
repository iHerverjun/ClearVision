// TcpCommunicationOperator.cs
// 客户端模式执行（带连接池）
// 作者：蘅芜君

using System.Collections.Concurrent;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Microsoft.Extensions.Logging;
using System.Net.Sockets;
using System.Text;

namespace Acme.Product.Infrastructure.Operators;

/// <summary>
/// TCP/IP通信算子 - 支持客户端/服务器模式（带连接池）
/// </summary>
public class TcpCommunicationOperator : OperatorBase
{
    public override OperatorType OperatorType => OperatorType.TcpCommunication;

    public TcpCommunicationOperator(ILogger<TcpCommunicationOperator> logger) : base(logger) { }

    // 连接池 - 静态缓存
    private static readonly ConcurrentDictionary<string, TcpClient> _connectionPool = new();
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> _connectionLocks = new();
    private static readonly ConcurrentDictionary<string, NetworkStream> _streamPool = new();

    protected override async Task<OperatorExecutionOutput> ExecuteCoreAsync(
        Operator @operator,
        Dictionary<string, object>? inputs,
        CancellationToken cancellationToken)
    {
        // 获取输入数据（可选）
        object? inputData = null;
        if (inputs != null && inputs.TryGetValue("Data", out var data))
        {
            inputData = data;
        }

        // 获取参数
        var mode = GetStringParam(@operator, "Mode", "Client");
        var ipAddress = GetStringParam(@operator, "IpAddress", "127.0.0.1");
        var port = GetIntParam(@operator, "Port", 8080, 1, 65535);
        var sendData = GetStringParam(@operator, "SendData", "");
        var timeout = GetIntParam(@operator, "Timeout", 5000, 100, 30000);
        var encoding = GetStringParam(@operator, "Encoding", "UTF8");

        var enc = encoding.ToUpper() switch
        {
            "ASCII" => Encoding.ASCII,
            "GBK" => Encoding.GetEncoding("GBK"),
            _ => Encoding.UTF8
        };

        string response = "";
        bool status = false;

        if (mode == "Client")
        {
            (response, status) = await ExecuteClientModeAsync(
                ipAddress, port, sendData, timeout, enc, cancellationToken);
        }
        else
        {
            // 服务器模式简化实现
            response = "服务器模式需要单独启动监听，当前版本仅支持客户端模式";
            status = false;
        }

        return OperatorExecutionOutput.Success(new Dictionary<string, object>
        {
            { "Response", response },
            { "Status", status },
            { "Mode", mode },
            { "IpAddress", ipAddress },
            { "Port", port }
        });
    }

    /// <summary>
    /// 从连接池获取或创建连接
    /// </summary>
    private async Task<(TcpClient client, NetworkStream stream)> GetOrCreateConnectionAsync(
        string ipAddress, int port, int timeoutMs, CancellationToken ct)
    {
        var key = $"{ipAddress}:{port}";
        var lockObj = _connectionLocks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));

        await lockObj.WaitAsync(ct);
        try
        {
            // 检查现有连接是否有效
            if (_connectionPool.TryGetValue(key, out var existingClient) &&
                _streamPool.TryGetValue(key, out var existingStream))
            {
                if (IsConnectionAlive(existingClient))
                {
                    Logger.LogDebug("TCP 连接复用: {Key}", key);
                    return (existingClient, existingStream);
                }

                // 清理旧连接
                try
                { existingStream.Dispose(); }
                catch { }
                _streamPool.TryRemove(key, out _);
                try
                { existingClient.Close(); }
                catch { }
                _connectionPool.TryRemove(key, out _);
            }

            // 建立新连接
            var client = new TcpClient();
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(timeoutMs);

            await client.ConnectAsync(ipAddress, port, cts.Token);
            var stream = client.GetStream();

            _connectionPool[key] = client;
            _streamPool[key] = stream;

            Logger.LogInformation("TCP 连接已建立: {Key}", key);
            return (client, stream);
        }
        finally
        {
            lockObj.Release();
        }
    }

    /// <summary>
    /// 检测连接是否存活
    /// </summary>
    private bool IsConnectionAlive(TcpClient client)
    {
        try
        {
            if (!client.Connected)
                return false;
            return !(client.Client.Poll(1, SelectMode.SelectRead) && client.Client.Available == 0);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 客户端模式执行（带连接池）
    /// </summary>
    private async Task<(string response, bool status)> ExecuteClientModeAsync(
        string ipAddress,
        int port,
        string sendData,
        int timeout,
        Encoding encoding,
        CancellationToken cancellationToken)
    {
        try
        {
            // 从连接池获取连接
            var (_, stream) = await GetOrCreateConnectionAsync(ipAddress, port, timeout, cancellationToken);

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromMilliseconds(timeout));

            // 发送数据
            if (!string.IsNullOrEmpty(sendData))
            {
                var sendBytes = encoding.GetBytes(sendData);
                await stream.WriteAsync(sendBytes.AsMemory(0, sendBytes.Length), cts.Token);
                await stream.FlushAsync(cts.Token);
            }

            // 接收响应
            var buffer = new byte[4096];
            var bytesRead = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cts.Token);
            var response = encoding.GetString(buffer, 0, bytesRead);

            return (response, true);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return ("通信被取消", false);
        }
        catch (OperationCanceledException)
        {
            return ("连接超时", false);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "TCP 通信错误: {IpAddress}:{Port}", ipAddress, port);
            return ($"通信错误: {ex.Message}", false);
        }
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        var host = GetStringParam(@operator, "Host", "127.0.0.1");
        var port = GetIntParam(@operator, "Port", 8080);
        var timeout = GetIntParam(@operator, "Timeout", 5000);
        var mode = GetStringParam(@operator, "Mode", "Client");
        var encoding = GetStringParam(@operator, "Encoding", "UTF8");

        if (@operator.Parameters.Any(p => p.Name == "Host") && string.IsNullOrEmpty(host))
        {
            return ValidationResult.Invalid("主机地址不能为空");
        }
        if (port < 1 || port > 65535)
        {
            return ValidationResult.Invalid("端口号必须在 1-65535 之间");
        }
        if (timeout < 100 || timeout > 30000)
        {
            return ValidationResult.Invalid("超时时间必须在 100-30000 ms 之间");
        }
        if (mode != "Client" && mode != "Server")
        {
            return ValidationResult.Invalid("模式必须是 Client 或 Server");
        }
        if (encoding.ToUpper() != "UTF8" && encoding.ToUpper() != "ASCII" && encoding.ToUpper() != "GBK")
        {
            return ValidationResult.Invalid("编码必须是 UTF8、ASCII 或 GBK");
        }

        return ValidationResult.Valid();
    }
}
