import { test, expect, Page } from '@playwright/test';
import { bootAuthenticatedApp } from './authHelper';

const PROJECT_ID = 'project-regression-001';
const PROJECT_NAME = 'Regression Project';
const CAMERA_ID = 'camera-bind-001';
const TINY_PNG_BASE64 =
    'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO7Z7KkAAAAASUVORK5CYII=';

const projectSummary = {
    id: PROJECT_ID,
    name: PROJECT_NAME,
    description: 'High-frequency regression project',
    version: '1.0.0',
    createdAt: '2026-03-20T09:00:00Z',
    modifiedAt: '2026-03-20T09:30:00Z',
};

const projectDetail = {
    ...projectSummary,
    flow: {
        nodes: [],
        edges: [],
    },
};

const cameraBinding = {
    id: CAMERA_ID,
    displayName: 'Cam_Main_01',
    serialNumber: 'SN-CAM-001',
    cameraId: 'CAM-001',
    deviceType: 'Hikvision',
    triggerMode: 'Software',
    exposureTimeUs: 12000,
    gainDb: 1.5,
    imageWidth: 1920,
    imageHeight: 1080,
    isEnabled: true,
};

function buildSettingsPayload() {
    return {
        general: {
            language: 'zh-CN',
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
        cameras: [cameraBinding],
    };
}

async function mockProjectApis(page: Page) {
    await page.route('**/api/projects', async route => {
        await route.fulfill({
            status: 200,
            contentType: 'application/json',
            body: JSON.stringify([projectSummary]),
        });
    });

    await page.route(`**/api/projects/${PROJECT_ID}`, async route => {
        await route.fulfill({
            status: 200,
            contentType: 'application/json',
            body: JSON.stringify(projectDetail),
        });
    });
}

async function mockSettingsApis(
    page: Page,
    options: {
        onPlcSettingsPut?: (payload: any) => void;
        onSettingsPut?: (payload: any) => void;
    } = {}) {
    const settingsPayload = buildSettingsPayload();

    await page.route('**/api/settings', async route => {
        const method = route.request().method();
        if (method === 'GET') {
            await route.fulfill({
                status: 200,
                contentType: 'application/json',
                body: JSON.stringify(settingsPayload),
            });
            return;
        }

        const payload = JSON.parse(route.request().postData() || '{}');
        options.onSettingsPut?.(payload);
        Object.assign(settingsPayload, payload);
        if (payload.communication) {
            settingsPayload.communication = payload.communication;
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

    await page.route('**/api/users', async route => {
        await route.fulfill({
            status: 200,
            contentType: 'application/json',
            body: JSON.stringify([]),
        });
    });

    await page.route('**/api/ai/models', async route => {
        await route.fulfill({
            status: 200,
            contentType: 'application/json',
            body: JSON.stringify([]),
        });
    });

    await page.route('**/api/cameras/bindings', async route => {
        if (route.request().method() === 'GET') {
            await route.fulfill({
                status: 200,
                contentType: 'application/json',
                body: JSON.stringify([cameraBinding]),
            });
            return;
        }

        await route.fulfill({
            status: 200,
            contentType: 'application/json',
            body: JSON.stringify({ success: true }),
        });
    });

    await page.route('**/api/cameras/soft-trigger-capture', async route => {
        await route.fulfill({
            status: 200,
            contentType: 'image/png',
            body: Buffer.from(TINY_PNG_BASE64, 'base64'),
            headers: {
                'X-Camera-Id': CAMERA_ID,
                'X-Trigger-Mode': 'Software',
                'X-Image-Width': '1920',
                'X-Image-Height': '1080',
            },
        });
    });

    await page.route('**/api/plc/settings', async route => {
        if (route.request().method() !== 'GET') {
            const payload = JSON.parse(route.request().postData() || '{}');
            options.onPlcSettingsPut?.(payload);
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
            return;
        }

        await route.fulfill({
            status: 200,
            contentType: 'application/json',
            body: JSON.stringify({
                success: true,
                settings: settingsPayload.communication,
            }),
        });
    });
}

async function mockResultsApis(page: Page) {
    await page.route(`**/api/inspection/history/${PROJECT_ID}**`, async route => {
        const url = new URL(route.request().url());
        const status = url.searchParams.get('status');
        await route.fulfill({
            status: 200,
            contentType: 'application/json',
            body: JSON.stringify({
                items: status === 'ng'
                    ? [
                        {
                            status: 'NG',
                            defects: [{ type: 'Scratch', description: 'Scratch' }],
                            processingTimeMs: 28,
                            timestamp: '2026-03-20T10:10:00Z',
                            confidenceScore: 0.88,
                            outputData: { station: 'S1' },
                        }
                    ]
                    : [
                        {
                            status: 'NG',
                            defects: [{ type: 'Scratch', description: 'Scratch' }],
                            processingTimeMs: 28,
                            timestamp: '2026-03-20T10:10:00Z',
                            confidenceScore: 0.88,
                            outputData: { station: 'S1' },
                        },
                        {
                            status: 'OK',
                            defects: [],
                            processingTimeMs: 22,
                            timestamp: '2026-03-20T10:11:00Z',
                            confidenceScore: 0.97,
                            outputData: { station: 'S1' },
                        },
                    ],
                totalCount: status === 'ng' ? 1 : 10,
                pageIndex: 0,
                pageSize: status === 'ng' ? 1 : 12,
            }),
        });
    });

    await page.route(`**/api/analysis/report/${PROJECT_ID}**`, async route => {
        const url = new URL(route.request().url());
        const status = url.searchParams.get('status');
        await route.fulfill({
            status: 200,
            contentType: 'application/json',
            body: JSON.stringify({
                summary: {
                    totalCount: status === 'ng' ? 1 : 10,
                    okCount: status === 'ng' ? 0 : 7,
                    ngCount: status === 'ng' ? 1 : 3,
                    errorCount: 0,
                    averageProcessingTimeMs: status === 'ng' ? 28 : 25,
                },
                defectDistribution: status === 'ng'
                    ? [{ defectType: 'Scratch', count: 1 }]
                    : [{ defectType: 'Scratch', count: 3 }],
                hourlyTrend: [
                    {
                        timestamp: '2026-03-20T10:00:00Z',
                        totalCount: status === 'ng' ? 1 : 10,
                        ngCount: status === 'ng' ? 1 : 3,
                        errorCount: 0
                    },
                ],
            }),
        });
    });

    await page.route(`**/api/analysis/statistics/${PROJECT_ID}**`, async route => {
        const url = new URL(route.request().url());
        const status = url.searchParams.get('status');
        await route.fulfill({
            status: 200,
            contentType: 'application/json',
            body: JSON.stringify({
                totalCount: status === 'ng' ? 1 : 10,
                okCount: status === 'ng' ? 0 : 7,
                ngCount: status === 'ng' ? 1 : 3,
                errorCount: 0,
                averageProcessingTimeMs: status === 'ng' ? 28 : 25,
            }),
        });
    });

    await page.route(`**/api/analysis/defect-distribution/${PROJECT_ID}**`, async route => {
        const url = new URL(route.request().url());
        const status = url.searchParams.get('status');
        await route.fulfill({
            status: 200,
            contentType: 'application/json',
            body: JSON.stringify({
                items: [{ defectType: 'Scratch', count: status === 'ng' ? 1 : 3 }],
            }),
        });
    });

    await page.route(`**/api/analysis/trend/${PROJECT_ID}**`, async route => {
        const url = new URL(route.request().url());
        const status = url.searchParams.get('status');
        await route.fulfill({
            status: 200,
            contentType: 'application/json',
            body: JSON.stringify({
                dataPoints: [
                    {
                        timestamp: '2026-03-20T10:00:00Z',
                        ngCount: status === 'ng' ? 1 : 3,
                        errorCount: 0,
                        defectCount: status === 'ng' ? 1 : 3
                    },
                ],
            }),
        });
    });
}

async function mockInspectionApis(page: Page) {
    await page.addInitScript(() => {
        class MockEventSource {
            addEventListener() {}
            close() {}
        }

        // @ts-expect-error test shim
        window.EventSource = MockEventSource;
    });

    await page.route('**/api/inspection/realtime/start', async route => {
        await route.fulfill({
            status: 200,
            contentType: 'application/json',
            body: JSON.stringify({ success: true }),
        });
    });

    await page.route('**/api/inspection/realtime/stop', async route => {
        await route.fulfill({
            status: 200,
            contentType: 'application/json',
            body: JSON.stringify({ success: true }),
        });
    });
}

async function mockHealthApi(page: Page, ok = true) {
    await page.route('**/api/health', async route => {
        await route.fulfill({
            status: ok ? 200 : 503,
            contentType: 'application/json',
            body: JSON.stringify({
                status: ok ? 'Healthy' : 'Unavailable'
            }),
        });
    });
}

async function bootRegressionApp(page: Page) {
    await page.addInitScript(() => {
        localStorage.setItem('cv_welcome_shown', 'true');
    });

    await bootAuthenticatedApp(page);
    await page.locator('#loading-screen').waitFor({ state: 'detached', timeout: 2000 }).catch(() => {});
    await expect(page.locator('.welcome-overlay')).toHaveCount(0);
}

async function openProject(page: Page) {
    await page.locator('.nav-btn[data-view="project"]').click();
    await expect(page.locator('#project-list')).toContainText(PROJECT_NAME);
    await page.locator('.project-list-item .btn-open').first().click();
    await expect(page.locator('#project-name')).toHaveText(PROJECT_NAME);
    await expect(page.locator('.nav-btn.active[data-view="flow"]')).toBeVisible();
}

test.describe('High Frequency Regression', () => {
    test('login regression: unauthenticated user is redirected to login page', async ({ page }) => {
        await page.goto('/index.html');

        await expect(page).toHaveURL(/\/login\.html$/);
        await expect(page.locator('#loginForm')).toBeVisible();
        await expect(page.locator('#username')).toBeVisible();
    });

    test('project open regression: opening a project hydrates status and returns to flow view', async ({ page }) => {
        await mockProjectApis(page);
        await bootRegressionApp(page);

        await openProject(page);
        await expect(page.locator('#flow-editor')).toBeVisible();
    });

    test('ai health regression: ai panel health check uses unified /api/health contract', async ({ page }) => {
        await mockProjectApis(page);
        await mockHealthApi(page, true);
        await bootRegressionApp(page);

        await openProject(page);

        await page.locator('.nav-btn[data-view="ai"]').click();
        await expect(page.locator('#ai-conn-status .status-dot')).toHaveClass(/connected/);
    });

    test('settings camera regression: camera selection gates preview and hand-eye calibration', async ({ page }) => {
        await mockProjectApis(page);
        await mockSettingsApis(page);
        await bootRegressionApp(page);

        await page.locator('.nav-btn[data-view="settings"]').click();
        await expect(page.locator('.settings-menu-item[data-tab="cameras"]')).toBeVisible();
        await page.locator('.settings-menu-item[data-tab="cameras"]').click();

        const previewButton = page.locator('#btn-camera-preview');
        const calibButton = page.locator('#btn-hand-eye-calib');

        await expect(previewButton).toBeDisabled();
        await expect(calibButton).toBeDisabled();

        await page.locator('#camera-bindings-table tbody tr.camera-row').first().click();

        await expect(previewButton).toBeEnabled();
        await expect(calibButton).toBeEnabled();
        await expect(page.locator('#camera-selection-hint')).toContainText('当前已选中');

        await previewButton.click();
        await expect(page.locator('#camera-preview-image')).toBeVisible();
        await expect(page.locator('#camera-preview-meta')).toContainText('1920 x 1080');
        await page.keyboard.press('Escape');
        await expect(page.locator('#camera-preview-image')).toHaveCount(0);

        await calibButton.click();
        await expect(page.locator('.calib-wizard-title')).toContainText('手眼标定向导');
        await expect(page.locator('#calib-camera-placeholder-secondary')).toContainText('刷新预览');
        await page.locator('#calib-btn-close').click();
    });

    test('settings plc regression: save all preserves drafts across protocols', async ({ page }) => {
        const plcSettingsPuts: any[] = [];
        const settingsPuts: any[] = [];

        await mockProjectApis(page);
        await mockSettingsApis(page, {
            onPlcSettingsPut: payload => plcSettingsPuts.push(payload),
            onSettingsPut: payload => settingsPuts.push(payload),
        });
        await bootRegressionApp(page);

        await page.locator('.nav-btn[data-view="settings"]').click();
        await expect(page.locator('.settings-menu-item[data-tab="communication"]')).toBeVisible();
        await page.locator('.settings-menu-item[data-tab="communication"]').click();

        await page.locator('#cfg-plcIpAddress').fill('10.10.10.11');
        await page.locator('#cfg-plcPort').fill('1102');

        await page.locator('#cfg-protocol').selectOption('MC');
        await expect(page.locator('#cfg-plcPort')).toHaveValue('5002');

        await page.locator('#cfg-plcIpAddress').fill('10.10.10.22');
        await page.locator('#cfg-plcPort').fill('5003');

        await page.locator('#btn-save-settings').click();

        await expect.poll(() => plcSettingsPuts.length).toBe(1);
        expect(plcSettingsPuts[0].activeProtocol).toBe('MC');
        expect(plcSettingsPuts[0].s7.ipAddress).toBe('10.10.10.11');
        expect(plcSettingsPuts[0].s7.port).toBe(1102);
        expect(plcSettingsPuts[0].mc.ipAddress).toBe('10.10.10.22');
        expect(plcSettingsPuts[0].mc.port).toBe(5003);

        await expect.poll(() => settingsPuts.length).toBe(1);
        expect(settingsPuts[0].communication.s7.ipAddress).toBe('10.10.10.11');
        expect(settingsPuts[0].communication.s7.port).toBe(1102);
        expect(settingsPuts[0].communication.mc.ipAddress).toBe('10.10.10.22');
        expect(settingsPuts[0].communication.mc.port).toBe(5003);
    });

    test('continuous run regression: protection guidance is visible before and after continuous run', async ({ page }) => {
        await mockProjectApis(page);
        await mockSettingsApis(page);
        await mockInspectionApis(page);
        await bootRegressionApp(page);

        await openProject(page);

        await page.locator('.nav-btn[data-view="inspection"]').click();
        await expect(page.locator('#protection-summary')).toContainText('运行保护已开启');

        await page.locator('#run-mode').selectOption('flow');
        await page.locator('#btn-run-continuous').click();

        await expect(page.locator('#protection-status')).toContainText('自动停止连续运行');
        await expect(page.locator('#btn-stop')).toBeEnabled();

        await page.locator('#btn-stop').click();
        await expect(page.locator('#protection-status')).toContainText('连续运行已停止');
        await expect(page.locator('#btn-run-continuous')).toBeEnabled();
    });

    test('result filter regression: server-paged filters refresh server-backed history and analytics', async ({ page }) => {
        await mockProjectApis(page);
        await mockResultsApis(page);
        await bootRegressionApp(page);

        await openProject(page);

        await page.locator('.nav-btn[data-view="results"]').click();
        await expect(page.locator('#results-count-info')).toContainText('当前页 2 条 / 共 10 条记录');

        await page.locator('#filter-status').selectOption('ng');

        await expect(page.locator('#results-count-info')).toContainText('当前页 1 条 / 共 1 条记录');
        await expect(page.locator('#results-grid')).toContainText('NG');
    });
});
