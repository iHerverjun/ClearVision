using System.Reflection;
using Acme.Product.Core.Attributes;
using Acme.Product.Infrastructure.Operators;
using Acme.Product.Tests.TestData;

namespace Acme.Product.Tests.Integration;

public class LegacyCalibrationContractAuditTests
{
    private static readonly string RepoRoot = ResolveRepoRoot();

    [Fact]
    public void CalibrationConsumers_ShouldNotExposeLegacyCalibrationFileParameter()
    {
        var consumerTypes = new[]
        {
            typeof(CoordinateTransformOperator),
            typeof(UndistortOperator),
            typeof(FisheyeUndistortOperator),
            typeof(PixelToWorldTransformOperator)
        };

        foreach (var type in consumerTypes)
        {
            var paramNames = type
                .GetCustomAttributes<OperatorParamAttribute>(inherit: false)
                .Select(attribute => attribute.Name)
                .ToArray();

            Assert.DoesNotContain("CalibrationFile", paramNames);
        }
    }

    [Fact]
    public void CalibrationSurface_ShouldNotExposeLegacyOutputPorts()
    {
        Assert.DoesNotContain(
            typeof(HandEyeCalibrationOperator).GetCustomAttributes<OutputPortAttribute>(false).Select(attribute => attribute.Name),
            name => name.Equals("HandEyeMatrix", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("InverseHandEyeMatrix", StringComparison.OrdinalIgnoreCase));

        Assert.DoesNotContain(
            typeof(HandEyeCalibrationValidatorOperator).GetCustomAttributes<InputPortAttribute>(false).Select(attribute => attribute.Name),
            name => name.Equals("HandEyeMatrix", StringComparison.OrdinalIgnoreCase));

        Assert.DoesNotContain(
            typeof(CalibrationLoaderOperator).GetCustomAttributes<OutputPortAttribute>(false).Select(attribute => attribute.Name),
            name => name.Equals("Transform2D", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("Transform3D", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("CameraMatrix", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("DistCoeffs", StringComparison.OrdinalIgnoreCase));

        Assert.DoesNotContain(
            typeof(NPointCalibrationOperator).GetCustomAttributes<OutputPortAttribute>(false).Select(attribute => attribute.Name),
            name => name.Equals("TransformMatrix", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("PixelSize", StringComparison.OrdinalIgnoreCase));

        Assert.DoesNotContain(
            typeof(TranslationRotationCalibrationOperator).GetCustomAttributes<OutputPortAttribute>(false).Select(attribute => attribute.Name),
            name => name.Equals("TransformMatrix", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("RotationCenter", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void CalibrationBundleV2TestData_DefaultPayloads_ShouldNotContainLegacyFields()
    {
        var cameraBundle = CalibrationBundleV2TestData.CreateAcceptedCameraBundleJson();
        var planarBundle = CalibrationBundleV2TestData.CreateAcceptedScaleOffsetBundleJson();

        foreach (var payload in new[] { cameraBundle, planarBundle })
        {
            Assert.DoesNotContain("\"CalibrationFile\"", payload, StringComparison.Ordinal);
            Assert.DoesNotContain("\"OriginX\"", payload, StringComparison.Ordinal);
            Assert.DoesNotContain("\"OriginY\"", payload, StringComparison.Ordinal);
            Assert.DoesNotContain("\"ScaleX\"", payload, StringComparison.Ordinal);
            Assert.DoesNotContain("\"ScaleY\"", payload, StringComparison.Ordinal);
            Assert.DoesNotContain("\"PixelSize\"", payload, StringComparison.Ordinal);
            Assert.DoesNotContain("\"TransformMatrix\"", payload, StringComparison.Ordinal);
            Assert.DoesNotContain("\"HandEyeMatrix\"", payload, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void ActiveDocsAndCatalogs_ShouldNotContainLegacyCalibrationKeywords()
    {
        var includedFiles = new[]
        {
            Path.Combine(RepoRoot, "docs", "算子资料", "算子手册.md"),
            Path.Combine(RepoRoot, "docs", "reference", "手册", "项目架构手册.md"),
            Path.Combine(RepoRoot, "docs", "reference", "指南", "代码库深度导读.md"),
            Path.Combine(RepoRoot, "docs", "算子资料", "算子目录.json"),
            Path.Combine(RepoRoot, "docs", "算子资料", "算子名片", "catalog.json")
        };

        var forbiddenTokens = new[]
        {
            "CalibrationFile",
            "FileFormat",
            "HandEyeMatrix",
            "handeye:solve",
            "handeye:save",
            "hand_eye_calib.json"
        };

        foreach (var filePath in includedFiles)
        {
            var content = File.ReadAllText(filePath);
            foreach (var token in forbiddenTokens)
            {
                Assert.DoesNotContain(token, content, StringComparison.Ordinal);
            }
        }
    }

    private static string ResolveRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null)
        {
            if (Directory.Exists(Path.Combine(current.FullName, "Acme.Product")) &&
                Directory.Exists(Path.Combine(current.FullName, "docs")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        return Directory.GetCurrentDirectory();
    }
}
