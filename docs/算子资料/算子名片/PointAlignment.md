# 点位对齐 / PointAlignment

## 基本信息
| 字段 | 值 |
|---|---|
| Class | `PointAlignmentOperator` |
| Enum | `OperatorType.PointAlignment` |
| Category | `数据处理` |
| Maturity | Stable |

## 算子语义
- 计算规则固定为 `CurrentPoint - ReferencePoint`，输出 `OffsetX / OffsetY / Distance`。
- `OutputUnit=Pixel` 时输出像素值；`OutputUnit=mm` 时按 `PixelSize` 缩放三个输出。
- 本算子仅提供像素域偏移能力，不替代真实物理标定。

## 输入与参数
### 输入端口
| Name | Type | Required | 说明 |
|---|---|---|---|
| `CurrentPoint` | `Point` | Yes | 当前点位 |
| `ReferencePoint` | `Point` | Yes | 参考点位 |

### 参数
| Name | Type | Default | 说明 |
|---|---|---|---|
| `OutputUnit` | `enum(Pixel/mm)` | `Pixel` | 输出单位 |
| `PixelSize` | `double` | `1.0` | 像素当量，必须是正的有限数 |

## 输出端口
| Name | Type | 说明 |
|---|---|---|
| `OffsetX` | `Float` | X 偏移（`Current - Reference`） |
| `OffsetY` | `Float` | Y 偏移（`Current - Reference`） |
| `Distance` | `Float` | 偏移欧氏距离 |

## 可靠性约束
- 点位坐标必须为有限数，`NaN/Infinity` 会直接失败。
- `PixelSize` 必须为正的有限数，`NaN/Infinity` 或 `<=0` 会被拒绝。

## 变更记录
| Version | Date | Changes |
|---|---|---|
| 1.0.3 | 2026-04-12 | 明确锁定 `Current-Reference` 符号语义；补充非有限值拒绝规则。 |
| 1.0.2 | 2026-03-14 | 文档补全。 |
| 1.0.0 | 2026-03-03 | 初版。 |
