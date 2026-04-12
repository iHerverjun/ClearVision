using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.ValueObjects;
using Acme.Product.Infrastructure.Calibration;
using Acme.Product.Infrastructure.Operators;
using Acme.Product.Infrastructure.Services;
using Acme.Product.Tests.Operators;
using Acme.Product.Tests.TestData;
using Microsoft.Extensions.Logging.Abstractions;

namespace Acme.Product.Tests.Integration;

public class CalibrationV2IntegrationTests
{
    [Fact]
    public async Task CalibrationLoader_To_Undistort_WithV2Bundle_ShouldSucceed()
    {
        var loader = new CalibrationLoaderOperator(NullLogger<CalibrationLoaderOperator>.Instance);
        var undistort = new UndistortOperator(NullLogger<UndistortOperator>.Instance);

        var tempFile = Path.Combine(Path.GetTempPath(), $"calibration_loader_undistort_{Guid.NewGuid():N}.json");
        try
        {
            await File.WriteAllTextAsync(tempFile, CalibrationBundleV2TestData.CreateAcceptedCameraBundleJson());

            var loaderOp = new Operator("CalibrationLoader", OperatorType.CalibrationLoader, 0, 0);
            loaderOp.AddParameter(new Parameter(Guid.NewGuid(), "FilePath", "FilePath", string.Empty, "string", tempFile));
            var loadResult = await loader.ExecuteAsync(loaderOp, null);
            Assert.True(loadResult.IsSuccess, loadResult.ErrorMessage);

            using var image = TestHelpers.CreateTestImage(width: 320, height: 240);
            var undistortOp = new Operator("Undistort", OperatorType.Undistort, 0, 0);
            var undistortResult = await undistort.ExecuteAsync(undistortOp, new Dictionary<string, object>
            {
                ["Image"] = image,
                ["CalibrationData"] = Assert.IsType<string>(loadResult.OutputData!["CalibrationData"])
            });

            Assert.True(undistortResult.IsSuccess, undistortResult.ErrorMessage);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public async Task DesktopHandEyeSavePath_To_CalibrationLoader_ShouldStayOnV2ScaleOffsetContract()
    {
        var service = new PlanarScaleOffsetCalibrationService();
        var loader = new CalibrationLoaderOperator(NullLogger<CalibrationLoaderOperator>.Instance);
        var tempFile = Path.Combine(Path.GetTempPath(), $"planar_scale_offset_v2_{Guid.NewGuid():N}.json");

        try
        {
            var saved = await service.SaveCalibrationAsync(
                new PlanarScaleOffsetCalibrationResult
                {
                    Success = true,
                    OriginX = 10.0,
                    OriginY = 20.0,
                    ScaleX = 0.02,
                    ScaleY = 0.02,
                    MeanErrorX = 0.01,
                    MeanErrorY = 0.02,
                    Rmse = 0.02,
                    PointCount = 4
                },
                tempFile);

            Assert.True(saved);

            var loaderOp = new Operator("CalibrationLoader", OperatorType.CalibrationLoader, 0, 0);
            loaderOp.AddParameter(new Parameter(Guid.NewGuid(), "FilePath", "FilePath", string.Empty, "string", tempFile));
            var loadResult = await loader.ExecuteAsync(loaderOp, null);

            Assert.True(loadResult.IsSuccess, loadResult.ErrorMessage);

            var bundle = Assert.IsType<CalibrationBundleV2>(loadResult.OutputData!["CalibrationBundle"]);
            Assert.Equal(CalibrationKindV2.PlanarTransform2D, bundle.CalibrationKind);
            Assert.Equal(TransformModelV2.ScaleOffset, bundle.TransformModel);
            Assert.True(bundle.Quality.Accepted);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public void FlowLinter_WithPreviewCalibrationBundle_ShouldRejectProductionConsumer()
    {
        var flow = new OperatorFlow("CalibrationFlow");
        var coord = new Operator(Guid.NewGuid(), "CoordinateTransform", OperatorType.CoordinateTransform, 0, 0);
        coord.AddParameter(new Parameter(Guid.NewGuid(), "CalibrationData", "CalibrationData", string.Empty, "string", CalibrationBundleV2TestData.CreatePreviewBundleJson()));
        flow.AddOperator(coord);

        var linter = new FlowLinter();
        var result = linter.Lint(flow);

        Assert.Contains(result.Issues, issue => issue.Code == "PARAM_001");
    }
}
