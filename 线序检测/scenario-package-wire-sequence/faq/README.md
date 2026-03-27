# FAQ

## Why does sequence mismatch happen when detections exist?

Use `DetectionSequenceJudge` with `SortBy=CenterX`, verify `BoxNms` is enabled, and make sure the fixed ROI excludes irrelevant objects.

## What should be tuned first on site?

Calibrate fixed ROI once per station, then tune only these runtime parameters first:

- `BoxNms.IouThreshold`
- `BoxNms.ScoreThreshold`

Do not treat these as runtime tuning targets:

- `ExpectedLabels`
- `ExpectedCount`
- model path
- labels path when the model has no metadata names
- `DetectionSequenceJudge.MinConfidence`

## Auto-tune Boundary

- Auto-tune for `wire-sequence-terminal` only changes:
  - `BoxNms.ScoreThreshold`
  - `BoxNms.IouThreshold`
- Do not auto-change:
  - `ExpectedLabels`
  - `ExpectedCount`
  - `ModelPath`
  - `LabelsPath` unless the model lacks metadata names

## Missing Assets

If preview or auto-tune returns `missing_model` / `missing_labels`:

1. Check whether `DeepLearning.ModelPath` is configured and points to the correct model.
2. If the model does not expose metadata names, configure `DeepLearning.LabelsPath` or place a matching `labels.txt` next to the model.
3. If the repository intentionally keeps model binaries out of source control, confirm the external delivery path first.
4. Do not continue tuning until the resource issue is resolved.

## How to release a new package version?

1. Register new artifact versions (template/model/rule/label).
2. Mark target artifact versions as active.
3. Generate a new manifest with package version update.
4. Add `versions/<newVersion>/release.json`.
