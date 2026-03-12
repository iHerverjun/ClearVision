# 画布算子端子断开连接改进计划

<!-- DOC_AUDIT_STATUS_START -->
## 文档审计状态（自动更新）
- 审计日期：2026-03-02
- 完成状态：未完成
- 任务统计：总计 24，已完成 0，未完成 24，待办关键词命中 12
- 判定依据：任务清单尚未开始勾选
<!-- DOC_AUDIT_STATUS_END -->

## TL;DR

> **目标**: 实现点击已连接的算子端子即可断开连接的功能，提升用户操作便利性。
>
> **当前问题**: 端子连接后，只能通过右键点击连接线或删除算子来断开，操作繁琐。
>
> **解决方案**: 在端口点击事件中增加"已连接检测"逻辑，点击已连接端口时直接断开对应连接。
>
> **涉及文件**: 
> - `flowCanvas.js` - 核心画布逻辑（主要修改）
> - `flowEditorInteraction.js` - 编辑器交互（次要调整）
>
> **预估工作量**: 短期任务（2-3个TODO）
> **并行执行**: 否（单文件顺序修改）

---

## 上下文分析

### 当前架构

```
用户操作流程:
1. 点击输出端口 → startConnection() → 进入连线模式
2. 移动鼠标 → 显示临时连线
3. 点击输入端口 → finishConnection() → 创建连接
4. 连接建立后存储在 connections 数组中

连接断开方式（现有）:
- 右键点击连接线 → 删除该连接
- 选中连接线 + Delete键 → 删除该连接
- 删除算子 → 自动删除相关连接
```

### 关键代码位置

**flowCanvas.js**
- `handleMouseDown()` (line 1237) - 鼠标按下事件处理
- `startConnection()` (line 576) - 开始连线
- `finishConnection()` (line 588) - 完成连线
- `removeConnection()` (line 735) - 删除连接
- `connections` 数组 - 存储所有连接

**连接数据结构**
```javascript
connection = {
    id: uuid,
    source: nodeId,      // 源节点ID
    sourcePort: index,   // 源端口索引
    target: nodeId,      // 目标节点ID
    targetPort: index    // 目标端口索引
}
```

### 问题根因

当前 `handleMouseDown` 中的端口点击处理逻辑：

```javascript
// line 1245-1257
const port = this.getPortAt(x, y);
if (port) {
    if (port.isOutput) {
        // 从输出端口开始连线 - 没有检查是否已有连接
        this.startConnection(port.nodeId, port.portIndex);
        return;
    } else if (this.isConnecting) {
        // 从输入端口完成连线
        this.finishConnection(port.nodeId, port.portIndex);
        return;
    }
}
```

**缺陷**: 没有检测点击的端口是否已有连接，无法实现"点击已连接端口即断开"的交互。

---

## 改进方案设计

### 方案对比

| 方案 | 描述 | 优点 | 缺点 | 推荐度 |
|------|------|------|------|--------|
| **A. 直接断开** | 点击已连接端口立即断开 | 简单直接，操作最快捷 | 可能误触 | ★★★★★ |
| **B. 提示确认** | 点击后弹出确认对话框 | 避免误操作 | 打断流畅性 | ★★★☆☆ |
| **C. 拖拽断开** | 拖拽已连接端口来断开 | 符合直觉 | 实现复杂 | ★★☆☆☆ |
| **D. 快捷键断开** | Alt+点击断开 | 精确控制 | 需要学习成本 | ★★★☆☆ |

**选定方案**: **方案A - 直接断开**，配合视觉反馈（toast提示）。

理由：
1. 大多数可视化编程工具（Node-RED、Unreal Blueprint）都采用此模式
2. 用户明确表达了"想要更改只能完全删除算子重新连接"的困扰，说明需要快速断开能力
3. 可通过撤销功能（Ctrl+Z）恢复误操作
4. 配合视觉反馈降低误触影响

### 交互流程

```
用户点击端口
    │
    ▼
检测端口是否有连接
    │
    ├── 是 → 删除该连接 + 显示提示"连接已断开"
    │           │
    │           ▼
    │       用户可立即重新连线（如果需要）
    │
    └── 否 → 保持现有行为（开始/完成连线）
```

### 视觉反馈设计

1. **断开时**: 显示 toast 提示 "连接已断开"（info级别，2秒自动消失）
2. **悬停已连接端口**: 光标变为 `not-allowed` 或显示断开图标
3. **断开后**: 端口可能有短暂的断开动画（可选增强）

---

