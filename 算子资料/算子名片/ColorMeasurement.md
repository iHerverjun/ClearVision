# 颜色测量 / ColorMeasurement

## 当前契约

- 正式参数主轴是 `MeasurementMode`
- `LabDeltaE` 模式输出 `LabMean`、`ReferenceLab`、`DeltaE`
- `HsvStats` 模式输出 `HueMean`、`SaturationMean`、`ValueMean`、`HueValid`
- `DeltaEMethod` 只在 `LabDeltaE` 模式下生效

## 关键语义

- `HueMean` 使用圆统计，不再做错误的算术平均
- 低饱和或低亮度区域会给出 `HueValid=false`
- Measurement 默认只输出像素域 / 颜色域统计，不直接给出物理量结论

## 输入

- `Image`
- 可选 `ReferenceColor`

## 输出

- `LabMean`
- `ReferenceLab`
- `DeltaE`
- `HueMean`
- `SaturationMean`
- `ValueMean`
- `HueValid`
- `Image`
