// EntityTests.cs
// 检测结果实体测试
// 作者：蘅芜君

using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using FluentAssertions;

namespace Acme.Product.Tests.Entities;

/// <summary>
/// 工程实体测试
/// </summary>
public class ProjectTests
{
    [Fact]
    public void Constructor_WithValidName_ShouldCreateProject()
    {
        // Arrange
        var name = "测试工程";
        var description = "测试描述";

        // Act
        var project = new Project(name, description);

        // Assert
        project.Name.Should().Be(name);
        project.Description.Should().Be(description);
        project.Version.Should().Be("1.0.0");
        project.IsDeleted.Should().BeFalse();
        project.GlobalSettings.Should().BeEmpty();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithInvalidName_ShouldThrowException(string? invalidName)
    {
        // Act
        Action act = () => new Project(invalidName!);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("工程名称不能为空*");
    }

    [Fact]
    public void UpdateInfo_WithValidData_ShouldUpdateProject()
    {
        // Arrange
        var project = new Project("旧名称", "旧描述");
        var newName = "新名称";
        var newDescription = "新描述";

        // Act
        project.UpdateInfo(newName, newDescription);

        // Assert
        project.Name.Should().Be(newName);
        project.Description.Should().Be(newDescription);
        project.ModifiedAt.Should().NotBeNull();
    }

    [Fact]
    public void MarkAsDeleted_ShouldSetIsDeletedToTrue()
    {
        // Arrange
        var project = new Project("测试工程");

        // Act
        project.MarkAsDeleted();

        // Assert
        project.IsDeleted.Should().BeTrue();
        project.ModifiedAt.Should().NotBeNull();
    }

    [Fact]
    public void Restore_ShouldSetIsDeletedToFalse()
    {
        // Arrange
        var project = new Project("测试工程");
        project.MarkAsDeleted();

        // Act
        project.Restore();

        // Assert
        project.IsDeleted.Should().BeFalse();
    }

    [Fact]
    public void SetGlobalSetting_ShouldAddSetting()
    {
        // Arrange
        var project = new Project("测试工程");
        var key = "CameraId";
        var value = "CAM_001";

        // Act
        project.SetGlobalSetting(key, value);

        // Assert
        project.GlobalSettings.Should().ContainKey(key).WhoseValue.Should().Be(value);
    }

    [Fact]
    public void GetGlobalSetting_WithExistingKey_ShouldReturnValue()
    {
        // Arrange
        var project = new Project("测试工程");
        project.SetGlobalSetting("Key", "Value");

        // Act
        var value = project.GetGlobalSetting("Key");

        // Assert
        value.Should().Be("Value");
    }

    [Fact]
    public void GetGlobalSetting_WithNonExistingKey_ShouldReturnNull()
    {
        // Arrange
        var project = new Project("测试工程");

        // Act
        var value = project.GetGlobalSetting("NonExisting");

        // Assert
        value.Should().BeNull();
    }
}

/// <summary>
/// 算子实体测试
/// </summary>
public class OperatorTests
{
    [Fact]
    public void Constructor_WithValidData_ShouldCreateOperator()
    {
        // Arrange
        var name = "测试算子";
        var type = OperatorType.Filtering;
        var x = 100.0;
        var y = 200.0;

        // Act
        var op = new Operator(name, type, x, y);

        // Assert
        op.Name.Should().Be(name);
        op.Type.Should().Be(type);
        op.Position.X.Should().Be(x);
        op.Position.Y.Should().Be(y);
        op.IsEnabled.Should().BeTrue();
        op.ExecutionStatus.Should().Be(OperatorExecutionStatus.NotExecuted);
    }

    [Fact]
    public void UpdatePosition_ShouldUpdateCoordinates()
    {
        // Arrange
        var op = new Operator("测试", OperatorType.Filtering, 0, 0);
        var newX = 150.0;
        var newY = 250.0;

        // Act
        op.UpdatePosition(newX, newY);

        // Assert
        op.Position.X.Should().Be(newX);
        op.Position.Y.Should().Be(newY);
    }

    [Fact]
    public void AddInputPort_ShouldAddPortToList()
    {
        // Arrange
        var op = new Operator("测试", OperatorType.Filtering, 0, 0);

        // Act
        op.AddInputPort("Image", PortDataType.Image, true);

        // Assert
        op.InputPorts.Should().HaveCount(1);
        op.InputPorts.First().Name.Should().Be("Image");
        op.InputPorts.First().DataType.Should().Be(PortDataType.Image);
    }

