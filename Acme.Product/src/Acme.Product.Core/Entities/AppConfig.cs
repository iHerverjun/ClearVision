// AppConfig.cs
// 应用程序全局配置
// 作者：蘅芜君

namespace Acme.Product.Core.Entities;

/// <summary>
/// 应用程序全局配置
/// </summary>
public class AppConfig
{
    /// <summary>
    /// 常规设置
    /// </summary>
    public GeneralConfig General { get; set; } = new();

    /// <summary>
    /// 硬件通讯设置
    /// </summary>
    public CommunicationConfig Communication { get; set; } = new();

    /// <summary>
    /// 数据存储设置
    /// </summary>
    public StorageConfig Storage { get; set; } = new();

    /// <summary>
    /// 运行时参数
    /// </summary>
    public RuntimeConfig Runtime { get; set; } = new();

    /// <summary>
    /// 相机硬件绑定配置列表
    /// </summary>
    public List<CameraBindingConfig> Cameras { get; set; } = new();

    /// <summary>
    /// 安全策略配置
    /// </summary>
    public SecurityConfig Security { get; set; } = new();

    /// <summary>
    /// 当前活动相机的逻辑ID
    /// </summary>
    public string ActiveCameraId { get; set; } = "";
}

public class GeneralConfig
{
    /// <summary>
    /// 软件标题
    /// </summary>
    public string SoftwareTitle { get; set; } = "ClearVision 检测站";

    /// <summary>
    /// 界面主题：dark / light
    /// </summary>
    public string Theme { get; set; } = "dark";

    /// <summary>
    /// 是否开机自启动
    /// </summary>
    public bool AutoStart { get; set; } = false;
}

public class CommunicationConfig
{
    /// <summary>
    /// PLC IP 地址
    /// </summary>
    public string PlcIpAddress { get; set; } = "192.168.1.100";

    /// <summary>
    /// PLC 端口号
    /// </summary>
    public int PlcPort { get; set; } = 502;

    /// <summary>
    /// 通讯协议
    /// </summary>
    public string Protocol { get; set; } = "ModbusTcp";

    /// <summary>
    /// 心跳检测间隔（毫秒）
    /// </summary>
    public int HeartbeatIntervalMs { get; set; } = 1000;

    public List<PlcAddressMapping> Mappings { get; set; } = new();
}

public class PlcAddressMapping
{
    public string Name { get; set; } = "";
    public string Address { get; set; } = "";
    public string DataType { get; set; } = "Bool";
    public string Description { get; set; } = "";
    public bool CanWrite { get; set; }
}

public class StorageConfig
{
    /// <summary>
    /// 图片保存根目录
    /// </summary>
    public string ImageSavePath { get; set; } = @"D:\VisionData\Images";

    /// <summary>
    /// 保存策略
    /// </summary>
    public string SavePolicy { get; set; } = "NgOnly";

    /// <summary>
    /// 图片保留天数
    /// </summary>
    public int RetentionDays { get; set; } = 30;

    /// <summary>
    /// 磁盘空间下限阈值 (GB)
    /// </summary>
    public int MinFreeSpaceGb { get; set; } = 5;
}

public class RuntimeConfig
{
    /// <summary>
    /// 是否自动开始检测
    /// </summary>
    public bool AutoRun { get; set; } = false;

    /// <summary>
    /// 连续 NG 停机阈值
    /// </summary>
    public int StopOnConsecutiveNg { get; set; } = 0;

    /// <summary>
    /// 缺料等待超时（秒）
    /// </summary>
    public int MissingMaterialTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// 是否启用运行保护规则
    /// </summary>
    public bool ApplyProtectionRules { get; set; } = true;
}

public class SecurityConfig
{
    /// <summary>
    /// 密码最小长度
    /// </summary>
    public int PasswordMinLength { get; set; } = 6;

    /// <summary>
    /// 会话自动超时（分钟）
    /// </summary>
    public int SessionTimeoutMinutes { get; set; } = 30;

    /// <summary>
    /// 登录失败锁定次数
    /// </summary>
    public int LoginFailureLockoutCount { get; set; } = 5;
}

/// <summary>
/// 相机硬件绑定配置 - 描述一台物理相机到逻辑名称的映射
/// </summary>
public class CameraBindingConfig
{
    /// <summary>
    /// 逻辑ID（由系统生成的唯一标识）
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];

    /// <summary>
    /// 显示名称
    /// </summary>
    public string DisplayName { get; set; } = "Camera";

    /// <summary>
    /// 序列号
    /// </summary>
    public string SerialNumber { get; set; } = "";

    /// <summary>
    /// IP地址
    /// </summary>
    public string IpAddress { get; set; } = "";

    /// <summary>
    /// 制造商
    /// </summary>
    public string Manufacturer { get; set; } = "Huaray";

    /// <summary>
    /// 型号名称
    /// </summary>
    public string ModelName { get; set; } = "";

    /// <summary>
    /// 接口类型（USB3 / GigE）
    /// </summary>
    public string InterfaceType { get; set; } = "";

    /// <summary>
    /// 是否启用此配置
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// 曝光时间（微秒）
    /// </summary>
    public double ExposureTimeUs { get; set; } = 5000.0;

    /// <summary>
    /// 增益（dB）
    /// </summary>
    public double GainDb { get; set; } = 1.0;

    /// <summary>
    /// 触发模式（Software / Continuous / Hardware）
    /// </summary>
    public string TriggerMode { get; set; } = "Software";
}
