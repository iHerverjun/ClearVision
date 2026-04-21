import { expect, Page, test } from '@playwright/test';
import { bootAuthenticatedApp } from './authHelper';

function createDarkSettingsPayload() {
  return {
    general: {
      softwareTitle: 'ClearVision',
      theme: 'dark',
      autoStart: false,
    },
    runtime: {
      autoRun: false,
      stopOnConsecutiveNg: 2,
      missingMaterialTimeoutSeconds: 15,
      applyProtectionRules: true,
    },
  };
}

async function mockDarkSettings(page: Page) {
  const settingsPayload = createDarkSettingsPayload();

  await page.route('**/api/settings', async route => {
    if (route.request().method() === 'GET') {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(settingsPayload),
      });
      return;
    }

    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(settingsPayload),
    });
  });
}

async function readBackgroundColor(page: Page, selector: string) {
  return page.locator(selector).evaluate(node => getComputedStyle(node).backgroundColor);
}

test('dark theme keeps flow and inspection critical surfaces off white', async ({ page }) => {
  await mockDarkSettings(page);
  await page.addInitScript(() => {
    localStorage.setItem('cv_theme', 'dark');
  });

  await bootAuthenticatedApp(page);

  await expect(page.locator('html')).toHaveAttribute('data-theme', 'dark');
  await expect.poll(() => readBackgroundColor(page, '#flow-canvas')).toContain('11, 24, 36');
  await expect.poll(() => readBackgroundColor(page, '#property-panel')).toContain('15, 36, 53');

  await page.locator('.nav-btn[data-view="inspection"]').click();
  await expect(page.locator('#inspection-view')).toBeVisible();
  await expect(page.locator('.inspection-protection-notice')).toBeVisible();

  await expect.poll(() => readBackgroundColor(page, '#inspection-image-area')).toContain('15, 36, 53');
  await expect.poll(() => readBackgroundColor(page, '.inspection-protection-notice')).toContain('66, 46, 18');
});
