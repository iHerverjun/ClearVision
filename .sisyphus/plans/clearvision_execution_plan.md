# ClearVision Execution Plan

<!-- DOC_AUDIT_STATUS_START -->
## 文档审计状态（自动更新）
- 审计日期：2026-03-02
- 完成状态：未完成
- 任务统计：总计 29，已完成 0，未完成 29，待办关键词命中 1
- 判定依据：任务清单尚未开始勾选
<!-- DOC_AUDIT_STATUS_END -->

## TL;DR

> **Quick Summary**: Complete the transition from "Prototype" to "Production-Ready" by filling backend CQRS gaps, finalizing the custom Flow Editor, and establishing a robust real-time data pipeline.
> 
> **Deliverables**:
> - Fully implemented CQRS Handlers (Commands/Queries)
> - Optimized Real-time Image Streaming (Native WebView2)
> - Type-Safe Frontend (C# DTOs -> TypeScript Interfaces)
> - Production-Ready Flow Editor (Custom Canvas)
> 
> **Estimated Effort**: Large (approx. 3-4 weeks)
> **Parallel Execution**: YES - Backend and Frontend tracks can run concurrently.

---

## Context

### Current Status (Sprint 5 Post-Analysis)
- **Backend**: DDD structure exists, but Application layer is thin (Services exist, but CQRS Handlers are empty). 9 Operators implemented.
- **Frontend**: Basic structure and Custom Canvas (`flowCanvas.js`) exist.
- **Infrastructure**: Logging and basic DB context exist, but build environment shows missing references (LSP errors).
- **Gaps**: Real-time streaming, Type safety, Serialization, and comprehensive Testing.

### Key Technical Decisions
- **Flow Editor**: **Refine Custom Canvas**. Stick to `flowCanvas.js` to avoid rewrite cost of switching to React Flow.
- **Type Sync**: **TypeGen**. Use `TypeGen` CLI tool in build process to generate TS interfaces from C# DTOs.
- **Real-time**: **Native WebView2**. Use `PostWebMessageAsJson` for zero-latency local streaming (avoiding SignalR overhead).
- **Serialization**: **JSON Only**. System.Text.Json for all project files.

---

## Work Objectives

### Core Objective
Complete all missing `TODO.md` items to reach "Feature Complete" (Beta) status.

### Concrete Deliverables
- [ ] `Acme.Product.Application` populated with 10+ Command/Query Handlers.
- [ ] `webMessageBridge.js` upgraded to handle binary image streams.
- [ ] `scripts/generate-types.sh` for DTO sync.
- [ ] `Playwright` test suite for UI verification.

### Definition of Done
- [ ] `dotnet build` passes with 0 warnings.
- [ ] All 39 unit tests pass + New Integration tests.
- [ ] Flow Editor can Create/Save/Load/Execute a 5-node graph.
- [ ] Real-time FPS > 30Hz for 1080p images.

---

## Execution Strategy

### Parallel Execution Waves

```
Wave 1 (Foundation & Stabilization):
├── Task 1.1: Build Fixes & CI Setup (DevOps)
├── Task 1.2: CQRS Infrastructure (Backend)
└── Task 1.3: Type Sync Setup (Shared)

Wave 2 (Core Development - Parallel):
├── Track A (Backend): Command/Query Implementation
│   ├── Task 2.1: Project Commands (Create/Update)
│   ├── Task 2.2: Inspection Commands (Execute)
│   └── Task 2.3: Data Export (CSV/JSON)
└── Track B (Frontend): Editor & Streaming
    ├── Task 3.1: Real-time Image Stream (Native)
    ├── Task 3.2: Flow Editor Interactions (Drag/Drop/Connect)
    └── Task 3.3: UI Components (Toast/Dialog/Input)

Wave 3 (QA & Polish):
├── Task 4.1: UI Testing (Playwright)
├── Task 4.2: Performance Tuning (MatPool/Rendering)
└── Task 4.3: Release Packaging
```

---

## TODOs

### Phase 1: Foundation & Stabilization (P0)

- [ ] 1.1 **Fix Build & Dependencies**
  - **Goal**: Resolve all LSP errors and missing references.
  - **Action**: Run `dotnet restore`. Verify `OpenCvSharp4`, `Serilog`, `MediatR` packages are correctly referenced in all projects.
  - **Verify**: `dotnet build --no-incremental` returns 0 errors.

- [ ] 1.2 **Setup CI/CD Pipeline**
  - **Goal**: Automated verification.
  - **Action**: Create `.github/workflows/ci.yml`.
  - **Steps**: Checkout -> Setup .NET -> Restore -> Build -> Test -> Publish Artifacts.

- [ ] 1.3 **Type Safety Setup (TypedSignalR/TypeGen)**
  - **Goal**: Auto-generate TS interfaces from C# DTOs.
  - **Recommendation**: Use `TypedSignalR.Client.TypeScript` (Source Generator) for best integration.
  - **Alternative**: `TypeGen` (CLI) for simpler setup.
  - **Action**: Configure generation to output `.ts` files to `wwwroot/src/types`.

### Phase 2: Backend Core (P1)

- [ ] 2.1 **Implement Project CQRS Handlers**
  - **Goal**: Move logic from Services to Handlers.
  - **Files**: `CreateProjectCommandHandler.cs`, `GetProjectQueryHandler.cs`, `UpdateFlowCommandHandler.cs`.
  - **Ref**: Use `ProjectRepository` and `AutoMapper`.

- [ ] 2.2 **Implement Inspection CQRS Handlers**
  - **Goal**: Execute detection flow.
  - **Files**: `ExecuteInspectionCommandHandler.cs`, `GetInspectionHistoryQueryHandler.cs`.
  - **Logic**: Trigger `IFlowExecutionService`, save to `InspectionResultRepository`.

- [ ] 2.3 **Serialization & Export**
  - **Goal**: Save/Load projects and export results.
  - **Action**: Implement `ProjectJsonSerializer` (System.Text.Json). Implement `ResultCsvExporter` (CsvHelper).

- [ ] 2.4 **Image Data & Management**
  - **Goal**: Efficient image handling.
  - **Action**: Optimize `MatPool` (Object Pool pattern for OpenCvSharp Mat). Implement `ImageFileManager` to save/load images from disk (not DB).

### Phase 3: Frontend & Integration (P1)

- [ ] 3.1 **Real-time Image Streaming (SharedMemory)**
  - **Goal**: Zero-copy high-performance image display (>30 FPS).
  - **Action**: Implement `CoreWebView2SharedBuffer` in `WebView2Host.cs`.
  - **Mechanism**: 
    1. Backend: Write `Mat` data to Shared Buffer.
    2. Frontend: Read `ArrayBuffer` from shared memory and render to `OffscreenCanvas`.
  - **Fallback**: Keep `PostWebMessageAsJson` (Base64) for small metadata/logs only.

- [ ] 3.2 **Flow Editor Core Completion**
  - **Goal**: Fully functional graph editor.
  - **Tasks**:
    - [ ] **Drag & Drop**: From Sidebar to Canvas.
    - [ ] **Connection Validation**: Check Port Types (e.g., Image -> Image).
    - [ ] **Selection API**: Multi-select, Delete, Copy/Paste.
    - [ ] **Zoom/Pan**: Infinite canvas navigation.

- [ ] 3.3 **UI Component Library**
  - **Goal**: Reusable UI elements.
  - **Create**: `Button`, `Input` (Text/Number), `Modal`, `Toast` (Notification), `SplitPanel`.
  - **Ref**: Use CSS Variables from `variables.css`.

### Phase 4: Quality Assurance (P2)

- [ ] 4.1 **UI Automation Tests**
  - **Goal**: E2E verification.
  - **Action**: Initialize Playwright project in `Tests/UI`.
  - **Cases**: App Launch -> Create Project -> Drag Operator -> Run -> Verify Result.

- [ ] 4.2 **Performance Optimization**
  - **Goal**: Stable 30FPS.
  - **Action**: Profile with `dotTrace`. Optimize `OnPaint` in Canvas. Ensure `Mat` objects are disposed in backend.

- [ ] 4.3 **Documentation & Release**
  - **Goal**: User-ready.
  - **Action**: Write `README.md`, `USER_MANUAL.md`. Configure `dotnet publish` for Single File execution.

---

## Success Criteria

### Final Verification
- [ ] Application starts without crashing.
- [ ] "Create Project" -> "Add Gaussian Blur" -> "Run" works end-to-end.
- [ ] Memory usage stays < 500MB during continuous inspection.
- [ ] All code committed to Git.
