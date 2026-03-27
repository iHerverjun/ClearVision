# Models

This scenario package now targets a two-wire terminal sequence contract:

- `Wire_Black`
- `Wire_Blue`

The recommended external model artifact name is:

- `wire-seq-yolo-v1.2.onnx`

The repository does not commit the actual ONNX binary. This folder keeps only the
contract for package metadata and local deployment paths.

## Local placement

Place the trained model at:

- `线序检测/scenario-package-wire-sequence/models/wire-seq-yolo-v1.2.onnx`

Or point `DeepLearning.ModelPath` to any valid external ONNX file.

## Label alignment

The model output class order must match [labels.txt](../labels/labels.txt):

1. `Wire_Blue`
2. `Wire_Black`

The inspection business order is still:

1. `Wire_Black`
2. `Wire_Blue`

Do not use the business order as the model class-order labels.

If the training/export label order changes, update these files together:

- `manifest.json`
- `rules/sequence-rule.v1.json`
- `template/terminal-wire-sequence.flow.template.json`
- `labels/labels.txt`
- `versions/<packageVersion>/release.json`
