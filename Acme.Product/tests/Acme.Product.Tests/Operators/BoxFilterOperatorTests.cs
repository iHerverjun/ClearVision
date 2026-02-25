using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.ValueObjects;
using Acme.Product.Infrastructure.Operators;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Acme.Product.Tests.Operators;

public class BoxFilterOperatorTests
{
    private readonly BoxFilterOperator _operator;

    public BoxFilterOperatorTests()
    {
        _operator = new BoxFilterOperator(Substitute.For<ILogger<BoxFilterOperator>>());
    }

    [Fact]
    public void OperatorType_ShouldBeBoxFilter()
    {
        Assert.Equal(OperatorType.BoxFilter, _operator.OperatorType);
    }

    [Fact]
    public async Task ExecuteAsync_WithClassFilter_ShouldReturnTargetClassOnly()
    {
        var op = CreateOperator(new Dictionary<string, object>
        {
            { "FilterMode", "Class" },
            { "TargetClasses", "defect" }
        });

        var detections = new DetectionList(new[]
        {
            new DetectionResult("defect", 0.9f, 10, 10, 20, 20),
            new DetectionResult("ok", 0.8f, 40, 40, 20, 20)
        });

        var result = await _operator.ExecuteAsync(op, new Dictionary<string, object> { { "Detections", detections } });

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.OutputData);
        Assert.Equal(1, (int)result.OutputData!["Count"]);
    }

    [Fact]
    public void ValidateParameters_WithInvalidMode_ShouldReturnInvalid()
    {
        var op = CreateOperator(new Dictionary<string, object> { { "FilterMode", "Invalid" } });

        var validation = _operator.ValidateParameters(op);

        Assert.False(validation.IsValid);
    }

    private static Operator CreateOperator(Dictionary<string, object>? parameters = null)
    {
        var op = new Operator("BoxFilter", OperatorType.BoxFilter, 0, 0);

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
