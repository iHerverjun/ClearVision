import { test, expect } from '@playwright/test';
import { bootAuthenticatedApp } from './authHelper';

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
});

