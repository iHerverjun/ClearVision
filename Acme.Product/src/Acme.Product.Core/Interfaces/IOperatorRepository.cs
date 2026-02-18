// IOperatorRepository.cs
// 根据类型获取算子
// 作者：蘅芜君

using Acme.Product.Core.Entities;

namespace Acme.Product.Core.Interfaces;

/// <summary>
/// 算子仓储接口
/// </summary>
public interface IOperatorRepository : IRepository<Operator>
{
    /// <summary>
    /// 根据流程ID获取算子列表
    /// </summary>
    Task<IEnumerable<Operator>> GetByFlowIdAsync(Guid flowId);

    /// <summary>
    /// 根据类型获取算子
    /// </summary>
    Task<IEnumerable<Operator>> GetByTypeAsync(Enums.OperatorType type);
}
