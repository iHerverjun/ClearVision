# FAQ

## Why does sequence mismatch happen when detections exist?

Use `DetectionSequenceJudge` with `SortBy=CenterX` and verify ROI excludes irrelevant objects.

## What should be tuned first on site?

Tune only these first:

- ROI position and size
- Model path
- Confidence threshold
- Target classes
- Expected labels sequence

## How to release a new package version?

1. Register new artifact versions (template/model/rule/label).
2. Mark target artifact versions as active.
3. Generate a new manifest with package version update.
4. Add `versions/<newVersion>/release.json`.
