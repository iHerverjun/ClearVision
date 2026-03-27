# Changelog

## Unreleased

- Fixed the wire label-set ordering to match the exported ONNX class order: `Wire_Blue`, `Wire_Black`.
- Clarified that `labels.txt` represents model `classId -> label` order, while `expectedSequence` remains the business inspection order `Wire_Black -> Wire_Blue`.

## 1.4.0 - 2026-03-24

- Switched the wire-sequence template from ROI image cropping to full-image detection followed by ROI-region box filtering.
- Replaced the `RoiManager -> ImageResize` front-end path with `BoxFilter(FilterMode=Region)` after `DeepLearning`.
- Kept the two-wire top-to-bottom contract `Wire_Black -> Wire_Blue` unchanged while aligning inference with full-frame training data.

## 1.3.0 - 2026-03-24

- Narrowed the default terminal wire-sequence contract to two labels: `Wire_Black` and `Wire_Blue`.
- Updated the template, rule, manifest, labels, and sample metadata to expect exactly two detections in top-to-bottom order.
- Bumped the recommended model artifact path/version to `wire-seq-yolo-v1.2.onnx`.

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
