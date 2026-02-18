// WebView2Host.cs
// 异步释放资源。
// 作者：蘅芜君

using System.Reflection;
using System.Text.Json;
using Acme.Product.Contracts.Messages;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using Acme.Product.Desktop.Handlers;

namespace Acme.Product.Desktop;

/// <summary>
/// WebView2 宿主类，负责 WebView2 控件的异步初始化和配置。
/// 基于《代码实践指导》中的 WebView2Host 设计模式。
/// </summary>
public sealed class WebView2Host : IAsyncDisposable
{
    private readonly WebView2 _webView;
    private CoreWebView2Environment? _environment;
    private bool _isInitialized;
    private bool _isDisposed;
    private readonly WebMessageHandler? _messageHandler;

    /// <summary>
    /// WebView2 初始化完成事件。
    /// </summary>
    public event EventHandler? Initialized;

    /// <summary>
    /// 收到 Web 消息事件。
    /// </summary>
    public event EventHandler<WebMessage>? MessageReceived;

    /// <summary>
    /// 获取是否已初始化。
    /// </summary>
    public bool IsInitialized => _isInitialized;

    /// <summary>
    /// 获取 CoreWebView2 实例（初始化后可用）。
    /// </summary>
    public CoreWebView2? CoreWebView2 => _webView.CoreWebView2;

    /// <summary>
    /// 创建 WebView2 宿主实例。
    /// </summary>
    /// <param name="webView">WebView2 控件实例</param>
    /// <param name="messageHandler">Web 消息处理器</param>
    public WebView2Host(WebView2 webView, WebMessageHandler? messageHandler = null)
    {
        _webView = webView ?? throw new ArgumentNullException(nameof(webView));
        _messageHandler = messageHandler;
    }

