# Wire Sequence Scenario Package

This package is the baseline reusable package for terminal wire-sequence inspection.

## Directory Contract

- `manifest.json`: Active package manifest consumed by platform tooling.
- `versions/`: Immutable package release descriptors.
- `template/`: Flow template artifacts.
- `models/`: Model artifacts and model version notes.
- `rules/`: Rule definitions (expected order, tolerance, NG reasons).
- `labels/`: Label conventions for model output alignment.
- `samples/`: Sample references for tuning and verification.
- `faq/`: Reusable scenario knowledge and troubleshooting notes.

## Version Policy

1. Package version uses semantic versioning (`major.minor.patch`).
2. Any model/template/rule change must register a new artifact version.
3. `manifest.json` points to currently active artifact versions.
4. Every released package snapshot is recorded in `versions/<packageVersion>/release.json`.
