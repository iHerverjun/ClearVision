import test from 'node:test';
import assert from 'node:assert/strict';
import {
  clampRectToBounds,
  normalizeRectFromPoints,
  resizeRectByHandle,
  screenToImagePoint,
  translateRect
} from '../../../../src/Acme.Product.Desktop/wwwroot/src/features/flow-editor/roiGeometry.mjs';

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
