using System.Text.Json;
using FluentAssertions;

namespace Acme.Product.Tests.AI;

public class WireSequenceScenarioPackageTests
{
    [Fact]
    public void ScenarioPackage_ShouldAlignTemplateManifestAndRule()
    {
        var repoRoot = ResolveRepoRoot();
        var packageRoot = ResolveScenarioPackageRoot(repoRoot);
        var templatePath = Path.Combine(packageRoot, "template", "terminal-wire-sequence.flow.template.json");
        var manifestPath = Path.Combine(packageRoot, "manifest.json");
        var rulePath = Path.Combine(packageRoot, "rules", "sequence-rule.v1.json");

        using var template = JsonDocument.Parse(File.ReadAllText(templatePath));
        using var manifest = JsonDocument.Parse(File.ReadAllText(manifestPath));
        using var rule = JsonDocument.Parse(File.ReadAllText(rulePath));

        manifest.RootElement.GetProperty("Version").GetString().Should().Be("1.4.0");
        manifest.RootElement.GetProperty("Constraints").GetProperty("RequiredResources")
            .EnumerateArray().Select(item => item.GetString())
            .Should().Equal("DeepLearning.ModelPath");

        var assetVersions = manifest.RootElement.GetProperty("Assets").EnumerateArray()
            .ToDictionary(
                item => item.GetProperty("ArtifactType").GetString()!,
                item => item.GetProperty("ArtifactVersion").GetString()!);
        assetVersions["Template"].Should().Be("1.4.0");
        assetVersions["Model"].Should().Be("1.2.0");
        assetVersions["Rule"].Should().Be("1.4.0");

        template.RootElement.GetProperty("requiredResources").EnumerateArray().Select(item => item.GetString())
            .Should().Equal("DeepLearning.ModelPath");
        template.RootElement.GetProperty("tunableParameters").EnumerateArray().Select(item => item.GetString())
            .Should().Equal("BoxNms.ScoreThreshold", "BoxNms.IouThreshold");

        var operatorTypes = template.RootElement.GetProperty("operators").EnumerateArray()
            .Select(item => item.GetProperty("type").GetString())
            .ToList();
        operatorTypes.Should().Equal(
            "ImageAcquisition",
            "DeepLearning",
            "BoxFilter",
            "BoxNms",
            "DetectionSequenceJudge",
            "ResultOutput");

        var connections = template.RootElement.GetProperty("connections").EnumerateArray().ToList();
        connections.Should().Contain(item =>
            item.GetProperty("source").GetString() == "op_2" &&
            item.GetProperty("sourcePort").GetString() == "Objects" &&
            item.GetProperty("target").GetString() == "op_3" &&
            item.GetProperty("targetPort").GetString() == "Detections");
        connections.Should().Contain(item =>
            item.GetProperty("source").GetString() == "op_3" &&
            item.GetProperty("sourcePort").GetString() == "Detections" &&
            item.GetProperty("target").GetString() == "op_4" &&
            item.GetProperty("targetPort").GetString() == "Detections");
        connections.Should().Contain(item =>
            item.GetProperty("source").GetString() == "op_4" &&
            item.GetProperty("sourcePort").GetString() == "Diagnostics" &&
            item.GetProperty("target").GetString() == "op_6" &&
            item.GetProperty("targetPort").GetString() == "Data");
        connections.Should().Contain(item =>
            item.GetProperty("source").GetString() == "op_5" &&
            item.GetProperty("sourcePort").GetString() == "Diagnostics" &&
            item.GetProperty("target").GetString() == "op_6" &&
            item.GetProperty("targetPort").GetString() == "Result");
        connections.Should().Contain(item =>
            item.GetProperty("source").GetString() == "op_5" &&
            item.GetProperty("sourcePort").GetString() == "Message" &&
            item.GetProperty("target").GetString() == "op_6" &&
            item.GetProperty("targetPort").GetString() == "Text");

        var deepLearningParams = template.RootElement.GetProperty("operators").EnumerateArray()
            .Single(item => item.GetProperty("id").GetString() == "op_2")
            .GetProperty("params");
        deepLearningParams.GetProperty("EnableInternalNms").GetString().Should().Be("false");
        deepLearningParams.GetProperty("Confidence").GetString().Should().Be("0.05");

        var boxFilterParams = template.RootElement.GetProperty("operators").EnumerateArray()
            .Single(item => item.GetProperty("id").GetString() == "op_3")
            .GetProperty("params");
        boxFilterParams.GetProperty("FilterMode").GetString().Should().Be("Region");
        boxFilterParams.GetProperty("RegionW").GetString().Should().Be("999999");
        boxFilterParams.GetProperty("RegionH").GetString().Should().Be("999999");

        var boxNmsParams = template.RootElement.GetProperty("operators").EnumerateArray()
            .Single(item => item.GetProperty("id").GetString() == "op_4")
            .GetProperty("params");
        boxNmsParams.GetProperty("ShowSuppressed").GetString().Should().Be("false");

        var judgeParams = template.RootElement.GetProperty("operators").EnumerateArray()
            .Single(item => item.GetProperty("id").GetString() == "op_5")
            .GetProperty("params");
        judgeParams.GetProperty("MinConfidence").GetString().Should().Be("0.0");

        var manifestSequence = manifest.RootElement.GetProperty("Constraints").GetProperty("ExpectedSequence")
            .EnumerateArray().Select(item => item.GetString()).ToArray();
        var ruleSequence = rule.RootElement.GetProperty("expectedSequence")
            .EnumerateArray().Select(item => item.GetString()).ToArray();
        var templateSequence = template.RootElement.GetProperty("expectedSequence")
            .EnumerateArray().Select(item => item.GetString()).ToArray();

        templateSequence.Should().Equal("Wire_Black", "Wire_Blue");
        manifestSequence.Should().Equal(templateSequence);
        ruleSequence.Should().Equal(templateSequence);
        rule.RootElement.GetProperty("requiredResources").EnumerateArray().Select(item => item.GetString())
            .Should().Equal("DeepLearning.ModelPath");
        rule.RootElement.GetProperty("thresholdOwner").GetString().Should().Be("BoxNms");
        rule.RootElement.GetProperty("minConfidence").GetDouble().Should().Be(0.0);
        rule.RootElement.GetProperty("sortBy").GetString().Should().Be("CenterY");
        rule.RootElement.GetProperty("direction").GetString().Should().Be("TopToBottom");

        judgeParams.GetProperty("ExpectedLabels").GetString().Should().Be("Wire_Black,Wire_Blue");
        judgeParams.GetProperty("SortBy").GetString().Should().Be("CenterY");
        judgeParams.GetProperty("Direction").GetString().Should().Be("TopToBottom");
    }

