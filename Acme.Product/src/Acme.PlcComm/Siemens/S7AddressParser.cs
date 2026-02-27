// S7AddressParser.cs
// 西门子S7地址解析器
// 作者：蘅芜君

using Acme.PlcComm.Core;
using Acme.PlcComm.Interfaces;
using System.Text.RegularExpressions;

namespace Acme.PlcComm.Siemens;

/// <summary>
/// 西门子S7地址解析器
/// 支持地址格式: DB1.DBW100, DB1.DBX10.3, M100, I0.0, Q0.0等
/// </summary>
public class S7AddressParser : IAddressParser
{
    // 正则表达式匹配各种地址格式
    private static readonly Regex DbAddressRegex = new(
        @"^DB(?<db>\d+)\.(?<type>DB[XBWDR])(?<offset>\d+)(?:\.(?<bit>\d+))?$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex SimpleAddressRegex = new(
        @"^(?<prefix>[MIQETC])(?<offset>\d+)(?:\.(?<bit>\d+))?$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex TypedSimpleAddressRegex = new(
        @"^(?<prefix>[MIQEA])(?<type>[BWD])(?<offset>\d+)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public OperateResult<PlcAddress> Parse(string address)
    {
        if (string.IsNullOrWhiteSpace(address))
            return OperateResult<PlcAddress>.Failure("地址不能为空");

        address = address.Trim().ToUpper();

        // 尝试匹配DB地址
        var dbMatch = DbAddressRegex.Match(address);
        if (dbMatch.Success)
        {
            return ParseDbAddress(dbMatch);
        }

        // 尝试匹配简单地址(M, I, Q等)
        var simpleMatch = SimpleAddressRegex.Match(address);
        if (simpleMatch.Success)
        {
            return ParseSimpleAddress(simpleMatch);
        }

        // 尝试匹配带类型前缀的简单地址(MB/MW/MD 等)
        var typedSimpleMatch = TypedSimpleAddressRegex.Match(address);
        if (typedSimpleMatch.Success)
        {
            return ParseTypedSimpleAddress(typedSimpleMatch);
        }

        return OperateResult<PlcAddress>.Failure($"不支持的地址格式: {address}");
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
        
        address = address.Trim().ToUpper();
        return DbAddressRegex.IsMatch(address)
               || SimpleAddressRegex.IsMatch(address)
               || TypedSimpleAddressRegex.IsMatch(address);
    }

    private OperateResult<PlcAddress> ParseDbAddress(Match match)
    {
        try
        {
            var dbNumber = int.Parse(match.Groups["db"].Value);
            var typeCode = match.Groups["type"].Value.ToUpper();
            var offset = int.Parse(match.Groups["offset"].Value);
            var bitOffset = match.Groups["bit"].Success ? int.Parse(match.Groups["bit"].Value) : -1;

            // 验证位偏移范围
            if (bitOffset > 7)
                return OperateResult<PlcAddress>.Failure("位偏移必须在0-7之间");

            var plcAddress = new PlcAddress
            {
                AreaType = "DB",
                DbNumber = dbNumber,
                StartAddress = offset,
                BitOffset = bitOffset
            };

            // 根据类型码确定数据类型
            plcAddress.DataType = typeCode switch
            {
                "DBX" => PlcDataType.Bit,
                "DBB" => PlcDataType.Byte,
                "DBW" => PlcDataType.Word,
                "DBD" => PlcDataType.DWord,
                "DBR" => PlcDataType.Float,
                _ => PlcDataType.Word
            };

            // 设置设备代码(用于协议层)
            plcAddress.DeviceCode = 0x84; // DB区域代码

            return OperateResult<PlcAddress>.Success(plcAddress);
        }
        catch (Exception ex)
        {
            return OperateResult<PlcAddress>.Failure($"解析DB地址失败: {ex.Message}");
        }
    }

    private OperateResult<PlcAddress> ParseSimpleAddress(Match match)
    {
        try
        {
            var prefix = match.Groups["prefix"].Value.ToUpper();
            var offset = int.Parse(match.Groups["offset"].Value);
            var bitOffset = match.Groups["bit"].Success ? int.Parse(match.Groups["bit"].Value) : -1;

            // 验证位偏移范围
            if (bitOffset > 7)
                return OperateResult<PlcAddress>.Failure("位偏移必须在0-7之间");

            var plcAddress = new PlcAddress
            {
                AreaType = prefix,
                StartAddress = offset,
                BitOffset = bitOffset
            };

            // 根据前缀确定区域和设备代码
            (plcAddress.DeviceCode, plcAddress.DataType) = prefix switch
            {
                "M" => ((byte)0x83, bitOffset >= 0 ? PlcDataType.Bit : PlcDataType.Word),
                "I" or "E" => ((byte)0x81, bitOffset >= 0 ? PlcDataType.Bit : PlcDataType.Word),
                "Q" or "A" => ((byte)0x82, bitOffset >= 0 ? PlcDataType.Bit : PlcDataType.Word),
                "T" => ((byte)0x1F, PlcDataType.Word),
                "C" => ((byte)0x1E, PlcDataType.Word),
                _ => ((byte)0x00, PlcDataType.Word)
            };

            return OperateResult<PlcAddress>.Success(plcAddress);
        }
        catch (Exception ex)
        {
            return OperateResult<PlcAddress>.Failure($"解析地址失败: {ex.Message}");
        }
    }

    private OperateResult<PlcAddress> ParseTypedSimpleAddress(Match match)
    {
        try
        {
            var prefix = match.Groups["prefix"].Value.ToUpper();
            var typeCode = match.Groups["type"].Value.ToUpper();
            var offset = int.Parse(match.Groups["offset"].Value);

            var plcAddress = new PlcAddress
            {
                AreaType = prefix,
                StartAddress = offset,
                BitOffset = -1
            };

            (plcAddress.DeviceCode, plcAddress.DataType) = prefix switch
            {
                "M" => ((byte)0x83, typeCode switch
                {
                    "B" => PlcDataType.Byte,
                    "W" => PlcDataType.Word,
                    "D" => PlcDataType.DWord,
                    _ => PlcDataType.Word
                }),
                "I" or "E" => ((byte)0x81, typeCode switch
                {
                    "B" => PlcDataType.Byte,
                    "W" => PlcDataType.Word,
                    "D" => PlcDataType.DWord,
                    _ => PlcDataType.Word
                }),
                "Q" or "A" => ((byte)0x82, typeCode switch
                {
                    "B" => PlcDataType.Byte,
                    "W" => PlcDataType.Word,
                    "D" => PlcDataType.DWord,
                    _ => PlcDataType.Word
                }),
                _ => ((byte)0x00, PlcDataType.Word)
            };

            return OperateResult<PlcAddress>.Success(plcAddress);
        }
        catch (Exception ex)
        {
            return OperateResult<PlcAddress>.Failure($"解析带类型地址失败: {ex.Message}");
        }
    }

}
