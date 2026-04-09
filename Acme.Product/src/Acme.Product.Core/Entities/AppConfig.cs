using System.Text.Json.Serialization;

namespace Acme.Product.Core.Entities;

public class AppConfig
{
    public GeneralConfig General { get; set; } = new();

    public CommunicationConfig Communication { get; set; } = new();

    public StorageConfig Storage { get; set; } = new();

    public RuntimeConfig Runtime { get; set; } = new();

    public List<CameraBindingConfig> Cameras { get; set; } = new();

    public SecurityConfig Security { get; set; } = new();

    public string ActiveCameraId { get; set; } = string.Empty;

    public void Normalize()
    {
        General ??= new GeneralConfig();
        General.Normalize();
        Communication ??= new CommunicationConfig();
        Communication.Normalize();
        Storage ??= new StorageConfig();
        Runtime ??= new RuntimeConfig();
        Cameras ??= new List<CameraBindingConfig>();
        Security ??= new SecurityConfig();
        ActiveCameraId ??= string.Empty;
    }
}

public class GeneralConfig
{
    public const string ThemeDark = "dark";
    public const string ThemeLight = "light";

    public string SoftwareTitle { get; set; } = "ClearVision 检测站";

    public string Theme { get; set; } = ThemeDark;

    public bool AutoStart { get; set; }

    public void Normalize()
    {
        Theme = NormalizeTheme(Theme);
    }

    public static string NormalizeTheme(string? theme)
    {
        var candidate = (theme ?? string.Empty).Trim().ToLowerInvariant();
        return candidate switch
        {
            ThemeLight => ThemeLight,
            ThemeDark => ThemeDark,
            _ => ThemeDark
        };
    }
}

public class CommunicationConfig
{
    public const string ProtocolS7 = "S7";
    public const string ProtocolMc = "MC";
    public const string ProtocolFins = "FINS";

    private const int DefaultHeartbeatIntervalMs = 1000;
    private const int DefaultS7Port = 102;
    private const int DefaultMcPort = 5002;
    private const int DefaultFinsPort = 9600;

    public string ActiveProtocol { get; set; } = ProtocolS7;

    public int HeartbeatIntervalMs { get; set; } = DefaultHeartbeatIntervalMs;

    public S7CommunicationProfile S7 { get; set; } = S7CommunicationProfile.CreateDefault();

    public PlcCommunicationProfile Mc { get; set; } = PlcCommunicationProfile.CreateDefault(DefaultMcPort);

