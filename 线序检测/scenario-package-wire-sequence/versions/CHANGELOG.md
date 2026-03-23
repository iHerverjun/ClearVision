# Changelog

## 1.2.0 - 2026-03-23

- Added structured diagnostics outputs for `BoxNms` and `DetectionSequenceJudge`.
- Rewired the template result chain to preserve image, text message, NMS diagnostics, and sequence diagnostics together.
- Disabled internal NMS inside `DeepLearning` for this scenario template and shifted threshold ownership to `BoxNms`.

## 1.1.0 - 2026-03-23

- Aligned the template to the fixed-ROI wire-root inspection baseline.
- Added `RoiManager` and `BoxNms` to the reusable flow skeleton.
- Unified template, manifest, and rule metadata for expected sequence and required resources.

## 1.0.0 - 2026-03-19

- Initial reusable package for terminal wire-sequence inspection.
- Added template/model/rule/label contracts.
- Added expected order constraints for `DetectionSequenceJudge`.
