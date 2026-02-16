namespace Acme.Product.Core.Enums;

/// <summary>
/// 用户角色枚举
/// </summary>
public enum UserRole
{
    /// <summary>
    /// 管理员 - 拥有全部权限，包括用户管理
    /// </summary>
    Admin = 0,

    /// <summary>
    /// 工程师 - 可编辑项目、运行、调试
    /// </summary>
    Engineer = 1,

    /// <summary>
    /// 操作员 - 只能运行已有项目，可只读浏览流程
    /// </summary>
    Operator = 2
}