## 工作计划

### Wave 1: 核心功能实现

#### TODO 1: 实现端口连接检测方法

**What to do**:
- 在 `FlowCanvas` 类中新增 `getConnectionAtPort(nodeId, portIndex, isOutput)` 方法
- 遍历 `this.connections` 数组，查找匹配的连接
- 返回连接对象或 null

**Code Location**: `flowCanvas.js` (line 530附近，在 `getPortAt` 方法之后)

**Implementation Details**:
```javascript
/**
 * 获取指定端口上的连接
 * @param {string} nodeId - 节点ID
 * @param {number} portIndex - 端口索引
 * @param {boolean} isOutput - 是否是输出端口
 * @returns {Object|null} 连接对象
 */
getConnectionAtPort(nodeId, portIndex, isOutput) {
    return this.connections.find(conn => {
        if (isOutput) {
            return conn.source === nodeId && conn.sourcePort === portIndex;
        } else {
            return conn.target === nodeId && conn.targetPort === portIndex;
        }
    }) || null;
}
```

**Acceptance Criteria**:
- [ ] 方法正确返回已存在的连接对象
- [ ] 方法在未连接时返回 null
- [ ] 正确区分输入端口和输出端口

**QA Scenario**:
```
Tool: Bash (node REPL)
Preconditions: 创建测试环境，添加两个节点并建立连接
Steps:
  1. 调用 getConnectionAtPort(sourceId, 0, true)
  2. 验证返回连接对象且 conn.source === sourceId
  3. 调用 getConnectionAtPort(sourceId, 1, true) 
  4. 验证返回 null（未连接的端口）
Expected Result: 方法行为符合预期
Evidence: .sisyphus/evidence/task-1-port-detection.log
```

**Parallelization**: 
- Can Run In Parallel: NO
- Blocks: TODO 2

**Commit**: YES
- Message: `feat(canvas): add port connection detection method`
- Files: `flowCanvas.js`

---

#### TODO 2: 修改 handleMouseDown 支持断开连接

**What to do**:
- 修改 `handleMouseDown` 方法中的端口点击处理逻辑
- 在 `port.isOutput` 分支中增加连接检测
- 如果端口已有连接，调用 `removeConnection` 并显示提示
- 如果端口未连接，保持原有行为

**Code Location**: `flowCanvas.js` line 1245-1257

**Implementation Details**:
```javascript
// 修改后的逻辑
const port = this.getPortAt(x, y);
if (port) {
    if (port.isOutput) {
        // 检查输出端口是否已有连接
        const existingConn = this.getConnectionAtPort(port.nodeId, port.portIndex, true);
        if (existingConn) {
            // 断开现有连接
            this.removeConnection(existingConn.id);
            if (window.showToast) {
                window.showToast('连接已断开', 'info');
            }
            console.log('[FlowCanvas] 已断开连接:', existingConn.id);
        } else {
            // 没有连接，开始新的连线
            this.startConnection(port.nodeId, port.portIndex);
        }
        return;
    } else if (this.isConnecting) {
        // 输入端口逻辑保持不变
        this.finishConnection(port.nodeId, port.portIndex);
        return;
    } else {
        // 新增：点击输入端口时也可以断开
        const existingConn = this.getConnectionAtPort(port.nodeId, port.portIndex, false);
        if (existingConn) {
            this.removeConnection(existingConn.id);
            if (window.showToast) {
                window.showToast('连接已断开', 'info');
            }
            console.log('[FlowCanvas] 已断开连接:', existingConn.id);
            return;
        }
    }
}
```

**Must NOT do**:
- 不要修改 `startConnection` 和 `finishConnection` 方法本身
- 不要改变未连接端口的点击行为
- 不要影响其他鼠标事件处理

**Acceptance Criteria**:
- [ ] 点击已有连接的输出端口 → 断开连接 + 显示提示
- [ ] 点击未连接的输出端口 → 开始连线（原有行为）
- [ ] 点击已有连接的输入端口 → 断开连接 + 显示提示
- [ ] 点击未连接的输入端口（在连线中）→ 完成连线（原有行为）
- [ ] 撤销功能（Ctrl+Z）可以恢复断开的连接

**QA Scenarios**:

