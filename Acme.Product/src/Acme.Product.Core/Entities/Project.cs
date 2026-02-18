// Project.cs
// 更新算子流程
// 作者：蘅芜君

using Acme.Product.Core.Entities.Base;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Events;
using Acme.Product.Core.ValueObjects;

namespace Acme.Product.Core.Entities;

/// <summary>
/// 视觉检测工程实体 - 聚合根
/// </summary>
public class Project : AggregateRoot
{
    /// <summary>
    /// 工程名称
    /// </summary>
    public string Name { get; private set; }

    /// <summary>
    /// 工程描述
    /// </summary>
    public string? Description { get; private set; }

    /// <summary>
    /// 工程版本
    /// </summary>
    public string Version { get; private set; }

    /// <summary>
    /// 算子流程
    /// </summary>
    public OperatorFlow Flow { get; private set; }

    /// <summary>
    /// 全局配置参数
    /// </summary>
    public Dictionary<string, string> GlobalSettings { get; private set; }

    /// <summary>
    /// 最后打开时间
    /// </summary>
    public DateTime? LastOpenedAt { get; private set; }

    private Project()
    {
        Name = string.Empty;
        Version = "1.0.0";
        // 使用相同 ID 以满足 Table Splitting 要求
        Flow = new OperatorFlow(Id, "默认流程");
        GlobalSettings = new Dictionary<string, string>();
    }

    public Project(string name, string? description = null) : this()
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("工程名称不能为空", nameof(name));

        Name = name;
        Description = description;
        ModifiedAt = DateTime.UtcNow;

        AddDomainEvent(new ProjectCreatedEvent(Id, Name));
    }

    /// <summary>
    /// 更新工程信息
    /// </summary>
    public void UpdateInfo(string name, string? description)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("工程名称不能为空", nameof(name));

        Name = name;
        Description = description;
        MarkAsModified();

        AddDomainEvent(new ProjectUpdatedEvent(Id, Name));
    }

    /// <summary>
    /// 更新版本号
    /// </summary>
    public void UpdateVersion(string version)
    {
        Version = version ?? throw new ArgumentNullException(nameof(version));
        MarkAsModified();
    }

    /// <summary>
    /// 记录打开时间
    /// </summary>
    public void RecordOpen()
    {
        LastOpenedAt = DateTime.UtcNow;
        MarkAsModified();
    }

    /// <summary>
    /// 设置全局配置
    /// </summary>
    public void SetGlobalSetting(string key, string value)
    {
        GlobalSettings[key] = value;
        MarkAsModified();
    }

    /// <summary>
    /// 获取全局配置
    /// </summary>
    public string? GetGlobalSetting(string key)
    {
        return GlobalSettings.TryGetValue(key, out var value) ? value : null;
    }

    /// <summary>
    /// 更新算子流程
    /// </summary>
    public void UpdateFlow(OperatorFlow newFlow)
    {
        if (newFlow == null)
            throw new ArgumentNullException(nameof(newFlow));

        Flow = newFlow;
        MarkAsModified();
    }
}
