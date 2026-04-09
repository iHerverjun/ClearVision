import { expect, Page, test } from '@playwright/test';
import { bootAuthenticatedApp } from './authHelper';

function buildSettingsPayload() {
    return {
        general: {
            softwareTitle: 'ClearVision',
            theme: 'dark',
            autoStart: false,
        },
        communication: {
            activeProtocol: 'S7',
            heartbeatIntervalMs: 1000,
            s7: {
                ipAddress: '127.0.0.1',
                port: 102,
                cpuType: 'S7-1200',
                rack: 0,
                slot: 1,
                mappings: [],
            },
            mc: {
                ipAddress: '127.0.0.1',
                port: 5002,
                mappings: [],
            },
            fins: {
                ipAddress: '127.0.0.1',
                port: 9600,
                mappings: [],
            },
        },
        storage: {
            imageSavePath: 'C:/Regression/Images',
            savePolicy: 'All',
            maxDiskUsagePercent: 80,
            retentionDays: 7,
        },
        runtime: {
            autoRun: false,
            stopOnConsecutiveNg: 2,
            missingMaterialTimeoutSeconds: 15,
            applyProtectionRules: true,
        },
        cameras: [],
        activeCameraId: '',
    };
}

function delay(ms: number) {
    return new Promise(resolve => setTimeout(resolve, ms));
}

async function mockThemeApis(page: Page, options: { settingsGetDelayMs?: number } = {}) {
    const settingsPayload = buildSettingsPayload();
    const requestStats = {
        settingsPutCount: 0,
        themePutCount: 0,
    };

    await page.route('**/api/settings/theme', async route => {
        requestStats.themePutCount += 1;
        const payload = JSON.parse(route.request().postData() || '{}');
        const requestedTheme = `${payload.theme || ''}`.trim().toLowerCase();
        settingsPayload.general.theme = requestedTheme === 'light' ? 'light' : 'dark';

        await route.fulfill({
            status: 200,
            contentType: 'application/json',
            body: JSON.stringify({
                message: '主题已保存',
                theme: settingsPayload.general.theme,
            }),
        });
    });

    await page.route('**/api/settings', async route => {
        if (route.request().method() === 'GET') {
            if (options.settingsGetDelayMs) {
                await delay(options.settingsGetDelayMs);
            }

            await route.fulfill({
                status: 200,
                contentType: 'application/json',
                body: JSON.stringify(settingsPayload),
            });
            return;
        }

        requestStats.settingsPutCount += 1;
        const payload = JSON.parse(route.request().postData() || '{}');
        Object.assign(settingsPayload, payload);
        if (payload.general) {
            settingsPayload.general = { ...settingsPayload.general, ...payload.general };
        }
        if (payload.communication) {
            settingsPayload.communication = payload.communication;
        }
        if (payload.storage) {
            settingsPayload.storage = payload.storage;
        }
        if (payload.runtime) {
            settingsPayload.runtime = payload.runtime;
        }
        if (payload.cameras) {
            settingsPayload.cameras = payload.cameras;
        }
        if (payload.activeCameraId !== undefined) {
            settingsPayload.activeCameraId = payload.activeCameraId;
        }

        await route.fulfill({
            status: 200,
            contentType: 'application/json',
            body: JSON.stringify(settingsPayload),
        });
    });

    await page.route('**/api/settings/disk-usage**', async route => {
        await route.fulfill({
            status: 200,
            contentType: 'application/json',
            body: JSON.stringify({
                totalGb: 200,
                usedGb: 24,
                freeGb: 176,
                usedPercent: 12,
            }),
        });
    });

    await page.route('**/api/plc/settings', async route => {
        if (route.request().method() === 'GET') {
            await route.fulfill({
                status: 200,
                contentType: 'application/json',
                body: JSON.stringify({
                    success: true,
                    settings: settingsPayload.communication,
                }),
            });
            return;
        }

        const payload = JSON.parse(route.request().postData() || '{}');
        settingsPayload.communication = payload;
        await route.fulfill({
            status: 200,
            contentType: 'application/json',
            body: JSON.stringify({
                success: true,
                settings: settingsPayload.communication,
                errors: [],
            }),
        });
    });

    await page.route('**/api/cameras/bindings', async route => {
        if (route.request().method() === 'GET') {
            await route.fulfill({
                status: 200,
                contentType: 'application/json',
                body: JSON.stringify(settingsPayload.cameras),
            });
            return;
        }

        const payload = JSON.parse(route.request().postData() || '[]');
        settingsPayload.cameras = Array.isArray(payload) ? payload : settingsPayload.cameras;
        await route.fulfill({
            status: 200,
            contentType: 'application/json',
            body: JSON.stringify({ success: true }),
        });
    });

    await page.route('**/api/users', async route => {
        await route.fulfill({
            status: 200,
            contentType: 'application/json',
            body: JSON.stringify([]),
        });
    });

    return {
        requestStats,
        settingsPayload,
    };
}

async function waitForAppReady(page: Page) {
    await expect(page.locator('#app')).toBeVisible();
    await expect(page.locator('#loading-screen')).toBeHidden();
}

test('theme cache is corrected by backend settings and persists across toolbar + settings saves', async ({ page }) => {
    const { settingsPayload, requestStats } = await mockThemeApis(page);

    await page.addInitScript(() => {
        localStorage.setItem('cv_theme', 'light');
    });

    await bootAuthenticatedApp(page);
    await waitForAppReady(page);

    const root = page.locator('html');
    await expect(root).toHaveAttribute('data-theme', 'dark');

    await page.locator('#btn-theme-toggle').click();
    await expect(root).toHaveAttribute('data-theme', 'light');
    await expect.poll(() => settingsPayload.general.theme).toBe('light');
    await expect.poll(() => requestStats.themePutCount).toBe(1);
    await expect.poll(() => requestStats.settingsPutCount).toBe(0);

    await page.reload();
    await waitForAppReady(page);
    await expect(root).toHaveAttribute('data-theme', 'light');

    await page.locator('.nav-btn[data-view="settings"]').click();
    await expect(page.locator('#cfg-theme')).toHaveValue('light');

    await page.selectOption('#cfg-theme', 'dark');
    await page.locator('#btn-save-settings').click();

    await expect(root).toHaveAttribute('data-theme', 'dark');
    await expect.poll(() => settingsPayload.general.theme).toBe('dark');
    await expect.poll(() => requestStats.settingsPutCount).toBe(1);

    await page.reload();
    await waitForAppReady(page);
    await expect(root).toHaveAttribute('data-theme', 'dark');
    await expect(page.locator('html')).toHaveAttribute('data-theme', 'dark');
});

test('slow theme sync does not block app startup', async ({ page }) => {
    await mockThemeApis(page, { settingsGetDelayMs: 6000 });

    await page.addInitScript(() => {
        localStorage.setItem('cv_theme', 'light');
    });

    await bootAuthenticatedApp(page);
    await waitForAppReady(page);

    await expect(page.locator('html')).toHaveAttribute('data-theme', 'light');
});