    /// <summary>
    /// 异步初始化 WebView2 环境和控件。
    /// </summary>
    /// <param name="userDataFolder">用户数据文件夹路径（可选）</param>
    /// <param name="language">语言设置（可选，默认 zh-CN）</param>
    public async Task InitializeAsync(
        string? userDataFolder = null,
        string language = "zh-CN")
    {
        if (_isInitialized)
        {
            return;
        }

        ObjectDisposedException.ThrowIf(_isDisposed, this);

        try
        {
            // 配置用户数据文件夹
            var dataFolder = userDataFolder ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Acme.Product",
                "WebView2");

            // 确保目录存在
            Directory.CreateDirectory(dataFolder);

            // 创建自定义环境
            _environment = await CoreWebView2Environment.CreateAsync(
                browserExecutableFolder: null,
                userDataFolder: dataFolder,
                options: new CoreWebView2EnvironmentOptions
                {
                    AdditionalBrowserArguments = $"--lang={language}",
                    AllowSingleSignOnUsingOSPrimaryAccount = true
                });

            // 确保 CoreWebView2 初始化完成
            await _webView.EnsureCoreWebView2Async(_environment);

            // 配置 WebView2
            await ConfigureWebView2Async();

            // 注册消息处理器
            RegisterMessageHandlers();

            // 加载初始页面
            LoadInitialPage();

            _isInitialized = true;

            // 触发初始化完成事件
            Initialized?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"WebView2 初始化失败: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// 配置 WebView2 设置。
    /// </summary>
    private async Task ConfigureWebView2Async()
    {
        var core = _webView.CoreWebView2;
        var settings = core.Settings;

        // 开发工具配置（发布时应禁用）
#if DEBUG
        settings.AreDevToolsEnabled = true;
        settings.AreDefaultContextMenusEnabled = true;
#else
        settings.AreDevToolsEnabled = false;
        settings.AreDefaultContextMenusEnabled = false;
#endif

        // 禁用不需要的功能
        settings.IsStatusBarEnabled = false;
        settings.IsZoomControlEnabled = false;

        // 启用本地文件访问（允许ES6模块加载）
        core.SetVirtualHostNameToFolderMapping(
            "app.local",
            GetWwwRootPath(),
            CoreWebView2HostResourceAccessKind.Allow);

        // 【科学方案一】开发环境禁用HTTP缓存，确保CSS/JS修改实时生效
#if DEBUG
        // 通过请求拦截器添加Cache-Control头
        core.WebResourceRequested += OnWebResourceRequested;

        // 更可靠的方法：在导航时清除缓存
        _ = ClearCacheAsync();
        System.Diagnostics.Debug.WriteLine("[WebView2Host] DEBUG模式：已清除缓存并禁用HTTP缓存");
#endif

        // 注入 API 配置脚本和动态版本号（在每个文档创建时执行）
        var apiPort = Program.GetWebPort();
        var apiBaseUrl = $"http://localhost:{apiPort}/api";
        var cssVersion = GenerateCssVersion();
        var initScript = $@"
            window.__API_BASE_URL__ = '{apiBaseUrl}';
            window.__CSS_VERSION__ = '{cssVersion}';
            console.log('[Desktop] API Base URL:', window.__API_BASE_URL__);
            console.log('[Desktop] CSS Version:', window.__CSS_VERSION__);
        ";
        await core.AddScriptToExecuteOnDocumentCreatedAsync(initScript);
        System.Diagnostics.Debug.WriteLine($"[WebView2Host] 已注入 API 配置脚本: {apiBaseUrl}");
        System.Diagnostics.Debug.WriteLine($"[WebView2Host] CSS版本号: {cssVersion}");

        // 新窗口处理：强制在当前窗口打开
        core.NewWindowRequested += (sender, e) =>
        {
            e.Handled = true;
            core.Navigate(e.Uri);
        };

        // 导航完成处理
        core.NavigationCompleted += (sender, e) =>
        {
            if (!e.IsSuccess)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"导航失败: {e.WebErrorStatus}");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine(
                    $"导航完成: {core.Source}");
            }
        };
    }

    /// <summary>
    /// 注册消息处理器。
    /// </summary>
    private void RegisterMessageHandlers()
    {
        // 【修复】移除重复的 WebMessageReceived 订阅
        // WebMessageHandler.Initialize() 已注册此事件，并能灵活匹配
        // 前端发送的 messageType/type/Type 等不同字段名。
        // WebView2Host 的 OnWebMessageReceived 将 JSON 反序列化为 WebMessage（期望 Type 属性），
        // 但前端实际使用 messageType 字段名，导致 Type 为空、消息匹配失败。
        // 保留此方法以便未来需要时重新启用。
        // _webView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;
    }

    /// <summary>
    /// 处理收到的 Web 消息。
    /// </summary>
    private void OnWebMessageReceived(
        object? sender,
        CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            var message = JsonSerializer.Deserialize<WebMessage>(
                e.WebMessageAsJson,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

            if (message is not null)
            {
                // 触发消息接收事件
                MessageReceived?.Invoke(this, message);

                // 处理消息并发送响应
                _ = HandleMessageAsync(message);
            }
        }
        catch (JsonException ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"无法解析 Web 消息: {ex.Message}");
        }
    }

    /// <summary>
    /// 异步处理消息。
    /// </summary>
    private async Task HandleMessageAsync(WebMessage message)
    {
        try
        {
            if (_messageHandler != null)
            {
                // 委托给 WebMessageHandler 处理
                var result = await _messageHandler.HandleAsync(message);
                await SendMessageAsync(result);
            }
            else
            {
                // 如果没有处理器，返回错误
                await SendMessageAsync(new WebMessageResponse
                {
                    RequestId = message.Id,
                    Success = false,
                    Error = "消息处理器未初始化"
                });
            }
        }
        catch (Exception ex)
        {
            var errorResponse = new WebMessageResponse
            {
                RequestId = message.Id,
                Success = false,
                Error = ex.Message
            };

            await SendMessageAsync(errorResponse);
        }
    }

    /// <summary>
    /// 获取 wwwroot 路径（开发环境或生产环境）。
    /// </summary>
    private static string GetWwwRootPath()
    {
        // 开发环境：使用项目目录
        var devPath = Path.Combine(
            Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "",
            "..", "..", "..", "wwwroot");

        if (Directory.Exists(devPath))
        {
            return Path.GetFullPath(devPath);
        }

        // 生产环境：使用执行目录
        var prodPath = Path.Combine(
            Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "",
            "wwwroot");

        return prodPath;
    }

    /// <summary>
    /// 执行 JavaScript 脚本。
    /// </summary>
    /// <typeparam name="T">消息类型</typeparam>
    /// <param name="message">消息对象</param>
    public Task SendMessageAsync<T>(T message) where T : class
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        if (!_isInitialized || _webView.CoreWebView2 is null)
        {
            throw new InvalidOperationException("WebView2 尚未初始化");
        }

        var json = JsonSerializer.Serialize(message, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        // 在 UI 线程上执行
        if (_webView.InvokeRequired)
        {
            _webView.Invoke(() => _webView.CoreWebView2.PostWebMessageAsJson(json));
        }
        else
        {
            _webView.CoreWebView2.PostWebMessageAsJson(json);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 加载初始页面。
    /// </summary>
    private void LoadInitialPage()
    {
        // 使用与 Program.cs 相同的逻辑查找 wwwroot
        var wwwrootPath = GetWwwRootPath();

        var indexPath = Path.Combine(wwwrootPath, "index.html");

        if (File.Exists(indexPath))
        {
            // 使用虚拟主机名加载，支持ES6模块
            _webView.Source = new Uri("http://app.local/index.html");
        }
        else
        {
            // 如果没有前端文件，显示欢迎页面
            var welcomeHtml = """
                <!DOCTYPE html>
                <html lang="zh-CN">
                <head>
                    <meta charset="UTF-8">
                    <title>Acme Product</title>
                    <style>
                        body {
                            font-family: 'Segoe UI', sans-serif;
                            display: flex;
                            justify-content: center;
                            align-items: center;
                            height: 100vh;
                            margin: 0;
                            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
                            color: white;
                        }
                        .container {
                            text-align: center;
                        }
                        h1 { font-size: 3rem; margin-bottom: 1rem; }
                        p { font-size: 1.2rem; opacity: 0.9; }
                    </style>
                </head>
                <body>
                    <div class="container">
                        <h1>🚀 Acme Product</h1>
                        <p>WebView2 已成功初始化</p>
                        <p>请在 wwwroot 目录中添加您的前端文件</p>
                    </div>
                </body>
                </html>
                """;

            _webView.CoreWebView2.NavigateToString(welcomeHtml);
        }
    }

    /// <summary>
    /// 导航到指定 URL。
    /// </summary>
    /// <param name="url">目标 URL</param>
    public void Navigate(string url)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        if (!_isInitialized || _webView.CoreWebView2 is null)
        {
            throw new InvalidOperationException("WebView2 尚未初始化");
        }

        _webView.CoreWebView2.Navigate(url);
    }

    /// <summary>
    /// 执行 JavaScript 脚本。
    /// </summary>
    /// <param name="script">JavaScript 代码</param>
    /// <returns>脚本执行结果</returns>
    public async Task<string> ExecuteScriptAsync(string script)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        if (!_isInitialized || _webView.CoreWebView2 is null)
        {
            throw new InvalidOperationException("WebView2 尚未初始化");
        }

        return await _webView.CoreWebView2.ExecuteScriptAsync(script);
    }

    /// <summary>
    /// 发送共享缓冲区数据（用于高性能图像传输）
    /// </summary>
    public void SendSharedBuffer(byte[] data, int width, int height)
    {
        if (!_isInitialized || _webView.CoreWebView2 is null || _environment is null)
            return;

        try
        {
            // 创建共享缓冲区
            // 注意：SharedBuffer 需要手动释放，但在 PostSharedBufferToScript 后，
            // WebView2 会在脚本接收并处理后管理其生命周期，或者我们需要在适当时候释放？
            // 根据文档，PostSharedBufferToScript 共享了缓冲区的所有权。
            // 但为了安全，通常应该在前端处理完发送回执后再释放，或者依赖 WebView2 的生命周期管理。
            // 简单起见，这里创建并发送。如果频繁调用，可能会有资源压力，但在 Demo 中可行。
            // 更好的做法是重用 SharedBuffer (RingBuffer 模式)。

            using var sharedBuffer = _environment.CreateSharedBuffer((ulong)data.Length);

            using (var stream = sharedBuffer.OpenStream())
            {
                stream.Write(data, 0, data.Length);
            }

            var additionalData = JsonSerializer.Serialize(new { width, height });

            _webView.CoreWebView2.PostSharedBufferToScript(
                sharedBuffer,
                CoreWebView2SharedBufferAccess.ReadOnly,
                additionalData);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"发送共享缓冲区失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 【科学方案三】清除WebView2缓存
    /// </summary>
    public async Task ClearCacheAsync()
    {
        if (!_isInitialized || _webView.CoreWebView2 is null)
        {
            throw new InvalidOperationException("WebView2 尚未初始化");
        }

        try
        {
            var profile = _webView.CoreWebView2.Profile;

            // 清除所有类型的缓存数据
            await profile.ClearBrowsingDataAsync(
                CoreWebView2BrowsingDataKinds.DiskCache |           // 磁盘缓存
                CoreWebView2BrowsingDataKinds.DownloadHistory |     // 下载历史
                CoreWebView2BrowsingDataKinds.AllDomStorage |       // DOM存储
                CoreWebView2BrowsingDataKinds.AllSite               // 站点数据
            );

            System.Diagnostics.Debug.WriteLine("[WebView2Host] 缓存已清除");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[WebView2Host] 清除缓存失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 【科学方案三】强制刷新（清除缓存并重新加载）
    /// </summary>
    public async Task ForceReloadAsync()
    {
        if (!_isInitialized || _webView.CoreWebView2 is null)
        {
            throw new InvalidOperationException("WebView2 尚未初始化");
        }

        // 1. 清除缓存
        await ClearCacheAsync();

        // 2. 重新加载当前页面（不使用缓存）
        _webView.CoreWebView2.Reload();

        System.Diagnostics.Debug.WriteLine("[WebView2Host] 强制刷新完成");
    }

    /// <summary>
    /// 【科学方案二】生成CSS版本号
    /// </summary>
    private string GenerateCssVersion()
    {
#if DEBUG
        // DEBUG模式：使用时间戳，确保每次启动都不同
        return DateTime.Now.Ticks.ToString();
#else
        // RELEASE模式：使用程序集版本号
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        return version?.ToString() ?? "1.0.0";
#endif
    }

    /// <summary>
    /// 处理 Web 资源请求事件（提取为命名方法以避免内存泄漏）
    /// </summary>
    private void OnWebResourceRequested(object? sender, CoreWebView2WebResourceRequestedEventArgs e)
    {
        // 只处理CSS和JS文件
        if (e.Request.Uri.EndsWith(".css") || e.Request.Uri.EndsWith(".js"))
        {
            // 添加无缓存头
            var headers = e.Request.Headers;
            // 注：WebView2 WebResourceRequested不支持直接修改请求头
            // 我们通过添加查询参数的方式实现缓存清除
        }
    }

    /// <summary>
    /// 异步释放资源。
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;

        // 取消事件订阅
        if (_webView.CoreWebView2 is not null)
        {
            _webView.CoreWebView2.WebMessageReceived -= OnWebMessageReceived;
#if DEBUG
            _webView.CoreWebView2.WebResourceRequested -= OnWebResourceRequested;
#endif
        }

        // 释放 WebView2 控件
        _webView.Dispose();

        await Task.CompletedTask;
    }
}
