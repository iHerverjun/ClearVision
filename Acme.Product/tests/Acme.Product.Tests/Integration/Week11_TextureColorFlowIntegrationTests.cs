using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Infrastructure.ImageProcessing;
using Acme.Product.Infrastructure.Operators;
using Acme.Product.Tests.Operators;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using OpenCvSharp;
using Xunit;

namespace Acme.Product.Tests.Integration;

public sealed class Week11_TextureColorFlowIntegrationTests
{
    [Fact]
    public async Task Flow_Laws_Glcm_ShouldDifferentiateConstantAndCheckerboard()
    {
        var lawsExec = new LawsTextureFilterOperator(NullLogger<LawsTextureFilterOperator>.Instance);
        var glcmExec = new GlcmTextureOperator(NullLogger<GlcmTextureOperator>.Instance);

        var lawsOp = new Operator("laws", OperatorType.LawsTextureFilter, 0, 0);
        lawsOp.AddParameter(TestHelpers.CreateParameter("KernelCombo", "E5E5", "string"));
        lawsOp.AddParameter(TestHelpers.CreateParameter("SubtractLocalMean", false, "bool"));
        lawsOp.AddParameter(TestHelpers.CreateParameter("EnergyWindowSize", 15, "int"));

        var glcmOp = new Operator("glcm", OperatorType.GlcmTexture, 0, 0);
        glcmOp.AddParameter(TestHelpers.CreateParameter("Levels", 16, "int"));
        glcmOp.AddParameter(TestHelpers.CreateParameter("Distance", 1, "int"));
        glcmOp.AddParameter(TestHelpers.CreateParameter("DirectionsDeg", "0,45,90,135", "string"));
        glcmOp.AddParameter(TestHelpers.CreateParameter("Symmetric", true, "bool"));
        glcmOp.AddParameter(TestHelpers.CreateParameter("Normalize", true, "bool"));

        using var constantMat = new Mat(256, 256, MatType.CV_8UC1, Scalar.All(128));
        using var checkerMat = BuildCheckerboard(256, 256, blockSize: 8);

        // Constant
        var lawsConst = await lawsExec.ExecuteAsync(
            lawsOp,
            new Dictionary<string, object> { ["Image"] = new ImageWrapper(constantMat.Clone()) });
        lawsConst.IsSuccess.Should().BeTrue(lawsConst.ErrorMessage);
        var meanEnergyConst = Convert.ToDouble(lawsConst.OutputData!["MeanEnergy"]);

        var filteredConst = lawsConst.OutputData["FilteredImage"].Should().BeOfType<ImageWrapper>().Subject;
        var glcmConst = await glcmExec.ExecuteAsync(glcmOp, new Dictionary<string, object> { ["Image"] = filteredConst });
        glcmConst.IsSuccess.Should().BeTrue(glcmConst.ErrorMessage);
        var contrastConst = Convert.ToDouble(glcmConst.OutputData!["Contrast"]);

        // Checkerboard
        var lawsChk = await lawsExec.ExecuteAsync(
            lawsOp,
            new Dictionary<string, object> { ["Image"] = new ImageWrapper(checkerMat.Clone()) });
        lawsChk.IsSuccess.Should().BeTrue(lawsChk.ErrorMessage);
        var meanEnergyChk = Convert.ToDouble(lawsChk.OutputData!["MeanEnergy"]);

        var filteredChk = lawsChk.OutputData["FilteredImage"].Should().BeOfType<ImageWrapper>().Subject;
        var glcmChk = await glcmExec.ExecuteAsync(glcmOp, new Dictionary<string, object> { ["Image"] = filteredChk });
        glcmChk.IsSuccess.Should().BeTrue(glcmChk.ErrorMessage);
        var contrastChk = Convert.ToDouble(glcmChk.OutputData!["Contrast"]);

        meanEnergyChk.Should().BeGreaterThan(meanEnergyConst + 1e-6);
        contrastChk.Should().BeGreaterThan(contrastConst + 0.1);

        // Dispose unconsumed outputs to avoid leaks (operators auto-release inputs, not outputs).
        (lawsConst.OutputData["EnergyImage"] as ImageWrapper)?.Dispose();
        (lawsChk.OutputData["EnergyImage"] as ImageWrapper)?.Dispose();
        (glcmConst.OutputData?["PerDirection"] as IDisposable)?.Dispose();
        (glcmChk.OutputData?["PerDirection"] as IDisposable)?.Dispose();
    }

    [Fact]
    public async Task Flow_ColorMeasurement_DeltaE_ShouldBeConsistentWithColorDifference()
    {
        var exec = new ColorMeasurementOperator(NullLogger<ColorMeasurementOperator>.Instance);

        var op = new Operator("color", OperatorType.ColorMeasurement, 0, 0);
        op.AddParameter(TestHelpers.CreateParameter("ColorSpace", "Lab", "string"));
        op.AddParameter(TestHelpers.CreateParameter("DeltaEMethod", "CIEDE2000", "string"));
        op.AddParameter(TestHelpers.CreateParameter("RoiX", 0, "int"));
        op.AddParameter(TestHelpers.CreateParameter("RoiY", 0, "int"));
        op.AddParameter(TestHelpers.CreateParameter("RoiW", 40, "int"));
        op.AddParameter(TestHelpers.CreateParameter("RoiH", 40, "int"));

        using var mat = new Mat(60, 60, MatType.CV_8UC3, new Scalar(30, 30, 200)); // BGR
        var inputs = new Dictionary<string, object>
        {
            ["Image"] = new ImageWrapper(mat.Clone()),
            ["ReferenceColor"] = new Dictionary<string, object>
            {
                ["L"] = 0.0,
                ["A"] = 0.0,
                ["B"] = 0.0
            }
        };

        var result = await exec.ExecuteAsync(op, inputs);
        result.IsSuccess.Should().BeTrue(result.ErrorMessage);

        var l = Convert.ToDouble(result.OutputData!["L"]);
        var a = Convert.ToDouble(result.OutputData["A"]);
        var b = Convert.ToDouble(result.OutputData["B"]);
        var deltaE = Convert.ToDouble(result.OutputData["DeltaE"]);

        var expected = ColorDifference.DeltaE00(new CieLab(0, 0, 0), new CieLab(l, a, b));
        deltaE.Should().BeApproximately(expected, 1e-6);

        // Dispose output image wrapper.
        (result.OutputData["Image"] as ImageWrapper)?.Dispose();
    }

    private static Mat BuildCheckerboard(int width, int height, int blockSize)
    {
        var mat = new Mat(height, width, MatType.CV_8UC1);
        var idx = mat.GetGenericIndexer<byte>();
        for (var y = 0; y < height; y++)
        {
            var by = (y / blockSize) & 1;
            for (var x = 0; x < width; x++)
            {
                var bx = (x / blockSize) & 1;
                idx[y, x] = (byte)(((bx ^ by) == 0) ? 0 : 255);
            }
        }
        return mat;
    }
}

