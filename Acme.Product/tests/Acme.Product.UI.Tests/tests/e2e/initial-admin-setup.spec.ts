import { test, expect } from '@playwright/test';

test.describe('initial admin setup', () => {
  test('shows blocking setup form when no users exist', async ({ page }) => {
    await page.route('**/api/auth/setup-status', async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          requiresInitialAdminSetup: true,
          usernameMinLength: 3,
          passwordMinLength: 8,
          requiresUppercase: false,
          requiresLowercase: false,
          requiresDigit: false,
        }),
      });
    });

    await page.goto('/login.html');

    await expect(page.locator('#setupForm')).toBeVisible();
    await expect(page.locator('#loginForm')).toBeHidden();
    await expect(page.locator('#loginModeBadge')).toContainText('首次初始化');
    await expect(page.locator('#setupRulesList')).toContainText('密码长度不少于 8 位');
  });

  test('creates initial admin and redirects to index automatically', async ({ page }) => {
    await page.addInitScript(() => {
      localStorage.setItem('cv_welcome_shown', 'true');
    });

    await page.route('**/api/auth/setup-status', async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          requiresInitialAdminSetup: true,
          usernameMinLength: 3,
          passwordMinLength: 8,
          requiresUppercase: false,
          requiresLowercase: false,
          requiresDigit: false,
        }),
      });
    });

    await page.route('**/api/auth/setup-admin', async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          token: 'setup-token',
          user: {
            id: 'admin-1',
            username: 'factory-admin',
            displayName: 'factory-admin',
            role: 'Admin',
            isActive: true,
          },
        }),
      });
    });

    await page.route('**/api/auth/me', async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          userId: 'admin-1',
          username: 'factory-admin',
          role: 'Admin',
        }),
      });
    });

    await page.route('**/api/projects**', async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([]),
      });
    });

    await page.goto('/login.html');
    await page.locator('#setupUsername').fill('factory-admin');
    await page.locator('#setupPassword').fill('password1');
    await page.locator('#setupConfirmPassword').fill('password1');
    await page.locator('#setupBtn').click();

    await page.waitForURL(/\/index\.html$/);

    const token = await page.evaluate(() => sessionStorage.getItem('cv_auth_token'));
    const storedUser = await page.evaluate(() => sessionStorage.getItem('cv_current_user'));

    expect(token).toBe('setup-token');
    expect(storedUser).toContain('factory-admin');
  });
});
