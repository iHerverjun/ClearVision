using System.Text.Json;
using Acme.Product.Infrastructure.Services;

namespace Acme.Product.Tests.Services;

public class PlanarScaleOffsetCalibrationServiceTests
{
    [Fact]
    public async Task SaveCalibrationAsync_WithSuccessfulResult_ShouldPersistCalibrationBundleV2()
    {
        var service = new PlanarScaleOffsetCalibrationService();
        var tempFile = Path.Combine(Path.GetTempPath(), $"planar_scale_offset_v2_{Guid.NewGuid():N}.json");

        try
        {
            var result = new PlanarScaleOffsetCalibrationResult
            {
                Success = true,
                OriginX = 10.0,
                OriginY = 20.0,
                ScaleX = 0.02,
                ScaleY = 0.03,
                MeanErrorX = 0.01,
                MeanErrorY = 0.02,
                Rmse = 0.03,
                PointCount = 4
            };

            var saved = await service.SaveCalibrationAsync(result, tempFile);
            Assert.True(saved);

            var json = await File.ReadAllTextAsync(tempFile);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            Assert.Equal(2, root.GetProperty("schemaVersion").GetInt32());
            Assert.Equal("planarTransform2D", root.GetProperty("calibrationKind").GetString());
            Assert.Equal("scaleOffset", root.GetProperty("transformModel").GetString());
            Assert.True(root.GetProperty("quality").GetProperty("accepted").GetBoolean());
            Assert.Equal("PlanarScaleOffsetCalibrationService", root.GetProperty("producerOperator").GetString());
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }
}
