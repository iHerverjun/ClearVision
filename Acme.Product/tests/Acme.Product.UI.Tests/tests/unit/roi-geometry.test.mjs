import test from 'node:test';
import assert from 'node:assert/strict';
import {
  DEFAULT_RECT_PARAM_KEYS,
  REGION_RECT_PARAM_KEYS,
  clampRectToBounds,
  normalizeRectFromPoints,
  rectFromParams,
  rectToParams,
  resizeRectByHandle,
  screenToImagePoint,
  translateRect
} from '../../../../src/Acme.Product.Desktop/wwwroot/src/features/flow-editor/roiGeometry.mjs';
import { getOperatorRoiConfig } from '../../../../src/Acme.Product.Desktop/wwwroot/src/features/flow-editor/roiEditorSupport.mjs';
import RoiEditorPanel from '../../../../src/Acme.Product.Desktop/wwwroot/src/features/flow-editor/roiEditorPanel.js';

test('normalizeRectFromPoints handles reverse drag direction', () => {
  assert.deepEqual(
    normalizeRectFromPoints({ x: 30, y: 40 }, { x: 10, y: 20 }),
    { x: 10, y: 20, width: 20, height: 20 }
  );
});

test('clampRectToBounds keeps rect inside image and enforces min size', () => {
  assert.deepEqual(
    clampRectToBounds({ x: -5, y: 90, width: 30, height: 20 }, { width: 100, height: 100 }, 1),
    { x: 0, y: 80, width: 30, height: 20 }
  );
});

test('screenToImagePoint converts viewport coordinates back to image space', () => {
  assert.deepEqual(
    screenToImagePoint({ x: 210, y: 120 }, { scale: 2, offset: { x: 10, y: 20 } }),
    { x: 100, y: 50 }
  );
});

test('resizeRectByHandle updates size from south-east handle', () => {
  assert.deepEqual(
    resizeRectByHandle(
      { x: 10, y: 10, width: 20, height: 20 },
      'se',
      { x: 40, y: 45 },
      { width: 100, height: 100 },
      1
    ),
    { x: 10, y: 10, width: 30, height: 35 }
  );
});

test('translateRect keeps moved rectangle within bounds', () => {
  assert.deepEqual(
    translateRect(
      { x: 80, y: 85, width: 20, height: 15 },
      { x: 10, y: 10 },
      { width: 100, height: 100 },
      1
    ),
    { x: 80, y: 85, width: 20, height: 15 }
  );
});

test('rectFromParams supports BoxFilter region parameter names', () => {
  assert.deepEqual(
    rectFromParams({
      RegionX: 12,
      RegionY: 14,
      RegionW: 30,
      RegionH: 18
    }, REGION_RECT_PARAM_KEYS),
    { x: 12, y: 14, width: 30, height: 18 }
  );
});

test('rectToParams can write back BoxFilter region parameter names', () => {
  assert.deepEqual(
    rectToParams({ x: 6, y: 8, width: 20, height: 16 }, REGION_RECT_PARAM_KEYS),
    { RegionX: 6, RegionY: 8, RegionW: 20, RegionH: 16 }
  );
});

test('getOperatorRoiConfig enables ROI editor for BoxFilter region mode', () => {
  const config = getOperatorRoiConfig({
    type: 'BoxFilter',
    parameters: [
      { name: 'FilterMode', value: 'Region' },
      { name: 'RegionX', value: 0 },
      { name: 'RegionY', value: 0 },
      { name: 'RegionW', value: 100 },
      { name: 'RegionH', value: 80 }
    ]
  });

  assert.equal(config.supported, true);
  assert.equal(config.editable, true);
  assert.deepEqual(config.rectParamKeys, REGION_RECT_PARAM_KEYS);
});

test('getOperatorRoiConfig keeps BoxFilter ROI editor readonly outside region mode', () => {
  const config = getOperatorRoiConfig({
    type: 'BoxFilter',
    parameters: [
      { name: 'FilterMode', value: 'Score' }
    ]
  });

  assert.equal(config.supported, true);
  assert.equal(config.editable, false);
  assert.match(config.readonlyMessage, /Region/);
  assert.deepEqual(DEFAULT_RECT_PARAM_KEYS, {
    x: 'X',
    y: 'Y',
    width: 'Width',
    height: 'Height'
  });
});

test('refreshFromOperator re-applies ROI state instead of only syncing the old overlay', async () => {
  const panel = {
    currentConfig: { editable: false },
    applyState() {
      this.currentConfig = { editable: true };
      return Promise.resolve();
    }
  };

  await RoiEditorPanel.prototype.refreshFromOperator.call(panel);

  assert.equal(panel.currentConfig.editable, true);
});
