// FinsAddressParser.cs
// 验证地址范围
// 作者：蘅芜君

using System.Text.RegularExpressions;
using Acme.PlcComm.Core;
using Acme.PlcComm.Interfaces;

namespace Acme.PlcComm.Omron;

/// <summary>
/// 欧姆龙FINS地址解析器
/// 支持地址格式: DM100, CIO200, W100, H50, A10等
/// </summary>
public class FinsAddressParser : IAddressParser
{
    // 内存区代码映射表
    // 字访问和位访问使用不同的代码
    private static readonly Dictionary<string, (byte wordCode, byte bitCode)> AreaCodes = 
        new(StringComparer.OrdinalIgnoreCase)
    {
        ["CIO"] = (0xB0, 0x30),  // 通道IO区
        ["WR"] = (0xB1, 0x31),   // 工作区
        ["HR"] = (0xB2, 0x32),   // 保持区
        ["AR"] = (0xB3, 0x33),   // 辅助区
        ["DM"] = (0x82, 0x02),   // 数据存储区
        ["TIM"] = (0x89, 0x09),  // 定时器区
        ["CNT"] = (0x89, 0x09),  // 计数器区(与定时器相同)
    };

    // EM区(扩展数据存储)支持多个bank
    private const byte EmWordBase = 0xA0;
    private const byte EmBitBase = 0x20;

    // 地址解析正则
    private static readonly Regex AddressRegex = new(
        @"^(?<prefix>[A-Z]+)(?<bank>[0-9]*)\s*(?<address>\d+)(?:\.(?<bit>\d+))?$",
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
        var bankStr = match.Groups["bank"].Value;
        var addrStr = match.Groups["address"].Value;
        var hasBitOffset = match.Groups["bit"].Success;
        var bitOffset = hasBitOffset ? int.Parse(match.Groups["bit"].Value) : -1;

        try
        {
            var startAddress = int.Parse(addrStr);
            var bank = string.IsNullOrEmpty(bankStr) ? 0 : int.Parse(bankStr);

            byte wordCode;
            byte bitCode;

            // 检查是否是EM区
            if (prefix == "EM")
            {
                if (bank > 15)
                    return OperateResult<PlcAddress>.Failure("EM区bank号必须在0-15之间");

                wordCode = (byte)(EmWordBase + bank);
                bitCode = (byte)(EmBitBase + bank);
            }
            else if (AreaCodes.TryGetValue(prefix, out var codes))
            {
                wordCode = codes.wordCode;
                bitCode = codes.bitCode;
            }
            else
            {
                return OperateResult<PlcAddress>.Failure($"不支持的内存区: {prefix}");
            }

            // 验证地址范围
            var validation = ValidateAddressRange(prefix, startAddress, bank);
            if (!string.IsNullOrEmpty(validation))
                return OperateResult<PlcAddress>.Failure(validation);

            // 验证位偏移
            if (hasBitOffset && bitOffset > 15)
                return OperateResult<PlcAddress>.Failure("位偏移必须在0-15之间");

            var plcAddress = new PlcAddress
            {
                AreaType = prefix,
                DbNumber = bank, // 用于EM区的bank号
                StartAddress = startAddress,
                BitOffset = bitOffset,
                DataType = hasBitOffset ? PlcDataType.Bit : PlcDataType.Word,
                DeviceCode = hasBitOffset ? bitCode : wordCode
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
        return AreaCodes.ContainsKey(prefix) || prefix == "EM";
    }

    /// <summary>
    /// 获取内存区代码
    /// </summary>
    public static byte GetAreaCode(string prefix, bool isBitAccess, int bank = 0)
    {
        prefix = prefix.ToUpper();

        if (prefix == "EM")
        {
            return isBitAccess 
                ? (byte)(EmBitBase + bank) 
                : (byte)(EmWordBase + bank);
        }

        if (AreaCodes.TryGetValue(prefix, out var codes))
        {
            return isBitAccess ? codes.bitCode : codes.wordCode;
        }

        return 0;
    }

    /// <summary>
    /// 验证地址范围
    /// </summary>
    private static string? ValidateAddressRange(string prefix, int address, int bank)
    {
        var maxAddresses = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["CIO"] = 6143,
            ["WR"] = 511,
            ["HR"] = 511,
            ["AR"] = 959,
            ["DM"] = 32767,
            ["TIM"] = 4095,
            ["CNT"] = 4095,
            ["EM"] = 32767,
        };

        if (maxAddresses.TryGetValue(prefix, out var maxAddr))
        {
            if (address > maxAddr)
                return $"{prefix}区地址超出范围，最大值为{maxAddr}";
        }

        return null;
    }
}