```
Scenario: 断开输出端口连接
  Tool: Playwright
  Preconditions: 
    - 画布上有两个节点
    - 节点A的输出端口0已连接到节点B的输入端口0
  Steps:
    1. 打开画布页面
    2. 点击节点A的输出端口0
  Expected Result: 
    - 连接线消失
    - 显示提示"连接已断开"
    - 节点和端口保持原位
  Evidence: .sisyphus/evidence/task-2-disconnect-output.png

Scenario: 断开输入端口连接
  Tool: Playwright
  Preconditions: 
    - 画布上有两个节点
    - 节点A的输出端口0已连接到节点B的输入端口0
  Steps:
    1. 打开画布页面
    2. 点击节点B的输入端口0
  Expected Result: 
    - 连接线消失
    - 显示提示"连接已断开"
  Evidence: .sisyphus/evidence/task-2-disconnect-input.png

Scenario: 未连接端口行为保持不变
  Tool: Playwright
  Preconditions: 
    - 画布上有两个未连接的节点
  Steps:
    1. 点击节点A的输出端口0
    2. 观察鼠标变为十字准星
    3. 移动鼠标到节点B的输入端口0
    4. 点击输入端口0
  Expected Result: 
    - 步骤1后进入连线模式
    - 步骤4后创建新连接
  Evidence: .sisyphus/evidence/task-2-normal-connect.gif
```

**Parallelization**: 
- Can Run In Parallel: NO
- Blocked By: TODO 1
- Blocks: TODO 3

**Commit**: YES
- Message: `feat(canvas): enable disconnect by clicking connected ports`
- Files: `flowCanvas.js`

---

#### TODO 3: 增强悬停视觉反馈

**What to do**:
- 修改 `handleMouseMove` 中的端口悬停处理
- 当悬停在已连接端口时，改变光标样式为 `pointer` 或显示断开提示
- 可选：在已连接端口周围绘制断开指示（如红色圆环）

**Code Location**: `flowCanvas.js` line 1296-1341

**Implementation Details**:
```javascript
// 在 handleMouseMove 中修改端口悬停逻辑
const port = this.getPortAt(x, y);
if (port) {
    // 检测端口是否有连接
    const hasConnection = this.getConnectionAtPort(port.nodeId, port.portIndex, port.isOutput) !== null;
    
    if (hasConnection) {
        this.canvas.style.cursor = 'pointer'; // 或者使用 'not-allowed' 表示可断开
        this.hoveredPort = port;
        // 可以添加视觉标记，如绘制红色圆环
    } else if (this.isConnecting) {
        this.canvas.style.cursor = 'crosshair';
        this.hoveredPort = port;
    } else {
        this.canvas.style.cursor = 'pointer';
        this.hoveredPort = port;
    }
} else {
    // ... 原有逻辑
}
```

**可选增强** - 在 `drawPortHighlight` 中增加断开提示：
```javascript
drawPortHighlight(port) {
    // ... 原有高亮绘制代码
    
    // 新增：如果是已连接端口，绘制断开指示
    const hasConnection = this.getConnectionAtPort(port.nodeId, port.portIndex, port.isOutput);
    if (hasConnection && !this.isConnecting) {
        // 绘制红色X或断开图标
        this.ctx.beginPath();
        this.ctx.arc(pos.x, pos.y, 12 * this.scale, 0, Math.PI * 2);
        this.ctx.strokeStyle = 'rgba(231, 76, 60, 0.5)'; // 红色半透明
        this.ctx.setLineDash([3, 3]);
        this.ctx.stroke();
        this.ctx.setLineDash([]);
    }
}
```

**Acceptance Criteria**:
- [ ] 悬停在已连接端口时，有视觉反馈表明可以点击断开
- [ ] 悬停在未连接端口时，保持原有光标样式
- [ ] 不影响连线模式下的光标行为

**QA Scenario**:
```
Scenario: 悬停已连接端口显示断开提示
  Tool: Playwright
  Preconditions: 
    - 画布上有一个已连接的端口
  Steps:
    1. 将鼠标悬停在已连接端口上
    2. 观察光标变化和视觉反馈
  Expected Result: 
    - 光标变为 pointer
    - 端口周围显示断开指示（如虚线圆环）
  Evidence: .sisyphus/evidence/task-3-hover-feedback.png
```

**Parallelization**: 
- Can Run In Parallel: NO
- Blocked By: TODO 2

**Commit**: YES (可与 TODO 2 合并)
- Message: `feat(canvas): add visual feedback for disconnectable ports`
- Files: `flowCanvas.js`

---

#### TODO 4: 验证和边界测试

**What to do**:
- 测试各种边界情况
- 确保撤销/重做功能正常工作
- 验证序列化/反序列化不受断开操作影响
- 测试多连接情况（输出端口可连接多个输入）

