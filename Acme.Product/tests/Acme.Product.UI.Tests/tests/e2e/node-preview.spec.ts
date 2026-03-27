import { test, expect } from '@playwright/test';
import { bootAuthenticatedApp } from './authHelper';

const PNG_BASE64 =
  'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVQIHWP4////fwAJ+wP9KobjigAAAABJRU5ErkJggg==';

async function stubOperatorLibrary(page) {
  await page.route('**/api/operators/types', async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: '[]',
    });
  });

  await page.route('**/api/operators/library', async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: '[]',
    });
  });
}

async function setCurrentProject(page) {
  await page.evaluate(async () => {
    const projectModule = await import('/src/features/project/projectManager.js');
    const inspectionModule = await import('/src/features/inspection/inspectionController.js');
    projectModule.setCurrentProject({
      id: 'e2e-project',
      name: 'E2E Project',
      description: '',
      flow: null,
    });
    inspectionModule.default.setProject('e2e-project');
  });
}

async function addAndSelectNode(page, config: {
  type: string;
  title: string;
  x?: number;
  y?: number;
  parameters?: unknown[];
  inputs?: Array<{ name: string; type: string }>;
  outputs?: Array<{ name: string; type: string }>;
}) {
  return page.evaluate(nodeConfig => {
    const flowCanvas = (window as any).flowCanvas;
    const node = flowCanvas.addNode(
      nodeConfig.type,
      nodeConfig.x ?? 120,
      nodeConfig.y ?? 120,
      {
        title: nodeConfig.title,
        parameters: nodeConfig.parameters ?? [],
        inputs: nodeConfig.inputs ?? [{ name: 'input', type: 'Image' }],
        outputs: nodeConfig.outputs ?? [{ name: 'output', type: 'Image' }],
        color: '#1890ff',
      }
    );

    (window as any).__e2ePreviewNodeId = node.id;
    flowCanvas.selectedNode = node.id;
    flowCanvas.onNodeSelected?.(node);
    return node.id;
  }, config);
}

test.describe('Node Preview Overlay', () => {
  test.beforeEach(async ({ page }) => {
    await stubOperatorLibrary(page);
    await bootAuthenticatedApp(page);
    await setCurrentProject(page);
  });

  test('shows overlay for image-output nodes', async ({ page }) => {
    await page.route('**/api/flows/preview-node', async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          success: true,
          outputImageBase64: PNG_BASE64,
          outputData: {
            OriginalImage: 'hidden-clean-image',
            Score: 0.98,
            Label: 'OK',
            Count: 1,
          },
          executionTimeMs: 12,
        }),
      });
    });

    await addAndSelectNode(page, {
      type: 'PreviewImageNode',
      title: '图像预览节点',
      parameters: [
        { name: 'Threshold', displayName: 'Threshold', dataType: 'int', value: 10, defaultValue: 10 },
      ],
      outputs: [{ name: 'Image', type: 'Image' }],
    });

    await expect(page.locator('.node-preview-card')).toBeVisible();
    await expect(page.locator('.node-preview-card img')).toBeVisible();
    await expect(page.locator('#preview-status-text')).toContainText('预览完成');
    await expect(page.locator('#preview-output-list')).toContainText('Score');
    await expect(page.locator('#preview-output-list')).not.toContainText('OriginalImage');
    await expect(page.locator('#preview-output-list')).not.toContainText('hidden-clean-image');
  });

  test('keeps overlay hidden for non-image nodes while right panel still shows summary', async ({ page }) => {
    await page.route('**/api/flows/preview-node', async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          success: true,
          outputData: {
            Result: 'OK',
            Count: 3,
          },
          executionTimeMs: 9,
        }),
      });
    });

    await addAndSelectNode(page, {
      type: 'StringSummaryNode',
      title: '摘要节点',
      outputs: [{ name: 'Text', type: 'String' }],
    });

    await page.waitForTimeout(700);
    await expect(page.locator('.node-preview-card')).toHaveCount(0);
    await expect(page.locator('#preview-output-list')).toContainText('Result');
  });

  test('parameter change triggers one debounced preview and panning does not trigger extra preview', async ({ page }) => {
    let previewCallCount = 0;
    await page.route('**/api/flows/preview-node', async route => {
      previewCallCount += 1;
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          success: true,
          outputImageBase64: PNG_BASE64,
          outputData: {
            Score: previewCallCount,
          },
          executionTimeMs: 15,
        }),
      });
    });

    await addAndSelectNode(page, {
      type: 'PreviewImageNode',
      title: '图像预览节点',
      parameters: [
        { name: 'Threshold', displayName: 'Threshold', dataType: 'int', value: 10, defaultValue: 10 },
      ],
      outputs: [{ name: 'Image', type: 'Image' }],
    });

    await page.waitForTimeout(700);
    expect(previewCallCount).toBe(1);

    const overlayBefore = await page.locator('.node-preview-card').boundingBox();

    await page.locator('#param-Threshold').fill('25');
    await page.locator('#param-Threshold').blur();

    await page.waitForTimeout(700);
    expect(previewCallCount).toBe(2);

    const canvas = page.locator('#flow-canvas');
    const box = await canvas.boundingBox();
    if (!box) {
      throw new Error('Canvas bounding box not found');
    }

    await page.mouse.move(box.x + box.width - 30, box.y + box.height - 30);
    await page.mouse.down();
    await page.mouse.move(box.x + box.width - 120, box.y + box.height - 80, { steps: 8 });
    await page.mouse.up();

    await page.waitForTimeout(150);
    expect(previewCallCount).toBe(2);

    const overlayAfter = await page.locator('.node-preview-card').boundingBox();
    const moved =
      overlayBefore &&
      overlayAfter &&
      (Math.abs(overlayBefore.x - overlayAfter.x) > 1 || Math.abs(overlayBefore.y - overlayAfter.y) > 1);
    expect(moved).toBeTruthy();
  });

  test('double click on ForEach still enters subgraph', async ({ page }) => {
    const nodeId = await addAndSelectNode(page, {
      type: 'ForEach',
      title: 'ForEach',
      outputs: [{ name: 'Result', type: 'Any' }],
    });

    const coords = await page.evaluate(selectedNodeId => {
      const flowCanvas = (window as any).flowCanvas;
      const rect = flowCanvas.getNodeScreenRect(selectedNodeId);
      const canvasRect = document.getElementById('flow-canvas').getBoundingClientRect();
      return {
        x: canvasRect.left + rect.x + rect.width / 2,
        y: canvasRect.top + rect.y + rect.height / 2,
      };
    }, nodeId);

    await page.mouse.dblclick(coords.x, coords.y);
    await expect(page.locator('#subgraph-breadcrumb')).toBeVisible();
  });
});
