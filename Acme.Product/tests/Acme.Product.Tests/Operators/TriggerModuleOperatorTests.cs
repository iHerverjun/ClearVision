using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.ValueObjects;
using Acme.Product.Infrastructure.Operators;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Acme.Product.Tests.Operators;

[Trait("Category", "Sprint5_Phase2")]
public class TriggerModuleOperatorTests
{
    [Fact]
    public void OperatorType_ShouldBeTriggerModule()
    {
        var sut = CreateSut();
        Assert.Equal(OperatorType.TriggerModule, sut.OperatorType);
    }

    [Fact]
    public async Task ExecuteAsync_TimerMode_ShouldRespectInterval()
    {
        var sut = CreateSut();
        var op = CreateOperator(new Dictionary<string, object>
        {
            { "TriggerMode", "Timer" },
            { "Interval", 1000 },
            { "AutoRepeat", true }
        });

        var first = await sut.ExecuteAsync(op, null);
        var second = await sut.ExecuteAsync(op, null);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.NotNull(first.OutputData);
        Assert.NotNull(second.OutputData);
        Assert.True((bool)first.OutputData!["Triggered"]);
        Assert.False((bool)second.OutputData!["Triggered"]);
        Assert.Equal(1, Convert.ToInt32(second.OutputData["TriggerCount"]));
    }

    [Fact]
    public void ValidateParameters_WithInvalidMode_ShouldReturnInvalid()
    {
        var sut = CreateSut();
        var op = CreateOperator(new Dictionary<string, object> { { "TriggerMode", "InvalidMode" } });

        var validation = sut.ValidateParameters(op);

        Assert.False(validation.IsValid);
    }

    private static TriggerModuleOperator CreateSut()
    {
        return new TriggerModuleOperator(Substitute.For<ILogger<TriggerModuleOperator>>());
    }

    private static Operator CreateOperator(Dictionary<string, object>? parameters = null)
    {
        var op = new Operator("Trigger", OperatorType.TriggerModule, 0, 0);

        if (parameters != null)
        {
            foreach (var (name, value) in parameters)
            {
                op.AddParameter(new Parameter(Guid.NewGuid(), name, name, string.Empty, "string", value));
            }
        }

        return op;
    }
}
