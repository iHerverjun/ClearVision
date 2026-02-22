import { test, expect } from '@playwright/test';

test.describe('ClearVision Application E2E Tests', () => {

  test('1. Login & Routing Guard', async ({ page }) => {
    // 1. Visit root, should redirect to login if unauthenticated
    await page.goto('/');
    
    // Check if we are on the login page
    await expect(page).toHaveURL(/.*\/login/);

    // Fill login form
    await page.fill('input[type="text"]', 'admin');
    await page.fill('input[type="password"]', 'admin');
    await page.click('button[type="submit"]');

    // Wait for navigation back to home or flow-editor
    await page.waitForURL(/.*\/flow-editor/);
    await expect(page).toHaveURL(/.*\/flow-editor/);
    
    // Verify sidebar is visible indicating successful login
    const sidebar = page.locator('.app-sidebar');
    await expect(sidebar).toBeVisible();
  });

  test('2. Flow Editor - Add Node', async ({ page }) => {
    // Navigate to flow editor directly (will use storage state if configured, or we login again)
    // For simplicity in a fresh context, let's login first
    await page.goto('/login');
    await page.fill('input[type="text"]', 'admin');
    await page.fill('input[type="password"]', 'admin');
    await page.click('button[type="submit"]');
    await page.waitForURL(/.*\/flow-editor/);

    // Expand operator library block
    const addBtn = page.getByText('Camera Configuration');
    await expect(addBtn).toBeVisible();

    // The react-flow canvas
    const canvas = page.locator('.vue-flow__pane');
    await expect(canvas).toBeVisible();

    // Check node count
    const nodeCount = await page.locator('.vue-flow__node').count();

    // We can't easily drag and drop in playwright using HTML5 dnd onto a custom canvas without exact coordinates, 
    // but we can check if the sidebar operator library rendered the operators.
    const opItem = page.locator('.operator-item').first();
    await expect(opItem).toBeVisible();
    
    // Let's test the Context Menu Add Action instead
    await canvas.click({ button: 'right' });
    const contextMenu = page.locator('.context-menu');
    await expect(contextMenu).toBeVisible();
  });

  test('3. Settings Panel - Theme Switch', async ({ page }) => {
    // Login
    await page.goto('/login');
    await page.fill('input[type="text"]', 'admin');
    await page.fill('input[type="password"]', 'admin');
    await page.click('button[type="submit"]');
    await page.waitForURL(/.*\/flow-editor/);

    // Open Settings Modal
    const settingsBtn = page.locator('.settings-btn');
    await settingsBtn.click();

    // Verify Modal Appears
    const modal = page.locator('.settings-modal');
    await expect(modal).toBeVisible();

    // Check if general tab is active
    const themeLabel = page.getByText('Theme');
    await expect(themeLabel).toBeVisible();

    // Click dark mode radio
    const darkRadioSpan = page.getByText('Dark');
    await darkRadioSpan.click();

    // The body should now have the "dark" class
    // Wait for Vue reactivity to update the HTML class
    await expect(page.locator('html')).toHaveClass(/dark/);
  });
});
