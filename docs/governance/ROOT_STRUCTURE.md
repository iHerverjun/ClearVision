# Root Structure Guide

## Root-Level Rules

The repository root should only contain:

- primary product workspaces
- shared tooling and automation entry points
- a single documentation entry point
- a small set of runtime files that are intentionally kept at root

The root should not accumulate:

- ad-hoc Markdown notes
- publish output
- test result dumps
- scratch frontend sandboxes
- notebooks
- dependency drop folders

## Current Top-Level Layout

| Path | Purpose |
|---|---|
| `Acme.Product/` | Main desktop/backend product workspace |
| `Acme.OperatorLibrary/` | Operator library workspace |
| `docs/` | Single documentation entry point |
| `scripts/` | Automation scripts and small generators |
| `tools/` | Support tooling and training-data utilities |
| `artifacts/` | Local publish output, test output, scratch runtime artifacts |
| `notebooks/` | Research and training notebooks |
| `vendor/` | Third-party dependency drops and legacy packages |
| `vision.db` | Local development database; keep at root until path is configurable |
| `tgconfig.json` | Local tool config; keep until usage is retired or relocated |

## Documentation Layout

| Path | Purpose |
|---|---|
| `docs/guides/` | How-to guides and usage documents |
| `docs/guides/hardware/` | Hardware/vendor-specific guides |
| `docs/plans/` | Implementation plans |
| `docs/roadmaps/` | Milestone roadmaps |
| `docs/reference/` | Reference manuals and architecture notes |
| `docs/reports/` | Review and assessment reports |
| `docs/audits/` | Audit and inspection documents |
| `docs/archive/` | Temporary or superseded docs kept for traceability |
| `docs/curation/` | Audit-generated classification and indexing output |
| `docs/completed_phases/` | Historical completed phase docs |

## Maintenance Rules

1. New project documentation should go under `docs/`, never at repository root.
2. Generated output should go under `artifacts/` or be ignored entirely.
3. Runtime data should not be moved from root unless the consuming code reads from a configurable path.
4. If a new top-level directory is proposed, define its ownership and lifecycle first.