    [Fact]
    public void ScenarioPackage_1_3_Release_ShouldPointToFrozenArtifacts()
    {
        var repoRoot = ResolveRepoRoot();
        var packageRoot = ResolveScenarioPackageRoot(repoRoot);
        var releasePath = Path.Combine(packageRoot, "versions", "1.3.0", "release.json");
        using var release = JsonDocument.Parse(File.ReadAllText(releasePath));

        release.RootElement.GetProperty("ManifestPath").GetString().Should().Be("versions/1.3.0/manifest.json");

        var artifacts = release.RootElement.GetProperty("Artifacts").EnumerateArray().ToList();
        artifacts.Single(item => item.GetProperty("ArtifactType").GetString() == "Template")
            .GetProperty("RelativePath").GetString().Should().Be("versions/1.3.0/template/terminal-wire-sequence.flow.template.json");
        artifacts.Single(item => item.GetProperty("ArtifactType").GetString() == "Rule")
            .GetProperty("RelativePath").GetString().Should().Be("versions/1.3.0/rules/sequence-rule.v1.json");
        artifacts.Single(item => item.GetProperty("ArtifactType").GetString() == "Label")
            .GetProperty("RelativePath").GetString().Should().Be("versions/1.3.0/labels/labels.txt");

        var manifestPath = Path.Combine(packageRoot, "versions", "1.3.0", "manifest.json");
        var templatePath = Path.Combine(packageRoot, "versions", "1.3.0", "template", "terminal-wire-sequence.flow.template.json");
        var rulePath = Path.Combine(packageRoot, "versions", "1.3.0", "rules", "sequence-rule.v1.json");
        var labelsPath = Path.Combine(packageRoot, "versions", "1.3.0", "labels", "labels.txt");

        File.Exists(manifestPath).Should().BeTrue();
        File.Exists(templatePath).Should().BeTrue();
        File.Exists(rulePath).Should().BeTrue();
        File.Exists(labelsPath).Should().BeTrue();

        using var manifest = JsonDocument.Parse(File.ReadAllText(manifestPath));
        using var template = JsonDocument.Parse(File.ReadAllText(templatePath));
        using var rule = JsonDocument.Parse(File.ReadAllText(rulePath));

        manifest.RootElement.GetProperty("Version").GetString().Should().Be("1.3.0");
        template.RootElement.GetProperty("templateVersion").GetString().Should().Be("1.3.0");
        rule.RootElement.GetProperty("ruleVersion").GetString().Should().Be("1.3.0");

        template.RootElement.GetProperty("operators").EnumerateArray()
            .Select(item => item.GetProperty("type").GetString())
            .Should().Equal(
                "ImageAcquisition",
                "RoiManager",
                "ImageResize",
                "DeepLearning",
                "BoxNms",
                "DetectionSequenceJudge",
                "ResultOutput");

        template.RootElement.GetProperty("expectedSequence").EnumerateArray().Select(item => item.GetString())
            .Should().Equal("Wire_Black", "Wire_Blue");
        rule.RootElement.GetProperty("direction").GetString().Should().Be("TopToBottom");
        File.ReadAllLines(labelsPath).Should().Equal("Wire_Blue", "Wire_Black");
    }

