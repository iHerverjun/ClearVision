# JsonExtractor

## 基本信息
- 类名: `JsonExtractorOperator`
- 枚举: `OperatorType.JsonExtractor`
- 分类: `数据处理`

## 输入与输出
- 输入:
- `Json` (`String`, 必填): JSON 字符串。
- 输出:
- `Value` (`Any`): 提取值或默认值。
- `IsSuccess` (`Boolean`): 路径是否命中。

## 参数契约
- `JsonPath` (`string`, 默认 `$.data`): 目标路径，支持 `$.a.b` 与数组下标（如 `$.items[0].id`）。
- `OutputType` (`string`, 默认 `Any`): `Any/String/Float/Double/Integer/Int/Boolean/Bool`。
- `DefaultValue` (`string`, 默认空): 未命中且 `Required=false` 时返回的默认值。
- `Required` (`bool`, 默认 `false`): 未命中时是否直接失败。

## 行为说明
- 仅使用正式参数 `JsonPath/OutputType/DefaultValue/Required`。
- 运行时仅输出 `Value/IsSuccess` 两个正式输出键。
- 不再依赖旧别名参数 `Path`，也不再输出旧键 `Found`。

## 变更记录
- `1.1.0` (2026-04-12): 严格收口到正式契约，删除旧别名依赖，统一输出键为 `Value/IsSuccess`。
