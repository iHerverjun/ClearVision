using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.ValueObjects;
using Acme.Product.Infrastructure.Operators;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Acme.Product.Tests.Operators;

[Trait("Category", "Sprint6_Phase3")]
public class TextSaveOperatorTests
{
    [Fact]
    public void OperatorType_ShouldBeTextSave()
    {
        var sut = CreateSut();
        Assert.Equal(OperatorType.TextSave, sut.OperatorType);
    }

    [Fact]
    public async Task ExecuteAsync_TextMode_ShouldWriteFile()
    {
        var sut = CreateSut();
        var path = Path.Combine(Path.GetTempPath(), $"cv_textsave_{Guid.NewGuid():N}.txt");
        var op = CreateOperator(new Dictionary<string, object>
        {
            { "FilePath", path },
            { "Format", "Text" },
            { "AppendMode", false },
            { "AddTimestamp", false },
            { "Encoding", "UTF8" }
        });

        var inputs = new Dictionary<string, object> { { "Text", "hello phase3" } };
        var result = await sut.ExecuteAsync(op, inputs);

        try
        {
            Assert.True(result.IsSuccess);
            Assert.NotNull(result.OutputData);
            Assert.True((bool)result.OutputData!["Success"]);
            Assert.True(File.Exists(path));
            var text = File.ReadAllText(path);
            Assert.Contains("hello phase3", text);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void ValidateParameters_WithInvalidFormat_ShouldReturnInvalid()
    {
        var sut = CreateSut();
        var op = CreateOperator(new Dictionary<string, object>
        {
            { "FilePath", "a.txt" },
            { "Format", "XML" }
        });
        Assert.False(sut.ValidateParameters(op).IsValid);
    }

    private static TextSaveOperator CreateSut()
    {
        return new TextSaveOperator(Substitute.For<ILogger<TextSaveOperator>>());
    }

    private static Operator CreateOperator(Dictionary<string, object>? parameters = null)
    {
        var op = new Operator("TextSave", OperatorType.TextSave, 0, 0);
        if (parameters != null)
        {
            foreach (var (k, v) in parameters)
            {
                op.AddParameter(new Parameter(Guid.NewGuid(), k, k, string.Empty, "string", v));
            }
        }

        return op;
    }
}

