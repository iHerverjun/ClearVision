using System.Buffers.Binary;
using System.Text;
using System.Text.RegularExpressions;
using Acme.PlcComm.Core;
using Acme.PlcComm.Interfaces;

namespace Acme.PlcComm.Mitsubishi;

/// <summary>
/// 三菱MC协议地址解析器
/// 支持地址格式: D100, M200, X10, Y17, B1F, W100等
/// </summary>
public class McAddressParser : IAddressParser
{
    // 软元件代码映射表
    private static readonly Dictionary<string, byte> DeviceCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["D"] = 0xA8,    // 数据寄存器(十进制)
        ["W"] = 0xB4,    // 链接寄存器(十六进制)
        ["R"] = 0xAF,    // 文件寄存器(十进制)
        ["M"] = 0x90,    // 辅助继电器(十进制)
        ["L"] = 0x92,    // 锁存继电器(十进制)
        ["B"] = 0xA0,    // 链接继电器(十六进制)
        ["X"] = 0x9C,    // 输入继电器(八进制)
        ["Y"] = 0x9D,    // 输出继电器(八进制)
        ["F"] = 0x93,    // 报警器(十进制)
        ["V"] = 0x94,    // 边缘继电器(十进制)
        ["S"] = 0x98,    // 步进继电器(十进制)
        ["SM"] = 0x91,   // 特殊继电器(十进制)
        ["SD"] = 0xA9,   // 特殊寄存器(十进制)
        ["TS"] = 0xC1,   // 定时器触点(十进制)
        ["TC"] = 0xC0,   // 定时器线圈(十进制)
        ["TN"] = 0xC2,   // 定时器当前值(十进制)
        ["CS"] = 0xC5,   // 计数器触点(十进制)
        ["CC"] = 0xC4,   // 计数器线圈(十进制)
        ["CN"] = 0xC6,   // 计数器当前值(十进制)
    };

    // 地址解析正则
    private static readonly Regex AddressRegex = new(
        @"^(?<prefix>[SM]*[A-Z]+)(?<address>[0-9A-Fa-f]+)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public OperateResult<PlcAddress> Parse(string address)
    {
        if (string.IsNullOrWhiteSpace(address))
            return OperateResult<PlcAddress>.Failure("地址不能为空");

        address = address.Trim().ToUpper();

        var match = AddressRegex.Match(address);
        if (!match.Success)
            return OperateResult<PlcAddress>.Failure($"无效的地址格式: {address}");

        var prefix = match.Groups["prefix"].Value;
        var addrStr = match.Groups["address"].Value;

        if (!DeviceCodes.TryGetValue(prefix, out var deviceCode))
            return OperateResult<PlcAddress>.Failure($"不支持的软元件类型: {prefix}");

        try
        {
            int startAddress;
            var dataType = PlcDataType.Word;

            // 根据软元件类型解析地址(不同的进制)
            if (prefix == "X" || prefix == "Y")
            {
                // X/Y使用八进制
                startAddress = Convert.ToInt32(addrStr, 8);
                dataType = PlcDataType.Bit;
            }
            else if (prefix == "B" || prefix == "W")
            {
                // B/W使用十六进制
                startAddress = Convert.ToInt32(addrStr, 16);
                dataType = prefix == "B" ? PlcDataType.Bit : PlcDataType.Word;
            }
            else
            {
                // 其他使用十进制
                startAddress = int.Parse(addrStr);
                // 判断是位还是字访问
                dataType = IsBitDevice(prefix) ? PlcDataType.Bit : PlcDataType.Word;
            }

            var plcAddress = new PlcAddress
            {
                AreaType = prefix,
                StartAddress = startAddress,
                DataType = dataType,
                DeviceCode = deviceCode
            };

            return OperateResult<PlcAddress>.Success(plcAddress);
        }
        catch (Exception ex)
        {
            return OperateResult<PlcAddress>.Failure($"解析地址失败: {ex.Message}");
        }
    }

    public bool TryParse(string address, out PlcAddress result)
    {
        var parseResult = Parse(address);
        if (parseResult.IsSuccess)
        {
            result = parseResult.Content!;
            return true;
        }
        result = new PlcAddress();
        return false;
    }

    public string ToAddressString(PlcAddress address)
    {
        return address.ToString();
    }

    public bool IsValidAddress(string address)
    {
        if (string.IsNullOrWhiteSpace(address)) return false;
        
        var match = AddressRegex.Match(address.Trim().ToUpper());
        if (!match.Success) return false;

        var prefix = match.Groups["prefix"].Value;
        return DeviceCodes.ContainsKey(prefix);
    }

    /// <summary>
    /// 获取软元件代码
    /// </summary>
    public static byte GetDeviceCode(string prefix)
    {
        return DeviceCodes.TryGetValue(prefix.ToUpper(), out var code) ? code : (byte)0;
    }

    /// <summary>
    /// 判断是否为位软元件
    /// </summary>
    private static bool IsBitDevice(string prefix)
    {
        var bitDevices = new[] { "X", "Y", "M", "L", "B", "F", "V", "S", "SM", "TS", "TC", "CS", "CC" };
        return bitDevices.Contains(prefix, StringComparer.OrdinalIgnoreCase);
    }
}
