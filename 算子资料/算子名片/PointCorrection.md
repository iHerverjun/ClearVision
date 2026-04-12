# 点位修正 / PointCorrection

## 基本信息
| 字段 | 值 |
|---|---|
| Class | `PointCorrectionOperator` |
| Enum | `OperatorType.PointCorrection` |
| Category | `数据处理` |
| Maturity | Stable |

## 算子语义
- 默认修正方向是 `ReferencePoint - DetectedPoint`。
- `CorrectionMode=TranslationOnly` 输出平移修正。
- `CorrectionMode=TranslationRotation` 输出刚体修正，`CorrectionAngle` 会归一化到 `[-180, 180)`。
- `CorrectionX/CorrectionY` 受 `OutputUnit` 影响；`TransformMatrix` 始终是像素域 2x3 矩阵。
- `TransformUnit` 固定输出 `"Pixel"`。

## 输入与参数
### 输入端口
| Name | Type | Required | 说明 |
|---|---|---|---|
| `DetectedPoint` | `Point` | Yes | 检测点位 |
| `DetectedAngle` | `Float` | No | 检测角度 |
| `ReferencePoint` | `Point` | Yes | 参考点位 |
| `ReferenceAngle` | `Float` | No | 参考角度 |

### 参数
| Name | Type | Default | 说明 |
|---|---|---|---|
| `CorrectionMode` | `enum(TranslationOnly/TranslationRotation)` | `TranslationOnly` | 修正模式 |
| `OutputUnit` | `enum(Pixel/mm)` | `Pixel` | `CorrectionX/Y` 输出单位 |
| `PixelSize` | `double` | `1.0` | 像素当量，必须是正的有限数 |
| `MaxAllowedDistance` | `double` | `0.0` | 默认关闭（`0`）；`>0` 时超阈值直接失败 |

## 输出端口
| Name | Type | 说明 |
|---|---|---|
| `CorrectionX` | `Float` | X 修正量（按 `OutputUnit`） |
| `CorrectionY` | `Float` | Y 修正量（按 `OutputUnit`） |
| `CorrectionAngle` | `Float` | 角度修正量（归一化到 `[-180, 180)`） |
| `TransformMatrix` | `Any` | 像素域 2x3 刚体矩阵 |
| `TransformUnit` | `String` | 固定为 `"Pixel"` |

## 可靠性约束
- 点位、角度、像素当量统一要求有限数；`NaN/Infinity` 会直接失败。
- `PixelSize` 必须为正的有限数。
- `MaxAllowedDistance` 必须是 `>=0` 的有限数。

## 变更记录
| Version | Date | Changes |
|---|---|---|
| 1.0.3 | 2026-04-12 | 新增角度归一化、`TransformUnit`、`MaxAllowedDistance`；`TransformMatrix` 固定为像素域；补充非有限值拒绝规则。 |
| 1.0.2 | 2026-03-14 | 文档补全。 |
| 1.0.0 | 2026-03-03 | 初版。 |
