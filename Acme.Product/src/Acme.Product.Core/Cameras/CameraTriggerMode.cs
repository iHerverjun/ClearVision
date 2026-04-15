using Acme.Product.Core.Entities;

namespace Acme.Product.Core.Cameras;

public enum CameraTriggerMode
{
    Software = 0,
    External = 1,
    Continuous = 2
}

public static class CameraTriggerModeExtensions
{
    public const int DefaultTargetFrameRateFps = 10;
    public const int MinTargetFrameRateFps = 1;
    public const int MaxTargetFrameRateFps = 120;

    public static CameraTriggerMode Normalize(string? rawMode)
    {
        if (string.IsNullOrWhiteSpace(rawMode))
        {
            return CameraTriggerMode.Software;
        }

        return rawMode.Trim().ToLowerInvariant() switch
        {
            "software" => CameraTriggerMode.Software,
            "hardware" => CameraTriggerMode.External,
            "external" => CameraTriggerMode.External,
            "externalsignal" => CameraTriggerMode.External,
            "continuous" => CameraTriggerMode.Continuous,
            _ => CameraTriggerMode.Software
        };
    }

    public static string ToConfigValue(this CameraTriggerMode mode) => mode switch
    {
        CameraTriggerMode.External => nameof(CameraTriggerMode.External),
        CameraTriggerMode.Continuous => nameof(CameraTriggerMode.Continuous),
        _ => nameof(CameraTriggerMode.Software)
    };

    public static bool IsFrameDriven(this CameraTriggerMode mode) =>
        mode is CameraTriggerMode.External or CameraTriggerMode.Continuous;

    public static int NormalizeTargetFrameRate(int targetFrameRateFps)
    {
        if (targetFrameRateFps <= 0)
        {
            return DefaultTargetFrameRateFps;
        }

        return Math.Clamp(targetFrameRateFps, MinTargetFrameRateFps, MaxTargetFrameRateFps);
    }

    public static CameraBindingConfig? FindBinding(this ICameraManager cameraManager, string? bindingIdOrCameraId)
    {
        if (string.IsNullOrWhiteSpace(bindingIdOrCameraId))
        {
            return null;
        }

        var normalized = bindingIdOrCameraId.Trim();
        return (cameraManager.GetBindings() ?? new List<CameraBindingConfig>()).FirstOrDefault(binding =>
            binding.Id.Equals(normalized, StringComparison.OrdinalIgnoreCase)
            || (!string.IsNullOrWhiteSpace(binding.SerialNumber)
                && binding.SerialNumber.Equals(normalized, StringComparison.OrdinalIgnoreCase)));
    }
}
