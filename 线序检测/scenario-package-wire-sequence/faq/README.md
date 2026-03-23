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
- labels path
- `DetectionSequenceJudge.MinConfidence`

## How to release a new package version?

1. Register new artifact versions (template/model/rule/label).
2. Mark target artifact versions as active.
3. Generate a new manifest with package version update.
4. Add `versions/<newVersion>/release.json`.
