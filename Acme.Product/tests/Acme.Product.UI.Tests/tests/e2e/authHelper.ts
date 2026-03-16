import { expect, Page } from '@playwright/test';

const E2E_USER = {
  username: 'admin',
  displayName: 'E2E Admin',
  role: 'Admin',
};

export async function bootAuthenticatedApp(page: Page): Promise<void> {
  await page.route('**/api/auth/me', async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(E2E_USER),
    });
  });

  await page.addInitScript(user => {
    sessionStorage.setItem('cv_auth_token', 'e2e-token');
    sessionStorage.setItem('cv_current_user', JSON.stringify(user));
  }, E2E_USER);

  await page.goto('/index.html');
  await expect(page.locator('#app')).toBeVisible();
}
