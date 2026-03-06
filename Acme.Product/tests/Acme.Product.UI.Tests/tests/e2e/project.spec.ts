import { test, expect } from '@playwright/test';
import { bootAuthenticatedApp } from './authHelper';

test.describe('ClearVision E2E', () => {
  test.beforeEach(async ({ page }) => {
    await bootAuthenticatedApp(page);
  });

  test('should load the home page', async ({ page }) => {
    await expect(page).toHaveTitle(/ClearVision/);
    await expect(page.locator('#app')).toBeVisible();
  });

  test('should verify default status', async ({ page }) => {
    await expect(page.locator('#status-text')).toBeVisible();
    await expect(page.locator('#project-name')).toBeVisible();
  });
});

