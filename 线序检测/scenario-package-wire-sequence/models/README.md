# Models

当前场景包的激活模型契约仍然是：

- `wire-seq-yolo-v1.1.onnx`

但当前仓库默认**不提交模型二进制**。本目录只保留：

1. 模型文件名与版本约定
2. 场景包 `manifest` / `release.json` 使用的相对路径契约
3. 本地或现场放置模型时的检查说明

## 外置模型交付规则

- 本地开发或现场调试时，将真实 ONNX 文件放到：
  - `线序检测/scenario-package-wire-sequence/models/wire-seq-yolo-v1.1.onnx`
- 如果不希望把模型放进仓库，也必须保证 `DeepLearning.ModelPath` 指向一份实际存在的外置文件。
- CI 默认只校验路径契约与文档一致性，不强依赖私有模型二进制。

## 标签一致性

- 模型输出类别必须与 `../labels/labels.txt` 对齐：
  - `Wire_Brown`
  - `Wire_Black`
  - `Wire_Blue`

## 版本说明

- `1.2.0` 仍沿用 `wire-seq-yolo-v1.1.onnx`。
- 后续只要模型文件、类别顺序或推理前处理发生变化，都必须同步更新：
  - `manifest.json`
  - `versions/<packageVersion>/release.json`
  - `README.md`
