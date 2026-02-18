// PlcClientFactory.cs
// 从连接字符串创建客户端
// 作者：蘅芜君

using Acme.PlcComm.Interfaces;
using Acme.PlcComm.Siemens;
using Acme.PlcComm.Mitsubishi;
using Acme.PlcComm.Omron;
using Microsoft.Extensions.Logging;

namespace Acme.PlcComm;

/// <summary>
/// PLC客户端工厂
/// 用于根据配置创建对应的PLC客户端实例
/// </summary>
public static class PlcClientFactory
{
    /// <summary>
    /// 创建西门子S7客户端
    /// </summary>
    public static IPlcClient CreateSiemensS7(
        string ipAddress,
        SiemensCpuType cpuType = SiemensCpuType.S71200,
        int rack = 0,
        int slot = 1,
        ILogger? logger = null)
    {
        return new SiemensS7Client(ipAddress, cpuType, rack, slot, logger);
    }

    /// <summary>
    /// 创建三菱MC客户端
    /// </summary>
    public static IPlcClient CreateMitsubishiMc(
        string ipAddress,
        ILogger? logger = null)
    {
        return new MitsubishiMcClient(ipAddress, logger);
    }

    /// <summary>
    /// 创建欧姆龙FINS客户端
    /// </summary>
    public static IPlcClient CreateOmronFins(
        string ipAddress,
        ILogger? logger = null)
    {
        return new OmronFinsClient(ipAddress, logger);
    }

    /// <summary>
    /// 从连接字符串创建客户端
    /// 格式: S7://192.168.0.1:102?cpu=S7-1200&rack=0&slot=1
    ///       MC://192.168.3.1:5002
    ///       FINS://192.168.250.1:9600
    /// </summary>
    public static IPlcClient CreateFromConnectionString(string connectionString, ILogger? logger = null)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("连接字符串不能为空", nameof(connectionString));

        var uri = new Uri(connectionString);
        var protocol = uri.Scheme.ToUpperInvariant();
        var ipAddress = uri.Host;
        var port = uri.Port;

        // 解析查询参数
        var queryParams = System.Web.HttpUtility.ParseQueryString(uri.Query);

        return protocol switch
        {
            "S7" => CreateS7FromUri(uri, queryParams, logger),
            "MC" => CreateMcFromUri(uri, queryParams, logger),
            "FINS" => CreateFinsFromUri(uri, queryParams, logger),
            _ => throw new NotSupportedException($"不支持的协议类型: {protocol}")
        };
    }

    private static IPlcClient CreateS7FromUri(Uri uri, System.Collections.Specialized.NameValueCollection queryParams, ILogger? logger)
    {
        var ipAddress = uri.Host;
        
        // 解析CPU类型
        var cpuTypeStr = queryParams["cpu"] ?? "S7-1200";
        var cpuType = cpuTypeStr.ToUpper() switch
        {
            "S7-200" => SiemensCpuType.S7200,
            "S7-200SMART" => SiemensCpuType.S7200Smart,
            "S7-300" => SiemensCpuType.S7300,
            "S7-400" => SiemensCpuType.S7400,
            "S7-1200" => SiemensCpuType.S71200,
            "S7-1500" => SiemensCpuType.S71500,
            _ => SiemensCpuType.S71200
        };

        // 解析Rack和Slot
        var rack = int.Parse(queryParams["rack"] ?? "0");
        var slot = int.Parse(queryParams["slot"] ?? "1");

        var client = new SiemensS7Client(ipAddress, cpuType, rack, slot, logger);
        
        if (uri.Port > 0)
            client.Port = uri.Port;

        return client;
    }

    private static IPlcClient CreateMcFromUri(Uri uri, System.Collections.Specialized.NameValueCollection queryParams, ILogger? logger)
    {
        var ipAddress = uri.Host;
        var client = new MitsubishiMcClient(ipAddress, logger);
        
        if (uri.Port > 0)
            client.Port = uri.Port;

        return client;
    }

    private static IPlcClient CreateFinsFromUri(Uri uri, System.Collections.Specialized.NameValueCollection queryParams, ILogger? logger)
    {
        var ipAddress = uri.Host;
        var client = new OmronFinsClient(ipAddress, logger);
        
        if (uri.Port > 0)
            client.Port = uri.Port;

        return client;
    }
}
