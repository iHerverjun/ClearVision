# 宽度测量 / WidthMeasurement

## 当前契约

- 正式采样参数为 `SampleCount`
- `MultiScanCount` 是加密扫描密度，必须大于等于 `SampleCount`
- `ManualLines` 的语义是“参考线约束下的真实边缘测量”
- 没有真实边缘证据时，算子直接失败，不再回退为两条参考线的几何间距

## 关键语义

- `Width` / `MeanWidth` 来自真实边缘样本
- `SampleCount` 是目标采样数
- `ExecutedScanCount` 是实际完成的扫描数
- `ValidSampleCount` 是通过边缘定位与鲁棒过滤后的有效样本数

## 输入

- `Image`
- `Line1`
- `Line2`

## 输出

- `Width`
- `MeanWidth`
- `MinWidth`
- `MaxWidth`
- `P95Width`
- `StdDev`
- `ValidSampleRate`
- `SampleCount`
- `MultiScanCount`
- `ExecutedScanCount`
- `ValidSampleCount`
- `Image`