    public PlcCommunicationProfile Fins { get; set; } = PlcCommunicationProfile.CreateDefault(DefaultFinsPort);

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string? PlcIpAddress { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int PlcPort { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string? Protocol { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string? IpAddress { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int Port { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public List<PlcAddressMapping>? Mappings { get; set; }

    public void Normalize()
    {
        ActiveProtocol = NormalizeProtocolKey(ActiveProtocol, Protocol);
        HeartbeatIntervalMs = HeartbeatIntervalMs > 0 ? HeartbeatIntervalMs : DefaultHeartbeatIntervalMs;

        S7 ??= S7CommunicationProfile.CreateDefault();
        S7.Normalize(DefaultS7Port);

        Mc ??= PlcCommunicationProfile.CreateDefault(DefaultMcPort);
        Mc.Normalize(DefaultMcPort);

        Fins ??= PlcCommunicationProfile.CreateDefault(DefaultFinsPort);
        Fins.Normalize(DefaultFinsPort);

        ApplyLegacyMigration();
    }

    public PlcCommunicationProfile GetProfile(string? protocol = null)
    {
        return NormalizeProtocolKey(protocol, ActiveProtocol) switch
        {
            ProtocolMc => Mc,
            ProtocolFins => Fins,
            _ => S7
        };
    }

    public List<PlcAddressMapping> GetMappings(string? protocol = null)
    {
        return GetProfile(protocol).Mappings;
    }

    public void SetMappings(string? protocol, IEnumerable<PlcAddressMapping>? mappings)
    {
        GetProfile(protocol).Mappings = NormalizeMappings(mappings);
    }

    public static string NormalizeProtocolKey(string? protocol, string? fallback = null)
    {
        var candidate = (protocol ?? fallback ?? ProtocolS7).Trim();
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return ProtocolS7;
        }

        return candidate.ToUpperInvariant() switch
        {
            "S7" or "SIEMENSS7" => ProtocolS7,
            "MC" or "MITSUBISHIMC" => ProtocolMc,
            "FINS" or "OMRONFINS" => ProtocolFins,
            _ => ProtocolS7
        };
    }

    public static int GetDefaultPort(string protocol)
    {
        return NormalizeProtocolKey(protocol) switch
        {
            ProtocolMc => DefaultMcPort,
            ProtocolFins => DefaultFinsPort,
            _ => DefaultS7Port
        };
    }

    public static List<PlcAddressMapping> NormalizeMappings(IEnumerable<PlcAddressMapping>? mappings)
    {
        if (mappings == null)
        {
            return new List<PlcAddressMapping>();
        }

        var normalized = new List<PlcAddressMapping>();
        foreach (var item in mappings)
        {
            if (item == null)
            {
                continue;
            }

            var mapping = item.Normalize();
            if (mapping.IsEmpty())
            {
                continue;
            }

            normalized.Add(mapping);
        }

        return normalized;
    }

    private void ApplyLegacyMigration()
    {
        var legacyIpAddress = FirstNonEmpty(PlcIpAddress, IpAddress);
        var legacyPort = PlcPort > 0 ? PlcPort : Port;
        var hasLegacyMappings = Mappings is { Count: > 0 };
        var hasLegacyConnection = !string.IsNullOrWhiteSpace(legacyIpAddress)
            || legacyPort > 0
            || !string.IsNullOrWhiteSpace(Protocol);

        if (!hasLegacyConnection && !hasLegacyMappings)
        {
            ClearLegacyFields();
            return;
        }

        var targetProtocol = NormalizeProtocolKey(Protocol, ActiveProtocol);
        var targetProfile = GetProfile(targetProtocol);

        if (!string.IsNullOrWhiteSpace(legacyIpAddress))
        {
            targetProfile.IpAddress = legacyIpAddress.Trim();
        }

        if (legacyPort > 0 && legacyPort <= 65535)
        {
            targetProfile.Port = legacyPort;
        }

        if (hasLegacyMappings)
        {
            targetProfile.Mappings = NormalizeMappings(Mappings);
        }

        targetProfile.Normalize(GetDefaultPort(targetProtocol));
        ActiveProtocol = targetProtocol;
        ClearLegacyFields();
    }

    private void ClearLegacyFields()
    {
        PlcIpAddress = null;
        PlcPort = 0;
        Protocol = null;
        IpAddress = null;
        Port = 0;
        Mappings = null;
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return null;
    }
}

public class PlcAddressMapping
{
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string DataType { get; set; } = "Bool";
    public string Description { get; set; } = string.Empty;
    public bool CanWrite { get; set; }

    public PlcAddressMapping Normalize()
    {
        return new PlcAddressMapping
        {
            Name = (Name ?? string.Empty).Trim(),
            Address = (Address ?? string.Empty).Trim(),
            DataType = string.IsNullOrWhiteSpace(DataType) ? "Bool" : DataType.Trim(),
            Description = (Description ?? string.Empty).Trim(),
            CanWrite = CanWrite
        };
    }

    public bool IsEmpty()
    {
        return string.IsNullOrWhiteSpace(Name)
            && string.IsNullOrWhiteSpace(Address)
            && string.IsNullOrWhiteSpace(Description);
    }
}

public class PlcCommunicationProfile
{
    public string IpAddress { get; set; } = string.Empty;
    public int Port { get; set; }
    public List<PlcAddressMapping> Mappings { get; set; } = new();

    public virtual void Normalize(int defaultPort)
    {
        IpAddress = (IpAddress ?? string.Empty).Trim();
        Port = Port == 0 ? defaultPort : Port;
        Mappings = CommunicationConfig.NormalizeMappings(Mappings);
    }

    public static PlcCommunicationProfile CreateDefault(int defaultPort)
    {
        return new PlcCommunicationProfile
        {
            Port = defaultPort,
            Mappings = new List<PlcAddressMapping>()
        };
    }
}

public sealed class S7CommunicationProfile : PlcCommunicationProfile
{
    public string CpuType { get; set; } = "S7-1200";
    public int Rack { get; set; }
    public int Slot { get; set; } = 1;

    public override void Normalize(int defaultPort)
    {
        base.Normalize(defaultPort);
        CpuType = string.IsNullOrWhiteSpace(CpuType) ? "S7-1200" : CpuType.Trim();
    }

    public static S7CommunicationProfile CreateDefault()
    {
        return new S7CommunicationProfile
        {
            Port = 102,
            CpuType = "S7-1200",
            Rack = 0,
            Slot = 1,
            Mappings = new List<PlcAddressMapping>()
        };
    }
}

public class StorageConfig
{
    public string ImageSavePath { get; set; } = @"D:\VisionData\Images";

    public string SavePolicy { get; set; } = "NgOnly";

    public int RetentionDays { get; set; } = 30;

    public int MinFreeSpaceGb { get; set; } = 5;
}

public class RuntimeConfig
{
    public bool AutoRun { get; set; }

    public int StopOnConsecutiveNg { get; set; }

    public int MissingMaterialTimeoutSeconds { get; set; } = 30;

    public bool ApplyProtectionRules { get; set; } = true;
}

public class SecurityConfig
{
    public int PasswordMinLength { get; set; } = 6;

    public int SessionTimeoutMinutes { get; set; } = 30;

    public int LoginFailureLockoutCount { get; set; } = 5;
}

public class CameraBindingConfig
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];

    public string DisplayName { get; set; } = "Camera";

    public string SerialNumber { get; set; } = string.Empty;

    public string IpAddress { get; set; } = string.Empty;

    public string Manufacturer { get; set; } = "Huaray";

    public string ModelName { get; set; } = string.Empty;

    public string InterfaceType { get; set; } = string.Empty;

    public bool IsEnabled { get; set; } = true;

    public double ExposureTimeUs { get; set; } = 5000.0;

    public double GainDb { get; set; } = 1.0;

    public string TriggerMode { get; set; } = "Software";
}
