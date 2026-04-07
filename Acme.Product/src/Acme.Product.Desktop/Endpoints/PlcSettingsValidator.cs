using Acme.PlcComm.Core;
using Acme.PlcComm.Interfaces;
using Acme.PlcComm.Mitsubishi;
using Acme.PlcComm.Omron;
using Acme.PlcComm.Siemens;
using Acme.Product.Core.Entities;

namespace Acme.Product.Desktop.Endpoints;

public static class PlcSettingsValidator
{
    public static PlcSettingsValidationResult Validate(CommunicationConfig? communication)
    {
        var result = new PlcSettingsValidationResult();
        communication ??= new CommunicationConfig();
        communication.Normalize();

        var protocol = CommunicationConfig.NormalizeProtocolKey(communication.ActiveProtocol);
        var profile = communication.GetProfile(protocol);

        ValidateConnection(protocol, communication, profile, result);
        ValidateMappings(protocol, profile.Mappings, result);

        return result;
    }

    private static void ValidateConnection(
        string protocol,
        CommunicationConfig communication,
        PlcCommunicationProfile profile,
        PlcSettingsValidationResult result)
    {
        if (string.IsNullOrWhiteSpace(profile.IpAddress))
        {
            result.AddConnectionError(protocol, "ipAddress", "PLC IP 地址不能为空。");
        }

        if (profile.Port <= 0 || profile.Port > 65535)
        {
            result.AddConnectionError(protocol, "port", "端口必须在 1-65535 之间。");
        }

        if (protocol != CommunicationConfig.ProtocolS7)
        {
            return;
        }

        var s7 = communication.S7 ?? S7CommunicationProfile.CreateDefault();
        if (string.IsNullOrWhiteSpace(s7.CpuType))
        {
            result.AddConnectionError(protocol, "cpuType", "S7 CPU 类型不能为空。");
        }

        if (s7.Rack < 0 || s7.Rack > 15)
        {
            result.AddConnectionError(protocol, "rack", "Rack 必须在 0-15 之间。");
        }

        if (s7.Slot < 0 || s7.Slot > 15)
        {
            result.AddConnectionError(protocol, "slot", "Slot 必须在 0-15 之间。");
        }
    }

    private static void ValidateMappings(
        string protocol,
        IReadOnlyList<PlcAddressMapping> mappings,
        PlcSettingsValidationResult result)
    {
        var parser = CreateAddressParser(protocol);
        var nameToIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var flaggedDuplicateIndexes = new HashSet<int>();

        for (var index = 0; index < mappings.Count; index++)
        {
            var mapping = mappings[index]?.Normalize() ?? new PlcAddressMapping();

            if (string.IsNullOrWhiteSpace(mapping.Name))
            {
                result.AddMappingError(protocol, index, "name", "变量名称不能为空。");
            }
            else if (nameToIndex.TryGetValue(mapping.Name, out var firstIndex))
            {
                if (flaggedDuplicateIndexes.Add(firstIndex))
                {
                    result.AddMappingError(protocol, firstIndex, "name", $"变量名称“{mapping.Name}”重复。");
                }

                result.AddMappingError(protocol, index, "name", $"变量名称“{mapping.Name}”重复。");
            }
            else
            {
                nameToIndex[mapping.Name] = index;
            }

            if (string.IsNullOrWhiteSpace(mapping.Address))
            {
                result.AddMappingError(protocol, index, "address", "PLC 地址不能为空。");
                continue;
            }

            var parseResult = parser.Parse(mapping.Address);
            if (!parseResult.IsSuccess || parseResult.Content == null)
            {
                result.AddMappingError(protocol, index, "address", parseResult.Message ?? "PLC 地址格式无效。");
                continue;
            }

            if (string.IsNullOrWhiteSpace(mapping.DataType))
            {
                result.AddMappingError(protocol, index, "dataType", "数据类型不能为空。");
                continue;
            }

            if (!IsDataTypeCompatible(protocol, mapping.DataType, parseResult.Content, out var message))
            {
                result.AddMappingError(protocol, index, "dataType", message);
            }
        }
    }

    private static IAddressParser CreateAddressParser(string protocol)
    {
        return protocol switch
        {
            CommunicationConfig.ProtocolMc => new McAddressParser(),
            CommunicationConfig.ProtocolFins => new FinsAddressParser(),
            _ => new S7AddressParser()
        };
    }

    private static bool IsDataTypeCompatible(
        string protocol,
        string dataType,
        PlcAddress address,
        out string message)
    {
        var normalizedDataType = NormalizeDataType(dataType);
        message = string.Empty;

        if (address.DataType == PlcDataType.Bit)
        {
            if (normalizedDataType is "BOOL" or "BIT")
            {
                return true;
            }

            message = "位地址只能映射为 Bool。";
            return false;
        }

        if (normalizedDataType is "BOOL" or "BIT")
        {
            message = "非位地址不能映射为 Bool。";
            return false;
        }

        if (address.DataType == PlcDataType.Byte && normalizedDataType is not ("BYTE" or "STRING"))
        {
            message = "字节地址仅支持 Byte 或 String。";
            return false;
        }

        if (protocol == CommunicationConfig.ProtocolS7
            && address.DataType == PlcDataType.DWord
            && normalizedDataType is not ("DWORD" or "UINT" or "INT32" or "INT" or "FLOAT"))
        {
            message = "S7 双字地址仅支持 DWord、Int32 或 Float。";
            return false;
        }

        return true;
    }

    private static string NormalizeDataType(string dataType)
    {
        return (dataType ?? string.Empty).Trim().ToUpperInvariant() switch
        {
            "BOOLEAN" => "BOOL",
            "SHORT" => "INT16",
            "INT" => "INT32",
            "USHORT" => "WORD",
            "UINT" => "DWORD",
            _ => (dataType ?? string.Empty).Trim().ToUpperInvariant()
        };
    }
}

public sealed class PlcSettingsValidationResult
{
    public List<PlcValidationIssue> Errors { get; } = new();

    public bool IsValid => Errors.Count == 0;

    public void AddConnectionError(string protocol, string field, string message)
    {
        Errors.Add(new PlcValidationIssue
        {
            Protocol = protocol,
            Section = "connection",
            Field = field,
            Message = message
        });
    }

    public void AddMappingError(string protocol, int index, string field, string message)
    {
        Errors.Add(new PlcValidationIssue
        {
            Protocol = protocol,
            Section = "mapping",
            Field = field,
            Index = index,
            Message = message
        });
    }
}

public sealed class PlcValidationIssue
{
    public string Protocol { get; set; } = string.Empty;
    public string Section { get; set; } = string.Empty;
    public string Field { get; set; } = string.Empty;
    public int? Index { get; set; }
    public string Message { get; set; } = string.Empty;
}
