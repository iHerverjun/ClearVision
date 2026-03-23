using System.Text.Json;
using FluentAssertions;

namespace Acme.Product.Tests.AI;

public class WireSequenceScenarioPackageTests
{
    [Fact]
    public void ScenarioPackage_ShouldAlignTemplateManifestAndRule()
    {
        var repoRoot = ResolveRepoRoot();
        var templatePath = Path.Combine(repoRoot, "线序检测", "scenario-package-wire-sequence", "template", "terminal-wire-sequence.flow.template.json");
        var manifestPath = Path.Combine(repoRoot, "线序检测", "scenario-package-wire-sequence", "manifest.json");
        var rulePath = Path.Combine(repoRoot, "线序检测", "scenario-package-wire-sequence", "rules", "sequence-rule.v1.json");

        using var template = JsonDocument.Parse(File.ReadAllText(templatePath));
        using var manifest = JsonDocument.Parse(File.ReadAllText(manifestPath));
        using var rule = JsonDocument.Parse(File.ReadAllText(rulePath));

        manifest.RootElement.GetProperty("Version").GetString().Should().Be("1.2.0");
        manifest.RootElement.GetProperty("Constraints").GetProperty("RequiredResources")
            .EnumerateArray().Select(item => item.GetString())
            .Should().Equal("DeepLearning.ModelPath", "DeepLearning.LabelsPath");

        var assetVersions = manifest.RootElement.GetProperty("Assets").EnumerateArray()
            .ToDictionary(
                item => item.GetProperty("ArtifactType").GetString()!,
                item => item.GetProperty("ArtifactVersion").GetString()!);
        assetVersions["Template"].Should().Be("1.2.0");
        assetVersions["Model"].Should().Be("1.1.0");
        assetVersions["Rule"].Should().Be("1.2.0");

        template.RootElement.GetProperty("requiredResources").EnumerateArray().Select(item => item.GetString())
            .Should().Equal("DeepLearning.ModelPath", "DeepLearning.LabelsPath");
        template.RootElement.GetProperty("tunableParameters").EnumerateArray().Select(item => item.GetString())
            .Should().Equal("BoxNms.ScoreThreshold", "BoxNms.IouThreshold");

        var operatorTypes = template.RootElement.GetProperty("operators").EnumerateArray()
            .Select(item => item.GetProperty("type").GetString())
            .ToList();
        operatorTypes.Should().Equal(
            "ImageAcquisition",
            "RoiManager",
            "ImageResize",
            "DeepLearning",
            "BoxNms",
            "DetectionSequenceJudge",
            "ResultOutput");

        var connections = template.RootElement.GetProperty("connections").EnumerateArray().ToList();
        connections.Should().Contain(item =>
            item.GetProperty("source").GetString() == "op_4" &&
            item.GetProperty("sourcePort").GetString() == "Objects" &&
            item.GetProperty("target").GetString() == "op_5" &&
            item.GetProperty("targetPort").GetString() == "Detections");
        connections.Should().Contain(item =>
            item.GetProperty("source").GetString() == "op_5" &&
            item.GetProperty("sourcePort").GetString() == "Detections" &&
            item.GetProperty("target").GetString() == "op_6" &&
            item.GetProperty("targetPort").GetString() == "Detections");
        connections.Should().Contain(item =>
            item.GetProperty("source").GetString() == "op_5" &&
            item.GetProperty("sourcePort").GetString() == "Diagnostics" &&
            item.GetProperty("target").GetString() == "op_7" &&
            item.GetProperty("targetPort").GetString() == "Data");
        connections.Should().Contain(item =>
            item.GetProperty("source").GetString() == "op_6" &&
            item.GetProperty("sourcePort").GetString() == "Diagnostics" &&
            item.GetProperty("target").GetString() == "op_7" &&
            item.GetProperty("targetPort").GetString() == "Result");
        connections.Should().Contain(item =>
            item.GetProperty("source").GetString() == "op_6" &&
            item.GetProperty("sourcePort").GetString() == "Message" &&
            item.GetProperty("target").GetString() == "op_7" &&
            item.GetProperty("targetPort").GetString() == "Text");

        var deepLearningParams = template.RootElement.GetProperty("operators").EnumerateArray()
            .Single(item => item.GetProperty("id").GetString() == "op_4")
            .GetProperty("params");
        deepLearningParams.GetProperty("EnableInternalNms").GetString().Should().Be("false");
        deepLearningParams.GetProperty("Confidence").GetString().Should().Be("0.05");

        var judgeParams = template.RootElement.GetProperty("operators").EnumerateArray()
            .Single(item => item.GetProperty("id").GetString() == "op_6")
            .GetProperty("params");
        judgeParams.GetProperty("MinConfidence").GetString().Should().Be("0.0");

        var manifestSequence = manifest.RootElement.GetProperty("Constraints").GetProperty("ExpectedSequence")
            .EnumerateArray().Select(item => item.GetString()).ToArray();
        var ruleSequence = rule.RootElement.GetProperty("expectedSequence")
            .EnumerateArray().Select(item => item.GetString()).ToArray();
        var templateSequence = template.RootElement.GetProperty("expectedSequence")
            .EnumerateArray().Select(item => item.GetString()).ToArray();

        templateSequence.Should().Equal("Wire_Brown", "Wire_Black", "Wire_Blue");
        manifestSequence.Should().Equal(templateSequence);
        ruleSequence.Should().Equal(templateSequence);
        rule.RootElement.GetProperty("requiredResources").EnumerateArray().Select(item => item.GetString())
            .Should().Equal("DeepLearning.ModelPath", "DeepLearning.LabelsPath");
        rule.RootElement.GetProperty("thresholdOwner").GetString().Should().Be("BoxNms");
        rule.RootElement.GetProperty("minConfidence").GetDouble().Should().Be(0.0);
    }

    private static string ResolveRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null)
        {
            var acmeProduct = Path.Combine(current.FullName, "Acme.Product");
            var wireSequence = Path.Combine(current.FullName, "线序检测");
            if (Directory.Exists(acmeProduct) && Directory.Exists(wireSequence))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Failed to resolve repository root for wire-sequence scenario package tests.");
    }
}
