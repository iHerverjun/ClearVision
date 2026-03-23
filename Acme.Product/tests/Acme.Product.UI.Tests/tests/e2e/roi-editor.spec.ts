import { test, expect, Page } from '@playwright/test';
import { bootAuthenticatedApp } from './authHelper';

const PREVIEW_PNG_BASE64 =
  'iVBORw0KGgoAAAANSUhEUgAAAEAAAABACAYAAACqaXHeAAAAAXNSR0IArs4c6QAAAARnQU1BAACxjwv8YQUAAAAJcEhZcwAADsMAAA7DAcdvqGQAAACNSURBVHhe7dAxAQAwDITAyn73qQEcwHALI2/bmTWAokkDKJo0gKJJAyiaNICiSQMomjSAokkDKJo0gKJJAyiaNICiSQMomjSAokkDKJo0gKJJAyiaNICiSQMomjSAokkDKJo0gKJJAyiaNICiSQMomjSAokkDKJo0gKJJAyiaNICiSQMomjSAool8wO4D9cdyOzoyljkAAAAASUVORK5CYII=';

async function stubOperatorLibrary(page: Page) {
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

async function setCurrentProject(page: Page) {
  await page.evaluate(async () => {
    const projectModule = await import('/src/features/project/projectManager.js');
    const inspectionModule = await import('/src/features/inspection/inspectionController.js');
    projectModule.setCurrentProject({
      id: 'e2e-roi-project',
      name: 'E2E ROI Project',
      description: '',
      flow: null,
    });
    inspectionModule.default.setProject('e2e-roi-project');
  });
}

function createRoiParameters(overrides: Record<string, unknown> = {}) {
  const values = {
    Shape: 'Rectangle',
    Operation: 'Crop',
    X: 8,
    Y: 10,
    Width: 20,
    Height: 18,
    CenterX: 12,
    CenterY: 12,
    Radius: 10,
    PolygonPoints: '[[10,10],[20,10],[20,20],[10,20]]',
    ...overrides,
  };

  return [
    {
      name: 'Shape',
      displayName: '形状',
      dataType: 'enum',
      value: values.Shape,
      defaultValue: 'Rectangle',
      options: ['Rectangle', 'Circle', 'Polygon'],
    },
    {
      name: 'Operation',
      displayName: '操作',
      dataType: 'enum',
      value: values.Operation,
      defaultValue: 'Crop',
      options: ['Crop', 'Mask'],
    },
    { name: 'X', displayName: 'X', dataType: 'int', value: values.X, defaultValue: values.X, min: 0, max: 64 },
    { name: 'Y', displayName: 'Y', dataType: 'int', value: values.Y, defaultValue: values.Y, min: 0, max: 64 },
    { name: 'Width', displayName: '宽度', dataType: 'int', value: values.Width, defaultValue: values.Width, min: 1, max: 64 },
    { name: 'Height', displayName: '高度', dataType: 'int', value: values.Height, defaultValue: values.Height, min: 1, max: 64 },
    { name: 'CenterX', displayName: '圆心X', dataType: 'int', value: values.CenterX, defaultValue: values.CenterX },
    { name: 'CenterY', displayName: '圆心Y', dataType: 'int', value: values.CenterY, defaultValue: values.CenterY },
    { name: 'Radius', displayName: '半径', dataType: 'int', value: values.Radius, defaultValue: values.Radius, min: 1, max: 64 },
    { name: 'PolygonPoints', displayName: '多边形顶点(JSON)', dataType: 'string', value: values.PolygonPoints, defaultValue: values.PolygonPoints },
  ];
}

async function addAndSelectRoiNode(page: Page, overrides: Record<string, unknown> = {}) {
  return page.evaluate((parameters) => {
    const flowCanvas = (window as any).flowCanvas;
    const node = flowCanvas.addNode(
      'RoiManager',
      180,
      140,
      {
        title: '固定ROI',
        parameters,
        inputs: [{ name: 'Image', type: 'Image' }],
        outputs: [{ name: 'Image', type: 'Image' }, { name: 'Mask', type: 'Image' }],
        color: '#1890ff',
      }
    );

    flowCanvas.selectedNode = node.id;
    flowCanvas.onNodeSelected?.(node);
    (window as any).__e2eRoiNodeId = node.id;
    return node.id;
  }, createRoiParameters(overrides));
}

async function waitForRoiEditorReady(page: Page) {
  await expect(page.locator('.roi-editor-panel')).toBeVisible();
  await page.waitForFunction(() => {
    const panel = (window as any).propertyPanel?.roiEditorPanel;
    return Boolean(panel?.currentImageSource && panel?.imageCanvas?.image);
  });
}

async function getRoiState(page: Page) {
  return page.evaluate(() => {
    const panel = (window as any).propertyPanel;
    const overlay = panel?.roiEditorPanel?.imageCanvas?.getPrimaryEditableOverlay?.();
    const readValue = (name: string) => {
      const input = document.querySelector<HTMLInputElement>(`#param-${name}`);
      return input ? Number.parseInt(input.value, 10) : null;
    };

    return {
      params: {
        x: readValue('X'),
        y: readValue('Y'),
        width: readValue('Width'),
        height: readValue('Height'),
      },
      overlay: overlay
        ? {
            x: Math.round(overlay.x),
            y: Math.round(overlay.y),
            width: Math.round(overlay.width),
            height: Math.round(overlay.height),
          }
        : null,
    };
  });
}

async function dispatchRoiDrag(
  page: Page,
  from: { x: number; y: number },
  to: { x: number; y: number },
  button: 'left' | 'right' = 'left'
) {
  await page.evaluate(({ startPoint, endPoint, mouseButton }) => {
    const panel = (window as any).propertyPanel?.roiEditorPanel;
    const canvas = document.querySelector<HTMLCanvasElement>('.roi-editor-canvas');
    if (!panel?.imageCanvas || !canvas) {
      throw new Error('ROI editor canvas not ready');
    }

    const toClient = (point: { x: number; y: number }) => ({
      clientX: canvas.getBoundingClientRect().left + panel.imageCanvas.offset.x + point.x * panel.imageCanvas.scale,
      clientY: canvas.getBoundingClientRect().top + panel.imageCanvas.offset.y + point.y * panel.imageCanvas.scale
    });

    const buttonValue = mouseButton === 'right' ? 2 : 0;
    const buttonsValue = mouseButton === 'right' ? 2 : 1;
    const start = toClient(startPoint);
    const end = toClient(endPoint);

    canvas.dispatchEvent(new MouseEvent('mousedown', {
      bubbles: true,
      button: buttonValue,
      buttons: buttonsValue,
      ...start
    }));

    for (let step = 1; step <= 6; step += 1) {
      const progress = step / 6;
      const intermediate = {
        clientX: start.clientX + (end.clientX - start.clientX) * progress,
        clientY: start.clientY + (end.clientY - start.clientY) * progress
      };
      canvas.dispatchEvent(new MouseEvent('mousemove', {
        bubbles: true,
        button: buttonValue,
        buttons: buttonsValue,
        ...intermediate
      }));
    }

    canvas.dispatchEvent(new MouseEvent('mouseup', {
      bubbles: true,
      button: buttonValue,
      buttons: 0,
      ...end
    }));
  }, {
    startPoint: from,
    endPoint: to,
    mouseButton: button
  });
}

async function getCanvasPointForImagePoint(page: Page, point: { x: number; y: number }) {
  return page.evaluate((targetPoint) => {
    const panel = (window as any).propertyPanel?.roiEditorPanel;
    const canvas = document.querySelector<HTMLCanvasElement>('.roi-editor-canvas');
    if (!panel?.imageCanvas || !canvas) {
      throw new Error('ROI editor canvas not ready');
    }

    const rect = canvas.getBoundingClientRect();
    return {
      x: rect.left + panel.imageCanvas.offset.x + targetPoint.x * panel.imageCanvas.scale,
      y: rect.top + panel.imageCanvas.offset.y + targetPoint.y * panel.imageCanvas.scale,
    };
  }, point);
}

async function getHandlePoint(page: Page, handle: string) {
  return page.evaluate((handleName) => {
    const panel = (window as any).propertyPanel?.roiEditorPanel;
    const canvas = document.querySelector<HTMLCanvasElement>('.roi-editor-canvas');
    const overlay = panel?.imageCanvas?.getPrimaryEditableOverlay?.();
    if (!panel?.imageCanvas || !canvas || !overlay) {
      throw new Error('ROI overlay not ready');
    }

    const handles = {
      nw: { x: overlay.x, y: overlay.y },
      n: { x: overlay.x + overlay.width / 2, y: overlay.y },
      ne: { x: overlay.x + overlay.width, y: overlay.y },
      e: { x: overlay.x + overlay.width, y: overlay.y + overlay.height / 2 },
      se: { x: overlay.x + overlay.width, y: overlay.y + overlay.height },
      s: { x: overlay.x + overlay.width / 2, y: overlay.y + overlay.height },
      sw: { x: overlay.x, y: overlay.y + overlay.height },
      w: { x: overlay.x, y: overlay.y + overlay.height / 2 },
    } as Record<string, { x: number; y: number }>;

    const point = handles[handleName];
    const rect = canvas.getBoundingClientRect();
    return {
      x: rect.left + panel.imageCanvas.offset.x + point.x * panel.imageCanvas.scale,
      y: rect.top + panel.imageCanvas.offset.y + point.y * panel.imageCanvas.scale,
    };
  }, handle);
}

test.describe('ROI Editor', () => {
  test.beforeEach(async ({ page }) => {
    await stubOperatorLibrary(page);
    await bootAuthenticatedApp(page);
    await setCurrentProject(page);
  });

  test('shows ROI editor for rectangle and disables it for non-rectangle shapes', async ({ page }) => {
    await page.route('**/api/flows/preview-node', async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          success: true,
          inputImageBase64: PREVIEW_PNG_BASE64,
          outputImageBase64: PREVIEW_PNG_BASE64,
          outputData: { Width: 64, Height: 64, Shape: 'Rectangle' },
          executionTimeMs: 8,
        }),
      });
    });

    await addAndSelectRoiNode(page);
    await waitForRoiEditorReady(page);

    await expect(page.locator('.roi-editor-panel')).toBeVisible();
    await expect(page.locator('#roi-editor-readonly')).toHaveClass(/hidden/);

    await page.selectOption('#param-Shape', 'Circle');
    await expect(page.locator('#roi-editor-readonly')).toBeVisible();
  });

  test('drawing a new rectangle updates XYWH and triggers one extra preview', async ({ page }) => {
    let previewCallCount = 0;
    await page.route('**/api/flows/preview-node', async route => {
      previewCallCount += 1;
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          success: true,
          inputImageBase64: PREVIEW_PNG_BASE64,
          outputImageBase64: PREVIEW_PNG_BASE64,
          outputData: { Width: 64, Height: 64, Count: previewCallCount },
          executionTimeMs: 10,
        }),
      });
    });

    await addAndSelectRoiNode(page, { X: 4, Y: 4, Width: 8, Height: 8 });
    await waitForRoiEditorReady(page);
    await page.waitForTimeout(700);
    expect(previewCallCount).toBe(1);

    await dispatchRoiDrag(page, { x: 20, y: 18 }, { x: 42, y: 40 });

    await page.waitForTimeout(500);
    expect(previewCallCount).toBe(2);

    const roiState = await getRoiState(page);
    expect(roiState.params.x).toBe(20);
    expect(roiState.params.y).toBe(18);
    expect(roiState.params.width).toBe(22);
    expect(roiState.params.height).toBe(22);
    expect(roiState.overlay).toEqual({ x: 20, y: 18, width: 22, height: 22 });
  });

  test('dragging and resizing the ROI updates parameters', async ({ page }) => {
    await page.route('**/api/flows/preview-node', async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          success: true,
          inputImageBase64: PREVIEW_PNG_BASE64,
          outputImageBase64: PREVIEW_PNG_BASE64,
          outputData: { Width: 64, Height: 64 },
          executionTimeMs: 10,
        }),
      });
    });

    await addAndSelectRoiNode(page, { X: 10, Y: 12, Width: 12, Height: 14 });
    await waitForRoiEditorReady(page);

    await dispatchRoiDrag(page, { x: 16, y: 18 }, { x: 24, y: 26 });

    let roiState = await getRoiState(page);
    expect(roiState.params.x).toBe(18);
    expect(roiState.params.y).toBe(20);

    const resizeFrom = await getHandlePoint(page, 'se');
    await page.evaluate((start) => {
      const panel = (window as any).propertyPanel?.roiEditorPanel;
      const canvas = document.querySelector<HTMLCanvasElement>('.roi-editor-canvas');
      if (!panel?.imageCanvas || !canvas) {
        throw new Error('ROI editor canvas not ready');
      }

      const rect = canvas.getBoundingClientRect();
      const startPoint = {
        x: (start.x - rect.left - panel.imageCanvas.offset.x) / panel.imageCanvas.scale,
        y: (start.y - rect.top - panel.imageCanvas.offset.y) / panel.imageCanvas.scale
      };
      const endPoint = { x: 42, y: 46 };
      const toClient = (point: { x: number; y: number }) => ({
        clientX: rect.left + panel.imageCanvas.offset.x + point.x * panel.imageCanvas.scale,
        clientY: rect.top + panel.imageCanvas.offset.y + point.y * panel.imageCanvas.scale
      });

      const startClient = toClient(startPoint);
      const endClient = toClient(endPoint);
      canvas.dispatchEvent(new MouseEvent('mousedown', { bubbles: true, button: 0, buttons: 1, ...startClient }));
      for (let step = 1; step <= 6; step += 1) {
        const progress = step / 6;
        const intermediate = {
          clientX: startClient.clientX + (endClient.clientX - startClient.clientX) * progress,
          clientY: startClient.clientY + (endClient.clientY - startClient.clientY) * progress
        };
        canvas.dispatchEvent(new MouseEvent('mousemove', { bubbles: true, button: 0, buttons: 1, ...intermediate }));
      }
      canvas.dispatchEvent(new MouseEvent('mouseup', { bubbles: true, button: 0, buttons: 0, ...endClient }));
    }, resizeFrom);

    roiState = await getRoiState(page);
    expect(roiState.params.width).toBe(24);
    expect(roiState.params.height).toBe(26);
    expect(roiState.overlay).toEqual({ x: 18, y: 20, width: 24, height: 26 });
  });

  test('manual parameter edits sync overlay and right-button pan does not mutate ROI', async ({ page }) => {
    await page.route('**/api/flows/preview-node', async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          success: true,
          inputImageBase64: PREVIEW_PNG_BASE64,
          outputImageBase64: PREVIEW_PNG_BASE64,
          outputData: { Width: 64, Height: 64 },
          executionTimeMs: 10,
        }),
      });
    });

    await addAndSelectRoiNode(page, { X: 6, Y: 7, Width: 10, Height: 12 });
    await waitForRoiEditorReady(page);

    await page.fill('#param-X', '14');
    await page.fill('#param-Y', '16');
    await page.fill('#param-Width', '18');
    await page.fill('#param-Height', '20');
    await page.locator('#param-Height').blur();
    await page.waitForTimeout(300);

    let roiState = await getRoiState(page);
    expect(roiState.overlay).toEqual({ x: 14, y: 16, width: 18, height: 20 });

    const beforePan = roiState.overlay;
    await dispatchRoiDrag(page, { x: 5, y: 5 }, { x: 12, y: 12 }, 'right');

    roiState = await getRoiState(page);
    expect(roiState.overlay).toEqual(beforePan);
    expect(roiState.params).toEqual({
      x: 14,
      y: 16,
      width: 18,
      height: 20,
    });
  });
});