**Test Cases**:

1. **多输出连接**
   - 场景：一个输出端口连接多个输入端口
   - 预期：点击输出端口应断开所有连接，或仅断开特定连接（需决策）
   - **决策**: 由于当前架构限制，点击输出端口断开所有该端口的连接

2. **撤销重做**
   - 场景：断开连接后按 Ctrl+Z
   - 预期：连接应恢复
   - 验证：历史记录正确保存

3. **序列化一致性**
   - 场景：断开连接后保存工程
   - 预期：保存的连接列表正确
   - 验证：重新加载后连接状态一致

4. **快速连续点击**
   - 场景：快速点击同一端口多次
   - 预期：行为稳定，无异常

5. **输入端口占用检查**
   - 场景：断开输入端口A的连接，立即连接到输入端口B
   - 预期：原有检查逻辑仍然有效（输入端口只能有一个连接）

**Acceptance Criteria**:
- [ ] 所有边界测试通过
- [ ] 撤销/重做功能正常
- [ ] 工程保存/加载正常
- [ ] 性能无明显下降

**QA Scenario**:
```
Scenario: 撤销断开的连接
  Tool: Playwright
  Preconditions: 
    - 画布上有一个已连接的端口
  Steps:
    1. 点击已连接端口断开连接
    2. 按 Ctrl+Z
  Expected Result: 
    - 连接恢复
    - 节点位置和其他状态不变
  Evidence: .sisyphus/evidence/task-4-undo-test.gif

Scenario: 保存后加载验证
  Tool: Playwright
  Preconditions: 
    - 画布上有多个连接
  Steps:
    1. 断开一些连接
    2. 保存工程
    3. 重新加载页面
    4. 加载刚才保存的工程
  Expected Result: 
    - 断开的连接保持断开状态
    - 保留的连接仍然存在
  Evidence: .sisyphus/evidence/task-4-serialization-test.png
```

**Parallelization**: 
- Can Run In Parallel: NO
- Blocked By: TODO 2, TODO 3

**Commit**: NO (验证任务，代码已在前序任务提交)

---

## 技术细节补充

### 关于输出端口的多连接

当前架构中，**输出端口可以连接多个输入端口**，而输入端口只能有一个连接。

**决策**: 当点击已有连接的输出端口时，**断开该端口的所有连接**。

理由：
1. 输出端口可以连接多个目标，无法确定用户想断开哪一个
2. 断开所有连接让用户重新选择是合理的
3. 可通过撤销恢复

**实现调整**（TODO 2 中需要修改）：
```javascript
if (port.isOutput) {
    // 获取该端口的所有连接
    const existingConns = this.connections.filter(conn => 
        conn.source === port.nodeId && conn.sourcePort === port.portIndex
    );
    
    if (existingConns.length > 0) {
        // 断开所有连接
        existingConns.forEach(conn => this.removeConnection(conn.id));
        if (window.showToast) {
            window.showToast(`已断开 ${existingConns.length} 个连接`, 'info');
        }
    } else {
        // 没有连接，开始新的连线
        this.startConnection(port.nodeId, port.portIndex);
    }
    return;
}
```

### 与 FlowEditorInteraction 的协调

`FlowEditorInteraction.js` 也重写了 `handleMouseDown`，但它是包装原方法：

```javascript
// flowEditorInteraction.js line 46
this.canvas.handleMouseDown = (e) => {
    // ... 新逻辑
    // 调用原方法
    originalMouseDown(e);
};
```

**风险**: 如果 `FlowEditorInteraction` 在 `FlowCanvas` 之后初始化，它会覆盖我们的修改。

**解决方案**:
1. 确保修改在 `FlowCanvas` 层面完成
2. 或者调整 `FlowEditorInteraction` 的初始化逻辑

建议：先在 `FlowCanvas` 实现，然后验证 `FlowEditorInteraction` 是否影响行为。

---

## 风险与缓解

| 风险 | 影响 | 缓解措施 |
|------|------|----------|
| 误触导致连接断开 | 中 | 提供撤销功能 + 视觉反馈 |
| 与现有交互冲突 | 中 | 保持未连接端口行为不变 |
| FlowEditorInteraction 覆盖 | 高 | 验证初始化顺序，必要时调整 |
| 性能影响（频繁查找） | 低 | 连接数组通常很小，影响可忽略 |
| 多连接输出端口行为不明确 | 中 | 文档说明 + 断开所有连接 |

---

## 最终验证清单

