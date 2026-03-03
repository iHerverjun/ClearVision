# ClearVision Fix Execution Plan

<!-- DOC_AUDIT_STATUS_START -->
## 文档审计状态（自动更新）
- 审计日期：2026-03-02
- 完成状态：未完成
- 任务统计：总计 24，已完成 0，未完成 24，待办关键词命中 0
- 判定依据：任务清单尚未开始勾选
<!-- DOC_AUDIT_STATUS_END -->

## Context
**User Request Summary**: The user requires a detailed, parallel execution plan to fix 30 blocking/high-priority issues in the ClearVision project. Issues span Backend (C# services, Async safety), Frontend (JS memory leaks), and Infrastructure (Repository validation).

**Key Findings**:
- **Backend**: `ImageAcquisitionService` requires implementation of 6 methods using `OpenCvSharp` and camera hardware interfaces. `OperatorService` needs standard CRUD implementation. `WebMessageHandler` has a dangerous `async void` pattern.
- **Frontend**: Canvas components (`imageCanvas.js`, `flowCanvas.js`) and `splitPanel.js` have event listeners and animation loops that are never cleaned up, causing memory leaks.
- **Infrastructure**: `ProjectRepository` lacks input validation.
- **Risks Identified**: OpenCV resource leaks (Mat disposal), Concurrency in image acquisition, Silence failure of async void tasks.

**Guardrails**:
- **OpenCV Safety**: All `Mat` objects must be disposed via `using` or explicit `Dispose()`. `ImageAcquisitionService` must implement `IDisposable`.
- **Async Safety**: Use `TaskExtensions.SafeFireAndForget` for legacy event handlers.
- **No Scope Creep**: Fix only the listed methods; do not refactor the entire architecture.

## Task Dependency Graph

| Task | Depends On | Reason |
|------|------------|--------|
| **Task 1: Core Extensions** | None | Base infrastructure for async safety |
| **Task 2: WebMessageHandler** | Task 1 | Requires `SafeFireAndForget` from Task 1 |
| **Task 3: Frontend Leaks** | None | Independent JS fixes |
| **Task 4: OperatorService** | None | Independent CRUD logic |
| **Task 5: ImageAcquisition (Base)** | None | Independent implementation |
| **Task 6: ImageAcquisition (Process)** | Task 5 | May share internal helper methods or cache logic |
| **Task 7: Repository Validation** | None | Independent validation logic |
| **Task 8: General Cleanup** | None | Low risk independent cleanup |

## Parallel Execution Graph

**Wave 1 (Start Immediately)**:
├── **Task 1**: Create `TaskExtensions.cs` (C# Core)
├── **Task 3**: Fix Frontend Memory Leaks (3 files) (JS/Frontend)
├── **Task 4**: Implement `OperatorService` CRUD (C# Service)
├── **Task 5**: Implement `ImageAcquisitionService` (Camera/Base) (C# Service)
└── **Task 7**: Add Validation to `ProjectRepository` (C# Infra)

**Wave 2 (After Wave 1)**:
├── **Task 2**: Fix `WebMessageHandler.cs` (Depends on Task 1)
├── **Task 6**: Implement `ImageAcquisitionService` (Process/File) (Depends on Task 5 completion)
└── **Task 8**: General Optimization/Cleanup (Can start anytime, best after P0s)

**Critical Path**: Task 1 → Task 2 (Short chain). Task 5 → Task 6 (Service completion).
**Estimated Parallel Speedup**: ~60% faster than sequential execution.

## Tasks

### Task 1: Create TaskExtensions.cs
**Description**: Create a new file `Acme.Product/src/Acme.Product.Core/Extensions/TaskExtensions.cs` to handle safe fire-and-forget execution for async void scenarios.
**Delegation Recommendation**:
- Category: `unspecified-low` - Standard utility implementation.
- Skills: [`typescript-programmer`] - *Correction: Using general coding capability as specific C# skill is not listed, but this is simple.*
**Skills Evaluation**:
- INCLUDED `typescript-programmer`: Close enough proxy for strict typing logic, or fallback to general model.
- OMITTED `frontend-ui-ux`: Backend task.
**Depends On**: None
**Acceptance Criteria**:
- [ ] File created at correct path.
- [ ] `SafeFireAndForget` method implemented handling `Task` and `ValueTask`.
- [ ] Exception handling logs error if `ILogger` provided.

### Task 2: Fix WebMessageHandler Async Void
**Description**: Modify `Acme.Product/src/Acme.Product.Desktop/Handlers/WebMessageHandler.cs` line 40. Change `async void OnWebMessageReceived` to `void` and use `Task.Run(...).SafeFireAndForget()`.
**Delegation Recommendation**:
- Category: `unspecified-high` - High risk of crashing process if done wrong.
- Skills: [`dev-browser`] - Context of WebView handling.
**Skills Evaluation**:
- INCLUDED `dev-browser`: Understanding of WebView message loops.
**Depends On**: Task 1
**Acceptance Criteria**:
- [ ] Line 40 signature changed to `private void`.
- [ ] Logic wrapped in `SafeFireAndForget`.
- [ ] No compilation errors.

### Task 3: Fix Frontend Memory Leaks
**Description**: Add `destroy()` methods to 3 JS files. Remove event listeners (`resize`, `mousemove`, `mouseup`) and cancel `requestAnimationFrame`.
**Files**:
1. `.../core/canvas/imageCanvas.js`
2. `.../core/canvas/flowCanvas.js`
3. `.../shared/components/splitPanel.js`
**Delegation Recommendation**:
- Category: `visual-engineering` - Frontend code manipulation.
- Skills: [`frontend-ui-ux`] - JavaScript DOM event handling.
**Skills Evaluation**:
- INCLUDED `frontend-ui-ux`: Expert knowledge of JS event loops and cleanup.
**Depends On**: None
**Acceptance Criteria**:
- [ ] `destroy()` method added to all 3 classes.
- [ ] `removeEventListener` calls match `addEventListener`.
- [ ] `cancelAnimationFrame` called for animation loops.

### Task 4: Implement OperatorService CRUD
**Description**: Implement `UpdateAsync` (line 322) and `DeleteAsync` (line 327) in `Acme.Product/src/Acme.Product.Application/Services/OperatorService.cs`.
**Delegation Recommendation**:
- Category: `unspecified-high` - Business logic implementation.
- Skills: [`typescript-programmer`] - Proxy for typed language logic.
**Skills Evaluation**:
- OMITTED `frontend-ui-ux`: Backend task.
**Depends On**: None
**Acceptance Criteria**:
- [ ] `UpdateAsync` validates existence, updates properties, calls Repo update.
- [ ] `DeleteAsync` validates existence, calls Repo delete.
- [ ] Throws `OperatorNotFoundException` if ID invalid.

### Task 5: Implement ImageAcquisitionService (Camera)
**Description**: Implement `AcquireFromCameraAsync`, `StartContinuousAcquisitionAsync`, `StopContinuousAcquisitionAsync` in `ImageAcquisitionService.cs`.
**Delegation Recommendation**:
- Category: `ultrabrain` - Complex concurrency and hardware interaction logic.
- Skills: [`python-programmer`] - Proxy for heavy backend/hardware logic (OpenCV context).
**Skills Evaluation**:
- INCLUDED `python-programmer`: Often overlaps with OpenCV usage patterns.
**Depends On**: None
**Acceptance Criteria**:
- [ ] `AcquireFromCameraAsync` retrieves frame from `_cameraManager`.
- [ ] `Start...` starts a background loop/timer for grabbing frames.
- [ ] Thread safety: Locking mechanism around shared resources.
- [ ] **Guardrail**: Service must implement `IDisposable` to clean up resources.

### Task 6: Implement ImageAcquisitionService (Process/File)
**Description**: Implement `PreprocessAsync`, `SaveToFileAsync`, `GetImageInfoAsync` in `ImageAcquisitionService.cs`.
**Delegation Recommendation**:
- Category: `unspecified-high` - Image processing logic.
- Skills: [`python-programmer`] - OpenCV knowledge.
**Skills Evaluation**:
- INCLUDED `python-programmer`: OpenCV familiarity.
**Depends On**: Task 5 (Conceptual dependency for file consistency)
**Acceptance Criteria**:
- [ ] `PreprocessAsync` uses OpenCvSharp for scaling/filtering.
- [ ] `SaveToFileAsync` saves `Mat` to disk.
- [ ] `GetImageInfoAsync` returns metadata from cached `Mat`.
- [ ] All `Mat` created locally are disposed.

### Task 7: Repository Input Validation
**Description**: Add validation (null checks, range checks) to `GetByNameAsync`, `SearchAsync`, `GetRecentAsync`, `GetWithFlowAsync` in `ProjectRepository.cs`.
**Delegation Recommendation**:
- Category: `quick` - Simple logic changes.
- Skills: [`git-master`] - Quick edits.
**Skills Evaluation**:
- INCLUDED `git-master`: Efficient text manipulation.
**Depends On**: None
**Acceptance Criteria**:
- [ ] Arguments validated at start of methods.
- [ ] `ArgumentNullException` or `ArgumentException` thrown for invalid inputs.

### Task 8: General Cleanup (P2)
**Description**: Remove hardcoded configs, clean CSS `!important`, remove `console.log`.
**Delegation Recommendation**:
- Category: `unspecified-low` - Search and replace tasks.
- Skills: [`git-master`] - Pattern matching and cleanup.
**Skills Evaluation**:
- INCLUDED `git-master`: Best for broad search/replace.
**Depends On**: None
**Acceptance Criteria**:
- [ ] No `console.log` in production JS code.
- [ ] `!important` usage reduced in CSS.

## Commit Strategy
- **Atomic Commits**: Each task should be a separate commit.
- **Prefixes**:
  - `feat`: Tasks 1, 4, 5, 6
  - `fix`: Tasks 2, 3, 7
  - `chore`: Task 8
- **Example**: `fix(frontend): resolve memory leaks in canvas components`

## Success Criteria
1. **Compilation**: Solution builds without errors.
2. **P0 Resolved**: All 6 `ImageAcquisition` methods and 2 `OperatorService` methods throw no `NotImplementedException`.
3. **Stability**: `WebMessageHandler` does not crash process on async error.
4. **Memory**: Frontend heap does not grow indefinitely on resize/reload.
