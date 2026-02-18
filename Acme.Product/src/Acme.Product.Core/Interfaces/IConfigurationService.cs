// IConfigurationService.cs
// 获取当前内存中的配置（同步方法，用于频繁访问）
// 作者：蘅芜君

using Acme.Product.Core.Entities;

namespace Acme.Product.Core.Interfaces;

/// <summary>
/// 应用配置服务接口
/// </summary>
public interface IConfigurationService
{
    /// <summary>
    /// 加载配置（若文件不存在则返回默认值）
    /// </summary>
    Task<AppConfig> LoadAsync();
    
    /// <summary>
    /// 保存配置到文件
    /// </summary>
    Task SaveAsync(AppConfig config);
    
    /// <summary>
    /// 获取当前内存中的配置（同步方法，用于频繁访问）
    /// </summary>
    AppConfig GetCurrent();
}