- [x] 点击已连接的输出端口断开所有该端口的连接
- [x] 点击已连接的输入端口断开该连接
- [x] 点击未连接端口保持原有行为
- [x] 悬停已连接端口有视觉反馈
- [x] 显示断开提示信息
- [ ] 撤销功能可恢复断开的连接（2026-03-07 复核：交互层 `undo()` 与脚本派发按键可恢复，但真实浏览器 `Ctrl+Z` 自动化证据仍未稳定）
- [x] 保存/加载工程正常（已验证断开后的状态可正确保存并重新加载）
- [ ] 不影响其他画布功能（拖拽、缩放、框选等）（2026-03-07 复核：官方 UI smoke `4/4` 通过；框选证据仍缺，暂不归档）
- [x] 代码通过现有测试（2026-03-07 在 `Acme.Product/tests/Acme.Product.UI.Tests` 运行 Playwright smoke，`editor.spec.ts` + `project.spec.ts` 共 `4/4` 通过）

---

## 附录: 完整修改代码片段

### flowCanvas.js - getConnectionAtPort 方法（新增）

```javascript
/**
 * 获取指定端口上的连接
 * @param {string} nodeId - 节点ID
 * @param {number} portIndex - 端口索引
 * @param {boolean} isOutput - 是否是输出端口
 * @returns {Object|null} 连接对象或null
 */
getConnectionAtPort(nodeId, portIndex, isOutput) {
    if (isOutput) {
        // 输出端口可能有多个连接，返回第一个
        return this.connections.find(conn => 
            conn.source === nodeId && conn.sourcePort === portIndex
        ) || null;
    } else {
        // 输入端口只能有一个连接
        return this.connections.find(conn => 
            conn.target === nodeId && conn.targetPort === portIndex
        ) || null;
    }
}

/**
 * 获取指定端口上的所有连接（用于输出端口）
 * @param {string} nodeId - 节点ID
 * @param {number} portIndex - 端口索引
 * @param {boolean} isOutput - 是否是输出端口
 * @returns {Array} 连接对象数组
 */
getConnectionsAtPort(nodeId, portIndex, isOutput) {
    if (isOutput) {
        return this.connections.filter(conn => 
            conn.source === nodeId && conn.sourcePort === portIndex
        );
    } else {
        const conn = this.connections.find(conn => 
            conn.target === nodeId && conn.targetPort === portIndex
        );
        return conn ? [conn] : [];
    }
}
```

### flowCanvas.js - handleMouseDown 修改

```javascript
handleMouseDown(e) {
    const rect = this.canvas.getBoundingClientRect();
    const x = (e.clientX - rect.left) / this.scale + this.offset.x;
    const y = (e.clientY - rect.top) / this.scale + this.offset.y;

    // 更新鼠标位置
    this.mousePosition = { x, y };

    // 首先检测是否点击了端口
    const port = this.getPortAt(x, y);
    if (port) {
        if (port.isOutput) {
            // 【新增】检查输出端口是否已有连接
            const existingConns = this.getConnectionsAtPort(port.nodeId, port.portIndex, true);
            
            if (existingConns.length > 0) {
                // 断开该端口的所有连接
                existingConns.forEach(conn => {
                    this.removeConnection(conn.id);
                });
                if (window.showToast) {
                    const msg = existingConns.length === 1 
                        ? '连接已断开' 
                        : `已断开 ${existingConns.length} 个连接`;
                    window.showToast(msg, 'info');
                }
                console.log('[FlowCanvas] 已断开连接:', existingConns.map(c => c.id));
            } else {
                // 没有连接，从输出端口开始连线
                this.startConnection(port.nodeId, port.portIndex);
            }
            return;
        } else if (this.isConnecting) {
            // 从输入端口完成连线
            this.finishConnection(port.nodeId, port.portIndex);
            return;
        } else {
            // 【新增】点击输入端口时检查是否已有连接
            const existingConn = this.getConnectionAtPort(port.nodeId, port.portIndex, false);
            
            if (existingConn) {
                // 断开该输入端口的连接
                this.removeConnection(existingConn.id);
                if (window.showToast) {
                    window.showToast('连接已断开', 'info');
                }
                console.log('[FlowCanvas] 已断开连接:', existingConn.id);
                return;
            }
        }
    }

    // 如果在连线状态但点击了空白处，取消连线
    if (this.isConnecting) {
        this.cancelConnection();
        return;
    }

    // 查找点击的节点...（后续代码保持不变）
}
```

---

**计划完成。执行 `/start-work` 开始实施。**