    [Fact]
    public void ScenarioPackage_Labels_ShouldTrackModelClassOrderSeparatelyFromExpectedSequence()
    {
        var repoRoot = ResolveRepoRoot();
        var packageRoot = ResolveScenarioPackageRoot(repoRoot);
        var templatePath = Path.Combine(packageRoot, "template", "terminal-wire-sequence.flow.template.json");
        var labelsPath = Path.Combine(packageRoot, "labels", "labels.txt");

        using var template = JsonDocument.Parse(File.ReadAllText(templatePath));

        var expectedSequence = template.RootElement.GetProperty("expectedSequence")
            .EnumerateArray()
            .Select(item => item.GetString())
            .ToArray();
        var modelLabelOrder = File.ReadAllLines(labelsPath);

        expectedSequence.Should().Equal("Wire_Black", "Wire_Blue");
        modelLabelOrder.Should().Equal("Wire_Blue", "Wire_Black");
        modelLabelOrder.Should().NotEqual(expectedSequence);
    }

    private static string ResolveScenarioPackageRoot(string repoRoot)
    {
        var packageRoot = Directory.GetDirectories(repoRoot, "scenario-package-wire-sequence", SearchOption.AllDirectories)
            .SingleOrDefault();

        packageRoot.Should().NotBeNullOrWhiteSpace();
        return packageRoot!;
    }

    private static string ResolveRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null)
        {
            var acmeProduct = Path.Combine(current.FullName, "Acme.Product");
            var hasScenarioPackage = Directory.Exists(acmeProduct) &&
                Directory.GetDirectories(current.FullName, "scenario-package-wire-sequence", SearchOption.AllDirectories).Length > 0;
            if (hasScenarioPackage)
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Failed to resolve repository root for wire-sequence scenario package tests.");
    }
}
