using System.Text.Json.Serialization;

namespace Acme.Product.Infrastructure.Calibration;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CalibrationKindV2
{
    Unknown = 0,
    CameraIntrinsics = 1,
    FisheyeIntrinsics = 2,
    PlanarTransform2D = 3,
    RigidTransform2D = 4,
    HandEye = 5,
    StereoRig = 6
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TransformModelV2
{
    None = 0,
    Preview = 1,
    ScaleOffset = 2,
    Similarity = 3,
    Affine = 4,
    Homography = 5,
    Projection = 6,
    Rigid3D = 7,
    StereoRig = 8,
    Rigid = 9
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DistortionModelV2
{
    None = 0,
    BrownConrady = 1,
    KannalaBrandt = 2
}

public sealed class CalibrationBundleV2
{
    public int SchemaVersion { get; set; } = 2;

    public string BundleId { get; set; } = string.Empty;

    public string CalibrationVersion { get; set; } = string.Empty;

    public string DatasetFingerprint { get; set; } = string.Empty;

    public string ChecksumSha256 { get; set; } = string.Empty;

    public CalibrationKindV2 CalibrationKind { get; set; } = CalibrationKindV2.Unknown;

    public TransformModelV2 TransformModel { get; set; } = TransformModelV2.None;

    public string SourceFrame { get; set; } = string.Empty;

    public string TargetFrame { get; set; } = string.Empty;

    public string Unit { get; set; } = "mm";

    public CalibrationImageSizeV2? ImageSize { get; set; }

    public CalibrationIntrinsicsV2? Intrinsics { get; set; }

    public CalibrationDistortionV2? Distortion { get; set; }

    public CalibrationTransform2DV2? Transform2D { get; set; }

    public CalibrationTransform3DV2? Transform3D { get; set; }

    public StereoCalibrationDataV2? Stereo { get; set; }

    public CalibrationQualityV2 Quality { get; set; } = new();

    public DateTime GeneratedAtUtc { get; set; } = DateTime.UtcNow;

    public string ProducerOperator { get; set; } = string.Empty;
}

public sealed class CalibrationImageSizeV2
{
    public int Width { get; set; }

    public int Height { get; set; }
}

public sealed class CalibrationIntrinsicsV2
{
    public double[][] CameraMatrix { get; set; } = Array.Empty<double[]>();
}

public sealed class CalibrationDistortionV2
{
    public DistortionModelV2 Model { get; set; } = DistortionModelV2.None;

    public double[] Coefficients { get; set; } = Array.Empty<double>();
}

public sealed class CalibrationTransform2DV2
{
    public TransformModelV2 Model { get; set; } = TransformModelV2.None;

    public double[][] Matrix { get; set; } = Array.Empty<double[]>();

    public double? PixelSizeX { get; set; }

    public double? PixelSizeY { get; set; }
}

public sealed class CalibrationTransform3DV2
{
    public TransformModelV2 Model { get; set; } = TransformModelV2.Rigid3D;

    public double[][] Matrix { get; set; } = Array.Empty<double[]>();

    public double[][]? InverseMatrix { get; set; }
}

public sealed class StereoCalibrationDataV2
{
    public CalibrationIntrinsicsV2? LeftIntrinsics { get; set; }

    public CalibrationIntrinsicsV2? RightIntrinsics { get; set; }

    public CalibrationDistortionV2? LeftDistortion { get; set; }

    public CalibrationDistortionV2? RightDistortion { get; set; }

    public double[][] Rotation { get; set; } = Array.Empty<double[]>();

    public double[] Translation { get; set; } = Array.Empty<double>();

    public double[][]? Essential { get; set; }

    public double[][]? Fundamental { get; set; }

    public double[][]? Q { get; set; }
}

public sealed class CalibrationQualityV2
{
    public bool Accepted { get; set; }

    public double MeanError { get; set; }

    public double MaxError { get; set; }

    public int InlierCount { get; set; }

    public int TotalSampleCount { get; set; }

    public List<string> Diagnostics { get; set; } = new();
}
