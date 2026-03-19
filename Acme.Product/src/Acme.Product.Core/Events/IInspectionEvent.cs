// IInspectionEvent.cs
// 检测事件基础接口
// 作者：架构修复方案 v2

namespace Acme.Product.Core.Events;

/// <summary>
/// 检测事件基础接口
/// </summary>
public interface IInspectionEvent
{
    /// <summary>
    /// 项目ID
    /// </summary>
    Guid ProjectId { get; }
    
    /// <summary>
    /// 时间戳
    /// </summary>
    DateTimeOffset Timestamp { get; }
}
