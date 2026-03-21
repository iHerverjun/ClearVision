---
title: "Dataview 工作台"
doc_type: "index"
status: "template"
topic: "文档索引"
created: "2026-03-21"
updated: "2026-03-21"
---

# Dataview 工作台

- [Active 索引](./active/索引.md)
- [Needs-Review 索引](./needs-review/索引.md)
- [Closed 索引](./closed/索引.md)

## 全局总览

```dataview
TABLE WITHOUT ID
  file.link AS 文档,
  status AS 状态,
  topic AS 主题,
  doc_type AS 类型,
  updated AS 更新
FROM "docs"
WHERE contains(list("active", "needs-review", "closed"), status) AND doc_type != "index"
SORT status ASC, topic ASC, updated DESC
```
