# 文档导航索引 / Docs Navigation

本页是 `docs/` 目录的统一入口，方便快速找到用户文档、部署文档、算子总索引以及逐算子说明。

## 快速入口 / Quick Links

### 使用与部署
- 用户指南：[`guides/guide-user.md`](./guides/guide-user.md)
- 部署指南：[`guides/guide-deployment.md`](./guides/guide-deployment.md)
- 代码库深度导读：[`guides/guide-codebase-deep-dive.md`](./guides/guide-codebase-deep-dive.md)

### 算子文档
- 算子总索引：[`OPERATOR_CATALOG.md`](./OPERATOR_CATALOG.md)
- 算子分类索引：[`operators/CATALOG.md`](./operators/CATALOG.md)
- 算子明细目录：`operators/*.md`
- 算子目录 JSON：[`operator_catalog.json`](./operator_catalog.json)
- 算子目录 JSON（operators 副本）：[`operators/catalog.json`](./operators/catalog.json)

### 常看补充
- 算子变更汇总：[`CHANGELOG.md`](./CHANGELOG.md)
- 算子版本历史：[`version-history.json`](./version-history.json)
- ??????????[COMPLETED_PROJECT_HISTORY.md](./COMPLETED_PROJECT_HISTORY.md)
- ?????????[DEVELOPMENT_PLANNING_CONSOLIDATED.md](./DEVELOPMENT_PLANNING_CONSOLIDATED.md)
- 当前架构审计：[`CURRENT_BUG_ARCH_AUDIT_2026-03-12.md`](./CURRENT_BUG_ARCH_AUDIT_2026-03-12.md)

## 推荐阅读顺序 / Recommended Reading Order

### 我是最终用户
1. [`guides/guide-user.md`](./guides/guide-user.md)
2. [`guides/guide-deployment.md`](./guides/guide-deployment.md)
3. [`OPERATOR_CATALOG.md`](./OPERATOR_CATALOG.md)

### 我是实施/交付同事
1. [`guides/guide-deployment.md`](./guides/guide-deployment.md)
2. [`guides/guide-user.md`](./guides/guide-user.md)
3. [`operators/CATALOG.md`](./operators/CATALOG.md)
4. `operators/*.md`

### 我是研发/维护同事
1. [`guides/guide-codebase-deep-dive.md`](./guides/guide-codebase-deep-dive.md)
2. [`OPERATOR_CATALOG.md`](./OPERATOR_CATALOG.md)
3. `operators/*.md`
4. [`operator_catalog.json`](./operator_catalog.json)

## 当前目录状态 / Current Index Snapshot
- 算子总数：**118**
- 目录平均质量分：**88.6**
- 质量分布：`A=77 / B=35 / C=6`
- 根目录索引与 `docs/operators/` 副本已同步生成

## 目录说明 / Directory Notes
- `guides/`：面向用户、部署、代码库理解的指南文档
- `operators/`：逐算子说明、分类索引、版本历史与变更记录
- `reports/`、`audits/`、`AlgorithmAudit/`：审计、评审与专题报告
- `roadmaps/`、`plans/`：规划与阶段性路线图

## 维护约定 / Maintenance Notes
- 算子目录索引的规范生成入口是：`scripts/OperatorDocGenerator/OperatorDocGenerator.csproj`
- 兼容脚本 `tools/generate_operator_catalog.csx` 已代理到上述生成器
- 若重新生成目录索引，会同步刷新：
  - `docs/OPERATOR_CATALOG.md`
  - `docs/operator_catalog.json`
  - `docs/operators/CATALOG.md`
  - `docs/operators/catalog.json`
