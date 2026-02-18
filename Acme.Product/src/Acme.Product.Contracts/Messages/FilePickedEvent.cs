// FilePickedEvent.cs
// 是否取消了选择
// 作者：蘅芜君

namespace Acme.Product.Contracts.Messages;

/// <summary>
/// 文件选择完成事件
/// </summary>
public class FilePickedEvent : EventBase
{
    public FilePickedEvent()
    {
        MessageType = nameof(FilePickedEvent);
    }

    /// <summary>
    /// 参数名称
    /// </summary>
    public string ParameterName { get; set; } = string.Empty;

    /// <summary>
    /// 选择的文件路径
    /// </summary>
    public string? FilePath { get; set; }

    /// <summary>
    /// 是否取消了选择
    /// </summary>
    public bool IsCancelled { get; set; }
}
