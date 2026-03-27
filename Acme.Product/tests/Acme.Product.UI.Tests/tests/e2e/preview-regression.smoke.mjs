import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

import {
  NodePreviewCoordinator,
  getCanvasPreviewEligibility,
  resolvePreviewInputImageBase64,
} from '../../../../src/Acme.Product.Desktop/wwwroot/src/features/flow-editor/previewCoordinator.js';
import ResultPanel from '../../../../src/Acme.Product.Desktop/wwwroot/src/features/results/resultPanel.js';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const repoRoot = path.resolve(__dirname, '../../../..');

const PNG_BASE64 =
  'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVQIHWP4////fwAJ+wP9KobjigAAAABJRU5ErkJggg==';

const sleep = ms => new Promise(resolve => setTimeout(resolve, ms));

async function runPreviewCoordinatorChecks() {
  assert.equal(resolvePreviewInputImageBase64({ outputImageBase64: PNG_BASE64 }), PNG_BASE64);
  assert.equal(resolvePreviewInputImageBase64({ OutputImage: `data:image/png;base64,${PNG_BASE64}` }), PNG_BASE64);

  assert.equal(getCanvasPreviewEligibility({ type: 'ImageAcquisition', outputs: [] }).eligible, true);
  assert.equal(
    getCanvasPreviewEligibility({ type: 'LegacyImageNode', outputs: [{ name: 'Image', type: '0' }] }).eligible,
    true
  );
  assert.equal(
    getCanvasPreviewEligibility({ type: 'StringNode', outputs: [{ name: 'Text', type: 'String' }] }).eligible,
    false
  );

  const acquisitionCoordinator = new NodePreviewCoordinator({
    getProjectId: () => 'project-1',
    getFlowRevision: () => 1,
    getNodeById: () => ({
      id: 'acq-1',
      type: 'ImageAcquisition',
      title: '图像采集',
      parameters: [
        { name: 'SourceType', value: 'File' },
        { name: 'FilePath', value: '' },
      ],
      outputs: [{ name: 'Image', type: 'Image' }],
    }),
    getOperatorMetadata: () => null,
    getInputImageBase64: () => null,
    previewExecutor: async () => {
      throw new Error('should not execute preview');
    },
    debounceMs: 10,
  });

  acquisitionCoordinator.setActiveNode({
    id: 'acq-1',
    type: 'ImageAcquisition',
    title: '图像采集',
    parameters: [
      { name: 'SourceType', value: 'File' },
      { name: 'FilePath', value: '' },
    ],
    outputs: [{ name: 'Image', type: 'Image' }],
  });
  await sleep(30);
  assert.equal(acquisitionCoordinator.getState().status, 'idle');
  assert.equal(acquisitionCoordinator.getState().presenter.statusText, '请先配置文件路径');
  acquisitionCoordinator.destroy();

  let previewCalls = 0;
  let node = {
    id: 'node-1',
    type: 'PreviewImageNode',
    title: '图像预览节点',
    parameters: [{ name: 'Threshold', value: 10 }],
    outputs: [{ name: 'Image', type: 'Image' }],
  };
  let flowRevision = 1;

  const coordinator = new NodePreviewCoordinator({
    getProjectId: () => 'project-1',
    getFlowRevision: () => flowRevision,
    getNodeById: () => node,
    getOperatorMetadata: () => null,
    getInputImageBase64: () => PNG_BASE64,
    previewExecutor: async () => {
      previewCalls += 1;
      await sleep(10);
      return {
        success: true,
        outputImageBase64: PNG_BASE64,
        outputData: { Score: previewCalls },
        executionTimeMs: 7,
      };
    },
    debounceMs: 30,
  });

  coordinator.setActiveNode(node);
  await sleep(80);
  assert.equal(previewCalls, 1);
  assert.equal(coordinator.getState().status, 'success');
  assert.equal(coordinator.getState().presenter.overlayEnabled, true);

  coordinator.requestActivePreview();
  await sleep(50);
  assert.equal(previewCalls, 1, 'same request key should reuse cache');

  node = {
    ...node,
    parameters: [{ name: 'Threshold', value: 20 }],
  };
  coordinator.invalidateActivePreview();
  await sleep(80);
  assert.equal(previewCalls, 2, 'parameter change should invalidate preview');

  flowRevision = 2;
  coordinator.handleStructureChanged();
  await sleep(80);
  assert.equal(previewCalls, 3, 'flow revision change should invalidate preview');

  coordinator.destroy();
}

function runSourceWiringChecks() {
  const appPath = path.join(
    repoRoot,
    'src',
    'Acme.Product.Desktop',
    'wwwroot',
    'src',
    'app.js'
  );
  const flowEditorPath = path.join(
    repoRoot,
    'src',
    'Acme.Product.Desktop',
    'wwwroot',
    'src',
    'features',
    'flow-editor',
    'flowEditorInteraction.js'
  );

  const appSource = fs.readFileSync(appPath, 'utf8');
  const flowEditorSource = fs.readFileSync(flowEditorPath, 'utf8');

  assert.match(appSource, /NodePreviewCoordinator/);
  assert.match(appSource, /NodePreviewOverlay/);
  assert.match(appSource, /previewCoordinator:\s*nodePreviewCoordinator/);

  const notifyMatches = flowEditorSource.match(/notifyViewStateChanged\?\.\(\)/g) || [];
  assert.ok(
    notifyMatches.length >= 4,
    'flow editor interaction should notify view-state changes during pan/drag lifecycle'
  );
}

function runResultPanelChecks() {
  const fakeResultPanel = {
    isExportMetadataKey: ResultPanel.prototype.isExportMetadataKey,
    isTechnicalCollectionKey: ResultPanel.prototype.isTechnicalCollectionKey,
    isStructuredExportText: () => false,
  };

  assert.equal(
    ResultPanel.prototype.shouldHideOutputDetailEntry.call(fakeResultPanel, 'OriginalImage', 'hidden', {}),
    true
  );
  assert.equal(
    ResultPanel.prototype.shouldHideOutputDetailEntry.call(fakeResultPanel, 'Image', 'hidden', {}),
    true
  );
  assert.equal(
    ResultPanel.prototype.shouldHideOutputDetailEntry.call(fakeResultPanel, 'Count', 1, {}),
    false
  );
}

await runPreviewCoordinatorChecks();
runSourceWiringChecks();
runResultPanelChecks();
console.log('preview regression smoke passed');
