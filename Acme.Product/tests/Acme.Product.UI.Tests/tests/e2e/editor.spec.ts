import { test, expect } from '@playwright/test';
import { bootAuthenticatedApp } from './authHelper';

const PROPERTY_SIDEBAR_SELECTOR = '[data-sidebar="property"]';
const PROPERTY_RESIZER_SELECTOR = '[data-sidebar-resizer="property"]';
const PROPERTY_SIDEBAR_STORAGE_KEY = 'cv_flow_property_sidebar_width';

async function getSidebarWidth(page) {
  return page.locator(PROPERTY_SIDEBAR_SELECTOR).evaluate(node =>
    Math.round(node.getBoundingClientRect().width)
  );
}

async function dragPropertySidebar(page, deltaX) {
  const resizer = page.locator(PROPERTY_RESIZER_SELECTOR);
  await expect(resizer).toBeVisible();

  const box = await resizer.boundingBox();
  if (!box) {
    throw new Error('Property sidebar resizer is not visible.');
  }

  const startX = box.x + box.width / 2;
  const centerY = box.y + box.height / 2;

  await page.mouse.move(startX, centerY);
  await page.mouse.down();
  await page.mouse.move(startX + deltaX, centerY, { steps: 16 });
  await page.mouse.up();
}

async function dragPropertySidebarWithTouch(page, deltaX) {
  const resizer = page.locator(PROPERTY_RESIZER_SELECTOR);
  await expect(resizer).toBeVisible();

  const box = await resizer.boundingBox();
  if (!box) {
    throw new Error('Property sidebar resizer is not visible.');
  }

  const startX = box.x + box.width / 2;
  const centerY = box.y + box.height / 2;
  const endX = startX + deltaX;

  await page.evaluate(({ selector, startX, endX, centerY }) => {
    const handle = document.querySelector(selector);
    if (!handle) {
      throw new Error('Property sidebar resizer is not available.');
    }

    const dispatch = (target, type, clientX) => {
      target.dispatchEvent(new PointerEvent(type, {
        bubbles: true,
        cancelable: true,
        composed: true,
        pointerId: 42,
        pointerType: 'touch',
        isPrimary: true,
        clientX,
        clientY: centerY,
        button: 0,
        buttons: type === 'pointerup' ? 0 : 1
      }));
    };

    dispatch(handle, 'pointerdown', startX);
    dispatch(window, 'pointermove', endX);
    dispatch(window, 'pointerup', endX);
  }, {
    selector: PROPERTY_RESIZER_SELECTOR,
    startX,
    endX,
    centerY
  });
}

test.describe('Flow Editor', () => {
  test.beforeEach(async ({ page }) => {
    await bootAuthenticatedApp(page);
  });

  test('should have canvas visible', async ({ page }) => {
    await expect(page.locator('#flow-canvas')).toBeVisible();
  });

  test('should have operator library', async ({ page }) => {
    await expect(page.locator('#operator-library')).toBeVisible();
  });

  test('shows a property sidebar resizer and supports wider and narrower widths', async ({ page }) => {
    await expect(page.locator(PROPERTY_RESIZER_SELECTOR)).toBeVisible();

    const initialWidth = await getSidebarWidth(page);
    expect(initialWidth).toBe(280);

    await dragPropertySidebar(page, -120);
    const widerWidth = await getSidebarWidth(page);
    expect(widerWidth).toBeGreaterThan(initialWidth + 80);

    await dragPropertySidebar(page, 90);
    const narrowerWidth = await getSidebarWidth(page);
    expect(narrowerWidth).toBeLessThan(widerWidth - 50);

    expect(await page.evaluate(() => document.body.classList.contains('property-sidebar-resizing'))).toBe(false);
  });

  test('restores the last property sidebar width after refresh', async ({ page }) => {
    await dragPropertySidebar(page, -150);
    const expectedWidth = await getSidebarWidth(page);

    const storedWidth = await page.evaluate(storageKey => {
      return window.localStorage.getItem(storageKey);
    }, PROPERTY_SIDEBAR_STORAGE_KEY);

    expect(storedWidth).toBe(String(expectedWidth));

    await page.reload();
    await expect(page.locator(PROPERTY_RESIZER_SELECTOR)).toBeVisible();
    await expect.poll(() => getSidebarWidth(page)).toBe(expectedWidth);
  });

  test('supports keyboard resizing on the property sidebar separator', async ({ page }) => {
    const resizer = page.locator(PROPERTY_RESIZER_SELECTOR);
    await expect(resizer).toHaveAttribute('tabindex', '0');

    const initialWidth = await getSidebarWidth(page);
    await resizer.focus();

    await page.keyboard.press('ArrowLeft');
    await expect.poll(() => getSidebarWidth(page)).toBe(initialWidth + 16);

    await page.keyboard.press('End');
    const viewportWidth = page.viewportSize()?.width ?? 1280;
    await expect.poll(() => getSidebarWidth(page)).toBe(Math.min(560, Math.round(viewportWidth * 0.45)));

    await page.keyboard.press('Home');
    await expect.poll(() => getSidebarWidth(page)).toBe(240);
  });

  test('accepts touch pointer drags on the property sidebar separator', async ({ page }) => {
    const initialWidth = await getSidebarWidth(page);

    await dragPropertySidebarWithTouch(page, -96);
    await expect.poll(() => getSidebarWidth(page)).toBeGreaterThan(initialWidth + 60);
  });

  test('clamps the property sidebar width to the configured min and max bounds', async ({ page }) => {
    const viewportWidth = page.viewportSize()?.width ?? 1280;
    const expectedMaxWidth = Math.min(560, Math.round(viewportWidth * 0.45));

    await dragPropertySidebar(page, -2000);
    await expect.poll(() => getSidebarWidth(page)).toBe(expectedMaxWidth);

    await dragPropertySidebar(page, 2000);
    await expect.poll(() => getSidebarWidth(page)).toBe(240);
  });

  test('disables the property sidebar resizer outside the flow view', async ({ page }) => {
    await page.locator('.nav-btn[data-view="results"]').click();

    await expect(page.locator('#results-view')).toBeVisible();
    await expect(page.locator(PROPERTY_SIDEBAR_SELECTOR)).toBeHidden();
    await expect(page.locator(PROPERTY_RESIZER_SELECTOR)).toBeHidden();
  });
});

