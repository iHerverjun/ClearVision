---
title: "阶段3端到端 Demo 说明"
doc_type: "note"
status: "closed"
topic: "对标Halcon"
created: "2026-03-20"
updated: "2026-03-20"
---
# 阶段3端到端 Demo 说明

## 目标

提供一个可复现的“AI + 3D 融合”最小演示闭环，验证阶段3新增能力已经可以串成端到端流程。

## 演示入口

- 自动化用例：`Acme.Product/tests/Acme.Product.Tests/Integration/Stage3_EndToEndDemoIntegrationTests.cs`
- 相关算子：
  - `SemanticSegmentationOperator`
  - `AnomalyDetectionOperator`
  - `RansacPlaneSegmentationOperator`

## Demo 流程

1. 使用模型仓库中的 `semantic_identity_2x2` 执行语义分割。
2. 使用正常样本构建异常检测特征库。
3. 对带人工缺陷的图像执行异常检测，得到异常分数、热力图和掩码。
4. 对合成平面点云执行 RANSAC 平面分割，得到平面内点比例。
5. 使用“语义结果 + 异常检测 + 3D 平面质量”形成最终 OK/NG 判定。

## 当前结论

- 代码层面已经具备可运行的最小演示闭环。
- Demo 用例当前以“缺陷存在，因此最终判定为 NG”为预期结果。
- 若要扩展为现场演示工程，只需替换真实图像源、真实模型资产和真实点云输入。

## 与规划项映射

- W9-1：语义分割算子
- W9-2：异常检测算子
- W10-1：模型仓库
- W12-1：端到端 Demo

## 后续扩展建议

- 将当前测试闭环转换为可导入的项目文件/流程模板。
- 接入真实业务模型与真实相机输入。
- 补充 GPU 实测耗时与现场 CT 数据，形成交付版 Demo 报告。


