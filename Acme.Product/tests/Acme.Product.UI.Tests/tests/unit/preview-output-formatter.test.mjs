import test from 'node:test';
import assert from 'node:assert/strict';
import {
  buildPreviewSummaryItems,
  formatPreviewOutputValue
} from '../../../../src/Acme.Product.Desktop/wwwroot/src/features/flow-editor/previewOutputFormatter.mjs';

test('formatPreviewOutputValue summarizes detections payloads by count', () => {
  assert.deepEqual(
    formatPreviewOutputValue('Detections', {
      detections: [{ label: 'Wire_Black' }, { label: 'Wire_Red' }]
    }),
    {
      text: '2 detections',
      title: null,
      kind: 'detections'
    }
  );
});

test('formatPreviewOutputValue summarizes suppressed detections by count', () => {
  assert.deepEqual(
    formatPreviewOutputValue('SuppressedDetections', [{ id: 1 }]),
    {
      text: '1 suppressed',
      title: null,
      kind: 'suppressed'
    }
  );
});

test('formatPreviewOutputValue summarizes generic arrays and objects', () => {
  assert.deepEqual(
    formatPreviewOutputValue('Labels', ['A', 'B', 'C']),
    {
      text: '3 items',
      title: null,
      kind: 'array'
    }
  );

  assert.deepEqual(
    formatPreviewOutputValue('Meta', { station: 'S1', mode: 'Auto' }),
    {
      text: '2 fields',
      title: null,
      kind: 'object'
    }
  );
});

test('formatPreviewOutputValue preserves numeric and boolean formatting', () => {
  assert.deepEqual(
    formatPreviewOutputValue('Score', 0.771545),
    {
      text: '0.772',
      title: null,
      kind: 'number'
    }
  );

  assert.deepEqual(
    formatPreviewOutputValue('Enabled', true),
    {
      text: 'true',
      title: null,
      kind: 'boolean'
    }
  );
});

test('formatPreviewOutputValue truncates long strings without losing the full title text', () => {
  const formatted = formatPreviewOutputValue(
    'Summary',
    'abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ',
    { stringMaxLength: 12 }
  );

  assert.equal(formatted.text, 'abcdefghijkl...');
  assert.equal(formatted.title, 'abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ');
  assert.equal(formatted.kind, 'string');
});

test('buildPreviewSummaryItems skips image-like payloads and keeps structured summaries compact', () => {
  const items = buildPreviewSummaryItems({
    PreviewImage: 'data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVQIHWP4////fwAJ+wP9KobjigAAAABJRU5ErkJggg==',
    Detections: { detections: [{}, {}] },
    Meta: { station: 'S1', mode: 'Auto' }
  }, {
    maxItems: 3,
    stringMaxLength: 16,
    skipImageLikeValues: true
  });

  assert.deepEqual(items, [
    {
      key: 'Detections',
      value: '2 detections',
      title: null,
      kind: 'detections'
    },
    {
      key: 'Meta',
      value: '2 fields',
      title: null,
      kind: 'object'
    }
  ]);
});
