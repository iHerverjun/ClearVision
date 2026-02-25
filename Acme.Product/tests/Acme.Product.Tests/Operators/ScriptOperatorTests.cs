using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.ValueObjects;
using Acme.Product.Infrastructure.Operators;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Acme.Product.Tests.Operators;

[Trait("Category", "Sprint5_Phase2")]
public class ScriptOperatorTests
{
    [Fact]
    public void OperatorType_ShouldBeScriptOperator()
    {
        var sut = CreateSut();
        Assert.Equal(OperatorType.ScriptOperator, sut.OperatorType);
    }

    [Fact]
    public async Task ExecuteAsync_WithValidExpression_ShouldReturnCalculatedOutputs()
    {
        var sut = CreateSut();
        var op = CreateOperator(new Dictionary<string, object>
        {
            { "ScriptLanguage", "CSharpExpression" },
            { "Code", "sum = Input1 + Input2; Output1 = sum; Output2 = sum * 2" },
            { "Timeout", 1000 }
        });

        var inputs = new Dictionary<string, object>
        {
            { "Input1", 2.0 },
            { "Input2", 3.0 }
        };

        var result = await sut.ExecuteAsync(op, inputs);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.OutputData);
        Assert.Equal(5.0, Convert.ToDouble(result.OutputData!["Output1"]), 6);
        Assert.Equal(10.0, Convert.ToDouble(result.OutputData["Output2"]), 6);
    }

    [Fact]
    public void ValidateParameters_WithInvalidLanguage_ShouldReturnInvalid()
    {
        var sut = CreateSut();
        var op = CreateOperator(new Dictionary<string, object>
        {
            { "ScriptLanguage", "Python" },
            { "Code", "Output1 = 1" }
        });

        var validation = sut.ValidateParameters(op);

        Assert.False(validation.IsValid);
    }

    private static ScriptOperator CreateSut()
    {
        return new ScriptOperator(Substitute.For<ILogger<ScriptOperator>>());
    }

    private static Operator CreateOperator(Dictionary<string, object>? parameters = null)
    {
        var op = new Operator("Script", OperatorType.ScriptOperator, 0, 0);

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
