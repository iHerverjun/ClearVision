# Wire Sequence Scenario Package

This package is the baseline reusable package for full-image terminal wire-sequence inspection with ROI-based detection filtering.

## Directory Contract

- `manifest.json`: Active package manifest consumed by platform tooling.
- `versions/`: Immutable package release descriptors.
- `template/`: Flow template artifacts.
- `models/`: Model artifacts and model version notes.
- `rules/`: Rule definitions (expected order, tolerance, NG reasons).
- `labels/`: Model class-order labels used for `classId -> label` alignment.
- `samples/`: Sample references for tuning and verification.
- `faq/`: Reusable scenario knowledge and troubleshooting notes.

## Baseline Flow

Current golden flow skeleton:

`ImageAcquisition -> DeepLearning -> BoxFilter(FilterMode=Region) -> BoxNms -> DetectionSequenceJudge -> ResultOutput`

Current diagnostics contract:

- `DeepLearning` runs on the full image, keeps a low confidence floor, and disables internal NMS for this scenario template.
- `BoxFilter(FilterMode=Region)` is the ROI gate and removes detections whose center falls outside the configured region.
- `BoxNms` is the single owner of runtime score / IoU filtering.
- `ResultOutput` should receive `BoxNms.Diagnostics`, `DetectionSequenceJudge.Diagnostics`, and `DetectionSequenceJudge.Message` together with the preview image.

Current tuning contract:

- `wire-sequence-terminal` v1 only allows automatic tuning for:
  - `BoxNms.ScoreThreshold`
  - `BoxNms.IouThreshold`
- `DeepLearning.Confidence` remains a low confidence floor and is not the primary现场调参项.
- `DetectionSequenceJudge.MinConfidence` stays fixed at `0.0`.
- `ExpectedLabels`, `ExpectedCount`, `DeepLearning.ModelPath`, `DeepLearning.LabelsPath` require manual review and must not be auto-rewritten.

## Asset Delivery

- The package manifest currently points to `models/wire-seq-yolo-v1.2.onnx`.
- The repository does not commit the private model binary by default.
- Local or on-site delivery must provide one of the following:
  - the model file at `models/wire-seq-yolo-v1.2.onnx`, or
  - an explicit `DeepLearning.ModelPath` pointing to a real external file.
- `labels/labels.txt` stores the model export class order and must match the ONNX metadata `names` order when metadata is present.
- `expectedSequence` and `DetectionSequenceJudge.ExpectedLabels` remain the business inspection order and are intentionally independent from model class order.

## Sample Delivery

- `samples/` now only contains the minimum directory contract and metadata example.
- Real OK / NG sample batches should be kept outside the repo when confidentiality requires it, but the sample manifest and sidecar metadata format must remain aligned with this package.

## Version Policy

1. Package version uses semantic versioning (`major.minor.patch`).
2. Any model/template/rule change must register a new artifact version.
3. `manifest.json` points to currently active artifact versions.
4. Every released package snapshot is recorded in `versions/<packageVersion>/release.json`.
