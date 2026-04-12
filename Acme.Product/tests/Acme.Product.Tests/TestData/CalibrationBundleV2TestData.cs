namespace Acme.Product.Tests.TestData;

internal static class CalibrationBundleV2TestData
{
    public static string CreateAcceptedCameraBundleJson(
        bool includeLegacyFields = false,
        bool fisheye = false,
        int width = 320,
        int height = 240)
    {
        var distortionModel = fisheye ? "kannalaBrandt" : "brownConrady";
        var distCoeffs = fisheye ? "0.1,0.01,0.0,0.0" : "0.1,0.01,0.0,0.0,0.0";
        var legacyModel = fisheye ? "\"Model\":\"Fisheye\",\"IsFisheye\":true," : string.Empty;
        var cx = width / 2.0;
        var cy = height / 2.0;
        var legacyFields = includeLegacyFields
            ? $$"""
                {{legacyModel}}
                "CameraMatrix":[[500.0,0.0,{{cx}}],[0.0,500.0,{{cy}}],[0.0,0.0,1.0]],
                "DistCoeffs":[{{distCoeffs}}],
                "ImageWidth":{{width}},
                "ImageHeight":{{height}},
                """
            : string.Empty;

        return $$"""
                 {
                   "schemaVersion": 2,
                   "calibrationKind": "{{(fisheye ? "fisheyeIntrinsics" : "cameraIntrinsics")}}",
                   "transformModel": "none",
                   "sourceFrame": "image",
                   "targetFrame": "imageUndistorted",
                   "unit": "mm",
                   "imageSize": {
                     "width": {{width}},
                     "height": {{height}}
                   },
                   "intrinsics": {
                     "cameraMatrix": [
                       [500.0, 0.0, {{cx}}],
                       [0.0, 500.0, {{cy}}],
                       [0.0, 0.0, 1.0]
                     ]
                   },
                   "distortion": {
                     "model": "{{distortionModel}}",
                     "coefficients": [{{distCoeffs}}]
                   },
                   "quality": {
                     "accepted": true,
                     "meanError": 0.11,
                     "maxError": 0.23,
                     "inlierCount": 24,
                     "totalSampleCount": 24,
                     "diagnostics": []
                   },
                   "producerOperator": "CalibrationBundleV2TestData",
                   {{legacyFields}}
                   "reserved": "compat"
                 }
                 """;
    }

    public static string CreateAcceptedScaleOffsetBundleJson(bool includeLegacyFields = false)
    {
        var legacyFields = includeLegacyFields
            ? """
              "OriginX":0.0,
              "OriginY":0.0,
              "ScaleX":0.02,
              "ScaleY":0.02,
              "PixelSize":0.02,
              "TransformMatrix":[[0.02,0.0,0.0],[0.0,0.02,0.0],[0.0,0.0,1.0]],
              """
            : string.Empty;

        return $$"""
                 {
                   "schemaVersion": 2,
                   "calibrationKind": "rigidTransform2D",
                   "transformModel": "scaleOffset",
                   "sourceFrame": "image",
                   "targetFrame": "world",
                   "unit": "mm",
                   "transform2D": {
                     "model": "scaleOffset",
                     "matrix": [
                       [0.02, 0.0, 0.0],
                       [0.0, 0.02, 0.0]
                     ],
                     "pixelSizeX": 0.02,
                     "pixelSizeY": 0.02
                   },
                   "quality": {
                     "accepted": true,
                     "meanError": 0.05,
                     "maxError": 0.09,
                     "inlierCount": 8,
                     "totalSampleCount": 8,
                     "diagnostics": []
                   },
                   "producerOperator": "CalibrationBundleV2TestData",
                   {{legacyFields}}
                   "reserved": "compat"
                 }
                 """;
    }

    public static string CreatePreviewBundleJson()
    {
        return """
               {
                 "schemaVersion": 2,
                 "calibrationKind": "cameraIntrinsics",
                 "transformModel": "preview",
                 "sourceFrame": "image",
                 "targetFrame": "imageUndistorted",
                 "quality": {
                   "accepted": false,
                   "meanError": 0.0,
                   "maxError": 0.0,
                   "inlierCount": 0,
                   "totalSampleCount": 0,
                   "diagnostics": ["preview-only"]
                 },
                 "producerOperator": "CalibrationBundleV2TestData"
               }
               """;
    }
}
