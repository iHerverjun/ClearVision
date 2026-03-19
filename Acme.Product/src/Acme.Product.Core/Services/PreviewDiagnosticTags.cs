namespace Acme.Product.Core.Services;

public static class PreviewDiagnosticTags
{
    public const string SpecularHighlightsDominant = "SpecularHighlightsDominant";
    public const string MaskTooNoisy = "MaskTooNoisy";
    public const string StrapFragmented = "StrapFragmented";
    public const string LowContrast = "LowContrast";
    public const string BlurryImage = "BlurryImage";
    public const string UnevenIllumination = "UnevenIllumination";

    public const string MissingExpectedClass = "MissingExpectedClass";
    public const string DuplicateDetectedClass = "DuplicateDetectedClass";
    public const string DetectionCountMismatch = "DetectionCountMismatch";
    public const string LowDetectionConfidence = "LowDetectionConfidence";
    public const string OrderMismatch = "OrderMismatch";
}
