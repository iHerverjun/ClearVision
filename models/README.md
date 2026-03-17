# 模型仓库 / Model Repository

该目录用于承载阶段3的模型与特征库索引，入口文件为 `models/model_catalog.json`。

当前仓库内默认附带的是轻量级测试资产，便于：

- 语义分割算子通过 `ModelId` 直接解析 ONNX 模型
- 异常检测算子通过 `ModelId` 解析特征库文件
- 自动化测试和 Demo 文档引用统一模型索引

推荐目录结构：

```text
models/
  model_catalog.json
  segmentation/
  anomaly_detection/
  object_detection/
```

`model_catalog.json` 中的 `path` 字段支持：

- 绝对路径
- 相对 `models/` 目录的路径
- 相对仓库根目录的路径

真实业务模型与大文件资产建议按需放置在仓库外部，再通过绝对路径或部署时生成的 catalog 挂载。
