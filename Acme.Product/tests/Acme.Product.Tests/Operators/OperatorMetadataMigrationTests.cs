using Acme.Product.Core.Enums;
using Acme.Product.Infrastructure.Services;

namespace Acme.Product.Tests.Operators;

[Trait("Category", "Sprint7_AiEvolution")]
public class OperatorMetadataMigrationTests
{
    [Fact]
    public void AllOperatorTypes_ShouldHaveMetadataAfterMigration()
    {
        var factory = new OperatorFactory();
        var allTypes = Enum.GetValues<OperatorType>();
        var missing = new List<OperatorType>();
        var missingDisplayName = new List<OperatorType>();
        var missingPorts = new List<OperatorType>();

        foreach (var type in allTypes)
        {
            var metadata = factory.GetMetadata(type);
            if (metadata == null)
            {
                missing.Add(type);
                continue;
            }

            if (string.IsNullOrWhiteSpace(metadata.DisplayName))
            {
                missingDisplayName.Add(type);
            }

            if (metadata.OutputPorts.Count == 0 && metadata.InputPorts.Count == 0)
            {
                missingPorts.Add(type);
            }
        }

        Assert.True(
            missing.Count == 0,
            $"Missing metadata for: {string.Join(", ", missing)}");

        Assert.True(
            missingDisplayName.Count == 0,
            $"Metadata missing display name for: {string.Join(", ", missingDisplayName)}");

        Assert.True(
            missingPorts.Count == 0,
            $"Metadata missing input/output ports for: {string.Join(", ", missingPorts)}");
    }

    [Fact]
    public void GetAllMetadata_ShouldExcludeLegacyAliasTypes()
    {
        var factory = new OperatorFactory();
        var all = factory.GetAllMetadata().Select(m => m.Type).ToHashSet();

        Assert.DoesNotContain(OperatorType.Preprocessing, all);
        Assert.DoesNotContain(OperatorType.GaussianBlur, all);
        Assert.DoesNotContain(OperatorType.OnnxInference, all);
        Assert.DoesNotContain(OperatorType.ModbusRtuCommunication, all);
    }

    [Fact]
    public void MetadataCatalog_ShouldPreferChineseDisplayNameAndCategory()
    {
        var factory = new OperatorFactory();
        var metadataByType = factory.GetAllMetadata().ToDictionary(m => m.Type, m => m);

        Assert.Equal("滤波", metadataByType[OperatorType.Filtering].DisplayName);
        Assert.Equal("预处理", metadataByType[OperatorType.Filtering].Category);

        Assert.Equal("边缘检测", metadataByType[OperatorType.EdgeDetection].DisplayName);
        Assert.Equal("特征提取", metadataByType[OperatorType.EdgeDetection].Category);

        Assert.Equal("深度学习", metadataByType[OperatorType.DeepLearning].DisplayName);
        Assert.Equal("AI检测", metadataByType[OperatorType.DeepLearning].Category);
    }
}
