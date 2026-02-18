// VisionEvents.cs
// 检测完成事件
// 作者：蘅芜君

using Acme.Product.Core.Events;

namespace Acme.Product.Core.Events;

/// <summary>
/// 工程创建事件
/// </summary>
public class ProjectCreatedEvent : DomainEventBase
{
    public Guid ProjectId { get; }
    public string ProjectName { get; }

    public override Guid AggregateId => ProjectId;

    public ProjectCreatedEvent(Guid projectId, string projectName)
    {
        ProjectId = projectId;
        ProjectName = projectName ?? throw new ArgumentNullException(nameof(projectName));
    }
}

/// <summary>
/// 工程更新事件
/// </summary>
public class ProjectUpdatedEvent : DomainEventBase
{
    public Guid ProjectId { get; }
    public string ProjectName { get; }

    public override Guid AggregateId => ProjectId;

    public ProjectUpdatedEvent(Guid projectId, string projectName)
    {
        ProjectId = projectId;
        ProjectName = projectName ?? throw new ArgumentNullException(nameof(projectName));
    }
}

/// <summary>
/// 算子执行完成事件
/// </summary>
public class OperatorExecutedEvent : DomainEventBase
{
    public Guid OperatorId { get; }
    public string OperatorName { get; }
    public long ExecutionTimeMs { get; }
    public bool IsSuccess { get; }

    public override Guid AggregateId => OperatorId;

    public OperatorExecutedEvent(Guid operatorId, string operatorName, long executionTimeMs, bool isSuccess)
    {
        OperatorId = operatorId;
        OperatorName = operatorName ?? throw new ArgumentNullException(nameof(operatorName));
        ExecutionTimeMs = executionTimeMs;
        IsSuccess = isSuccess;
    }
}

/// <summary>
/// 检测完成事件
/// </summary>
public class InspectionCompletedEvent : DomainEventBase
{
    public Guid ProjectId { get; }
    public Guid InspectionId { get; }
    public string Status { get; }
    public int DefectCount { get; }

    public override Guid AggregateId => InspectionId;

    public InspectionCompletedEvent(Guid projectId, Guid inspectionId, string status, int defectCount)
    {
        ProjectId = projectId;
        InspectionId = inspectionId;
        Status = status ?? throw new ArgumentNullException(nameof(status));
        DefectCount = defectCount;
    }
}
