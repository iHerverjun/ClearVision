# 标定加载 / CalibrationLoader

## 定位
- 统一读取 `CalibrationBundleV2`（JSON）并输出强类型标定数据。
- 运行时只支持 JSON V2，不再支持 XML/YAML。

## 输入与参数
- 参数 `FilePath`：必填，`CalibrationBundleV2` JSON 文件路径。

## 输出
- `CalibrationData`：原始 JSON 字符串。
- `CalibrationBundle`：反序列化后的强类型对象。
- `Accepted`：`Quality.Accepted`。
- `Transform2D`、`Transform3D`、`CameraMatrix`、`DistCoeffs`：标准化输出。

## 约束
- 必须满足：
  - `schemaVersion = 2`
  - `calibrationKind` 非 `Unknown`
  - `sourceFrame`、`targetFrame` 非空
  - `quality` 节点存在
- 建议消费者在生产链路上强制 `Accepted = true`。

## V2 示例
```json
{
  "schemaVersion": 2,
  "calibrationKind": "planarTransform2D",
  "transformModel": "scaleOffset",
  "sourceFrame": "image",
  "targetFrame": "world",
  "unit": "mm",
  "transform2D": {
    "model": "scaleOffset",
    "matrix": [[0.02, 0.0, 0.0], [0.0, 0.02, 0.0]]
  },
  "quality": {
    "accepted": true,
    "meanError": 0.03,
    "maxError": 0.08,
    "inlierCount": 12,
    "totalSampleCount": 12,
    "diagnostics": []
  },
  "producerOperator": "NPointCalibrationOperator"
}
```