    [Fact]
    public void AddOutputPort_ShouldAddPortToList()
    {
        // Arrange
        var op = new Operator("测试", OperatorType.Filtering, 0, 0);

        // Act
        op.AddOutputPort("Result", PortDataType.Image);

        // Assert
        op.OutputPorts.Should().HaveCount(1);
        op.OutputPorts.First().Name.Should().Be("Result");
    }

    [Fact]
    public void MarkExecutionStarted_ShouldSetStatusToExecuting()
    {
        // Arrange
        var op = new Operator("测试", OperatorType.Filtering, 0, 0);

        // Act
        op.MarkExecutionStarted();

        // Assert
        op.ExecutionStatus.Should().Be(OperatorExecutionStatus.Executing);
        op.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void MarkExecutionCompleted_ShouldSetStatusToSuccess()
    {
        // Arrange
        var op = new Operator("测试", OperatorType.Filtering, 0, 0);
        op.MarkExecutionStarted();

        // Act
        op.MarkExecutionCompleted(100);

        // Assert
        op.ExecutionStatus.Should().Be(OperatorExecutionStatus.Success);
        op.ExecutionTimeMs.Should().Be(100);
    }

    [Fact]
    public void MarkExecutionFailed_ShouldSetStatusToFailed()
    {
        // Arrange
        var op = new Operator("测试", OperatorType.Filtering, 0, 0);
        var errorMessage = "执行失败";

        // Act
        op.MarkExecutionFailed(errorMessage);

        // Assert
        op.ExecutionStatus.Should().Be(OperatorExecutionStatus.Failed);
        op.ErrorMessage.Should().Be(errorMessage);
    }

    [Fact]
    public void Disable_ShouldSetIsEnabledToFalse()
    {
        // Arrange
        var op = new Operator("测试", OperatorType.Filtering, 0, 0);

        // Act
        op.Disable();

        // Assert
        op.IsEnabled.Should().BeFalse();
    }
}

/// <summary>
/// 检测结果实体测试
/// </summary>
public class InspectionResultTests
{
    [Fact]
    public void Constructor_ShouldCreateResultWithDefaultValues()
    {
        // Arrange
        var projectId = Guid.NewGuid();

        // Act
        var result = new InspectionResult(projectId);

        // Assert
        result.ProjectId.Should().Be(projectId);
        result.Status.Should().Be(InspectionStatus.NotInspected);
        result.Defects.Should().BeEmpty();
    }

    [Fact]
    public void AddDefect_ShouldAddDefectToList()
    {
        // Arrange
        var result = new InspectionResult(Guid.NewGuid());
        var defect = new Defect(
            result.Id,
            DefectType.Scratch,
            10, 20, 100, 50,
            0.95,
            "划痕缺陷"
        );

        // Act
        result.AddDefect(defect);

        // Assert
        result.Defects.Should().HaveCount(1);
        result.Defects.First().Type.Should().Be(DefectType.Scratch);
    }

    [Fact]
    public void SetResult_WithOKStatus_ShouldUpdateStatus()
    {
        // Arrange
        var result = new InspectionResult(Guid.NewGuid());

        // Act
        result.SetResult(InspectionStatus.OK, 150, 0.98);

        // Assert
        result.Status.Should().Be(InspectionStatus.OK);
        result.ProcessingTimeMs.Should().Be(150);
        result.ConfidenceScore.Should().Be(0.98);
    }

    [Fact]
    public void IsOK_WithNoDefectsAndOKStatus_ShouldReturnTrue()
    {
        // Arrange
        var result = new InspectionResult(Guid.NewGuid());
        result.SetResult(InspectionStatus.OK, 100);

        // Act
        var isOk = result.IsOK;

        // Assert
        isOk.Should().BeTrue();
    }

    [Fact]
    public void IsOK_WithDefects_ShouldReturnFalse()
    {
        // Arrange
        var result = new InspectionResult(Guid.NewGuid());
        result.SetResult(InspectionStatus.NG, 100);
        result.AddDefect(new Defect(
            result.Id, DefectType.Scratch, 0, 0, 10, 10, 0.9
        ));

        // Act
        var isOk = result.IsOK;

        // Assert
        isOk.Should().BeFalse();
    }

    [Fact]
    public void GetNGCount_ShouldReturnDefectCount()
    {
        // Arrange
        var result = new InspectionResult(Guid.NewGuid());
        result.AddDefect(new Defect(result.Id, DefectType.Scratch, 0, 0, 10, 10, 0.9));
        result.AddDefect(new Defect(result.Id, DefectType.Stain, 0, 0, 10, 10, 0.8));

        // Act
        var count = result.GetNGCount();

        // Assert
        count.Should().Be(2);
    }
}
