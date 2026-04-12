# ArrayIndexer

## 基本信息
- 类名: `ArrayIndexerOperator`
- 枚举: `OperatorType.ArrayIndexer`
- 分类: `数据处理`

## 输入与输出
- 输入:
- `List` (`Any`, 必填): 可枚举集合或 `DetectionList`。
- 输出:
- `Item` (`Any`): 选中的元素。
- `Found` (`Boolean`): 是否找到元素。
- `Index` (`Integer`): 选中元素在原始输入列表中的索引。

## 参数契约
- `Mode` (`enum`, 默认 `Index`): `Index/MaxConfidence/MaxArea/MinArea/First/Last`。
- `Index` (`int`, 默认 `0`): `Mode=Index` 时使用。
- `LabelFilter` (`string`, 默认空): 仅在标签匹配的子集内选择。

## 行为说明
- 严格只接受 `List` 输入，不再兼容旧输入键 `Items`。
- `LabelFilter` 先过滤，再按 `Mode` 选择目标元素。
- `Index` 输出固定为原始输入列表索引，不是过滤后子集索引。
- `MaxConfidence/MaxArea/MinArea` 使用单次遍历选择，不再全排序。

## 变更记录
- `1.1.0` (2026-04-12): 收口 `List` 单输入、声明 `LabelFilter`、`Index` 语义改为原始索引、极值选择改为单次遍历。
