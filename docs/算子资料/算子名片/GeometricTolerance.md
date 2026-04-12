# 几何公差 / GeometricTolerance

## 当前契约

- 该算子已经从旧的 `AngleOnly` 近似模型切换为 `DatumZone2D`
- 输入主链是 `FeaturePrimary + DatumA (+ DatumB / DatumC)`
- 正式参数包括：
  - `ToleranceType = Parallelism | Perpendicularity | Position | Concentricity`
  - `ZoneSize`
  - `EvaluationMode`
  - `NominalX`
  - `NominalY`

## 关键语义

- `Parallelism` / `Perpendicularity` 给出真实 `ZoneDeviation`
- `Position` 依赖 datum frame 与 nominal 目标位置
- `Concentricity` 使用中心偏移做判定
- 输出中 `Accepted` 是正式验收位，不再用旧的 angle-only 文案冒充 GD&T

## 输入

- 可选 `Image`
- `FeaturePrimary`
- `DatumA`
- 可选 `DatumB`
- 可选 `DatumC`

## 输出

- `Tolerance`
- `ZoneDeviation`
- `AngularDeviationDeg`
- `LinearBand`
- `MeasurementModel`
- `Accepted`
- `Image`
