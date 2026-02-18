// OperatorRepository.cs
// 算子仓储实现
// 作者：蘅芜君

using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Acme.Product.Infrastructure.Repositories;

/// <summary>
/// 算子仓储实现
/// </summary>
public class OperatorRepository : RepositoryBase<Operator>, IOperatorRepository
{
    public OperatorRepository(Data.VisionDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<Operator>> GetByFlowIdAsync(Guid flowId)
    {
        // 注意：这里假设 Operator 有一个 FlowId 属性，但实际上当前实体没有
        // 实际实现可能需要通过 OperatorFlow 的导航属性来获取
        // 这里提供一个基本实现框架
        return await _dbSet
            .Where(o => !o.IsDeleted)
            .ToListAsync();
    }

    public async Task<IEnumerable<Operator>> GetByTypeAsync(OperatorType type)
    {
        return await _dbSet
            .Where(o => o.Type == type && !o.IsDeleted)
            .ToListAsync();
    }
}
