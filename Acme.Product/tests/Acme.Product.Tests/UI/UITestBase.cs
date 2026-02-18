// UITestBase.cs
// UITestBase实现
// 作者：蘅芜君

using Microsoft.Playwright;
using Xunit;

namespace Acme.Product.Tests.UI;

/// <summary>
/// UI 测试基类 - Playwright 配置
/// Sprint 5: S5-007 实现
/// </summary>
public abstract class UITestBase : IAsyncLifetime
{
    protected IPlaywright? Playwright { get; private set; }
    protected IBrowser? Browser { get; private set; }
    protected IBrowserContext? Context { get; private set; }
    protected IPage? Page { get; private set; }

    /// <summary>
    /// 测试初始化
    /// </summary>
    public async Task InitializeAsync()
    {
        Playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        
        Browser = await Playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true,
            SlowMo = 100
        });

        Context = await Browser.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize { Width = 1920, Height = 1080 },
            RecordVideoDir = "videos/",
            RecordVideoSize = new RecordVideoSize { Width = 1920, Height = 1080 }
        });

        Page = await Context.NewPageAsync();
    }

    /// <summary>
    /// 测试清理
    /// </summary>
    public async Task DisposeAsync()
    {
        if (Page != null)
        {
            await Page.CloseAsync();
        }

        if (Context != null)
        {
            await Context.CloseAsync();
        }

        if (Browser != null)
        {
            await Browser.CloseAsync();
        }

        Playwright?.Dispose();
    }

    /// <summary>
    /// 导航到应用
    /// </summary>
    protected async Task NavigateToAppAsync(string url = "http://localhost:5000")
    {
        if (Page == null) throw new InvalidOperationException("Page not initialized");
        
        await Page.GotoAsync(url);
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    /// <summary>
    /// 截图
    /// </summary>
    protected async Task TakeScreenshotAsync(string name)
    {
        if (Page == null) return;
        
        var path = $"screenshots/{name}_{DateTime.Now:yyyyMMdd_HHmmss}.png";
        Directory.CreateDirectory("screenshots");
        await Page.ScreenshotAsync(new PageScreenshotOptions { Path = path });
    }
}
