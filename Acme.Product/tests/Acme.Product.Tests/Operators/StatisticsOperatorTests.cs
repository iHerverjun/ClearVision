// StatisticsOperatorTests.cs
// StatisticsOperator 单元测试 - 验证 CPK/CP 计算
// 作者：蘅芜君

using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Acme.Product.Infrastructure.Operators;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Acme.Product.Tests.Operators;

public class StatisticsOperatorTests
{
    private readonly ILogger<StatisticsOperator> _loggerMock;
    private readonly StatisticsOperator _operator;

    public StatisticsOperatorTests()
    {
        _loggerMock = Substitute.For<ILogger<StatisticsOperator>>();
        _operator = new StatisticsOperator(_loggerMock);

        // 由于 _historyValues 是静态的，测试前需要清理
        typeof(StatisticsOperator)
            .GetField("_historyValues", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
            ?.SetValue(null, new List<double>());
    }

    [Fact]
    public async Task ExecuteAsync_BasicStats_ReturnsCorrectResults()
    {
        // 准备数据
        var inputs1 = new Dictionary<string, object> { { "Value", 10.0 } };
        var inputs2 = new Dictionary<string, object> { { "Value", 20.0 } };
        var op = CreateOperator();

        // 执行
        await _operator.ExecuteAsync(op, inputs1);
        var result = await _operator.ExecuteAsync(op, inputs2);

        // 验证
        Assert.True(result.IsSuccess);
        Assert.Equal(15.0, (double)result.OutputData!["Mean"]);
        Assert.Equal(2, (int)result.OutputData["Count"]);
        // 10 和 20 的标准差为 5.0 (样本标准差，N-1 基准)
        // Mean = 15, Variance = ((10-15)^2 + (20-15)^2)/(2-1) = (25+25)/1 = 50
        // StdDev = sqrt(50) ≈ 7.071
        // 注意：之前的代码实现中可能是用了 N 还是 N-1？
        // 查阅 StatisticsOperator.cs
        Assert.Equal(Math.Sqrt(50), (double)result.OutputData["StdDev"], 3);
    }

    [Fact]
    public async Task ExecuteAsync_WithCpkParams_CalculatesCpkCorrectly()
    {
        // 场景：均值 10.0, 标准差 1.0, USL=13.0, LSL=7.0
        var op = CreateOperator(new Dictionary<string, object>
        {
            { "USL", 13.0 },
            { "LSL", 7.0 }
        });

        // 构造均值为 10，标准差约为 1 的序列: 9.29, 10.71 (间隔1.42, 均值10, 差0.71, var=0.71^2*2/1 = 1)
        double offset = Math.Sqrt(0.5); // 约 0.707
        await _operator.ExecuteAsync(op, new Dictionary<string, object> { { "Value", 10.0 - offset } });
        var result = await _operator.ExecuteAsync(op, new Dictionary<string, object> { { "Value", 10.0 + offset } });

        Assert.True(result.IsSuccess);
        // Mean=10, StdDev=1, USL=13, LSL=7
        // Cp = (13-7)/6 = 1.0
        // Cpk = min((13-10)/3, (10-7)/3) = 1.0
        Assert.Equal(1.0, (double)result.OutputData!["Cp"], 2);
        Assert.Equal(1.0, (double)result.OutputData["Cpk"], 2);
    }

    [Fact]
    public async Task ExecuteAsync_WithTightSpec_ReportsNotCapable()
    {
        // 场景：均值 10.0, 标准差 1.0, USL=11.0, LSL=9.0
        var op = CreateOperator(new Dictionary<string, object>
        {
            { "USL", 11.0 },
            { "LSL", 9.0 }
        });

        double offset = Math.Sqrt(0.5);
        await _operator.ExecuteAsync(op, new Dictionary<string, object> { { "Value", 10.0 - offset } });
        var result = await _operator.ExecuteAsync(op, new Dictionary<string, object> { { "Value", 10.0 + offset } });

        Assert.True(result.IsSuccess);
        // Mean=10, StdDev=1, USL=11, LSL=9
        // Cpk = min((11-10)/3, (10-9)/3) = 0.3333
        Assert.Equal(0.33, (double)result.OutputData!["Cpk"], 2);
        Assert.False((bool)result.OutputData["IsCapable"]);
    }

    [Fact]
    public void ValidateParameters_UslLessThanLsl_ReturnsInvalid()
    {
        var op = CreateOperator(new Dictionary<string, object>
        {
            { "USL", 5.0 },
            { "LSL", 10.0 }
        });

        var result = _operator.ValidateParameters(op);

        Assert.False(result.IsValid);
        Assert.Contains("大于", result.Errors[0]);
    }

    private Operator CreateOperator(Dictionary<string, object>? parameters = null)
    {
        var op = new Operator(Guid.NewGuid(), "Stats", OperatorType.Statistics, 0, 0);

        if (parameters != null)
        {
            foreach (var (key, value) in parameters)
            {
                op.AddParameter(new Acme.Product.Core.ValueObjects.Parameter(Guid.NewGuid(), key, key, "", "double", value));
            }
        }

        return op;
    }
}
