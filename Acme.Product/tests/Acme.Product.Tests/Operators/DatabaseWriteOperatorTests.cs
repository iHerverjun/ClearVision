// DatabaseWriteOperatorTests.cs
// DatabaseWriteOperatorTests测试
// 作者：蘅芜君

using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Infrastructure.Operators;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Acme.Product.Tests.Operators;

public class DatabaseWriteOperatorTests
{
    private readonly DatabaseWriteOperator _operator;

    public DatabaseWriteOperatorTests()
    {
        _operator = new DatabaseWriteOperator(Substitute.For<ILogger<DatabaseWriteOperator>>());
    }

    [Fact]
    public void OperatorType_ShouldBeDatabaseWrite()
    {
        _operator.OperatorType.Should().Be(OperatorType.DatabaseWrite);
    }

    [Fact]
    public void ValidateParameters_Default_ShouldBeValid()
    {
        var op = new Operator("测试", OperatorType.DatabaseWrite, 0, 0);
        _operator.ValidateParameters(op).IsValid.Should().BeTrue();
    }

    [Fact]
    public void ValidateParameters_WithEmptyConnectionString_ShouldReturnInvalid()
    {
        var op = new Operator("测试", OperatorType.DatabaseWrite, 0, 0);
        op.AddParameter(new(Guid.NewGuid(), "ConnectionString", "连接字符串", "", "string", "", "", "", true));
        var result = _operator.ValidateParameters(op);
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void ValidateParameters_WithEmptyTableName_ShouldReturnInvalid()
    {
        var op = new Operator("测试", OperatorType.DatabaseWrite, 0, 0);
        op.AddParameter(new(Guid.NewGuid(), "TableName", "表名", "", "string", "", "", "", true));
        var result = _operator.ValidateParameters(op);
        result.IsValid.Should().BeFalse();
    }
}
