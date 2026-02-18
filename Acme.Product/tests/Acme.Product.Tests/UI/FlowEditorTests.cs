// FlowEditorTests.cs
// 流程编辑器 UI 测试
// 作者：蘅芜君

using FluentAssertions;
using Xunit;

namespace Acme.Product.Tests.UI;

/// <summary>
/// 流程编辑器 UI 测试
/// Sprint 5: S5-007 实现
/// </summary>
public class FlowEditorTests : UITestBase
{
    [Fact(Skip = "需要运行中的应用程序")]
    public async Task FlowEditor_PageLoad_ShouldDisplayCanvas()
    {
        // Arrange
        await NavigateToAppAsync();
        
        // Act
        var canvas = await Page!.QuerySelectorAsync("#flow-canvas");
        
        // Assert
        canvas.Should().NotBeNull();
    }

    [Fact(Skip = "需要运行中的应用程序")]
    public async Task FlowEditor_DragOperator_ShouldCreateNode()
    {
        // Arrange
        await NavigateToAppAsync();
        
        // Act - 从算子库拖拽到画布
        var operatorItem = await Page!.QuerySelectorAsync(".operator-item");
        var canvas = await Page.QuerySelectorAsync("#flow-canvas");
        
        if (operatorItem != null && canvas != null)
        {
            var box = await canvas.BoundingBoxAsync();
            if (box != null)
            {
                // 使用简单的拖拽操作
                await operatorItem.ClickAsync();
                await canvas.ClickAsync();
            }
        }
        
        // Assert
        var nodes = await Page.QuerySelectorAllAsync(".operator-node");
        nodes.Count.Should().BeGreaterThan(0);
    }
}
