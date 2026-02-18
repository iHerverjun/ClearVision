// MainForm.cs
// 初始化菜单栏
// 作者：蘅芜君

using System.Windows.Forms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Web.WebView2.WinForms;

namespace Acme.Product.Desktop;

/// <summary>
/// 主窗体，集成 WebView2 控件。
/// </summary>
public partial class MainForm : Form
{
    private readonly WebView2 _webView;
    private readonly WebView2Host _webView2Host;

    public MainForm()
    {
        InitializeComponent();

        // 创建 WebView2 控件
        _webView = new WebView2
        {
            Dock = DockStyle.Fill
        };
        Controls.Add(_webView);

        // 创建 WebView2 宿主
        var messageHandler = Program.ServiceProvider?.GetService<Handlers.WebMessageHandler>();
        _webView2Host = new WebView2Host(_webView, messageHandler);

        // 窗体加载时初始化 WebView2
        Load += MainForm_Load;
        FormClosing += MainForm_FormClosing;
    }

    private async void MainForm_Load(object? sender, EventArgs e)
    {
        try
        {
            // 初始化菜单栏（在WebView2初始化之前）
            InitializeMenu();

            await _webView2Host.InitializeAsync();

            // S4-006: 初始化 WebMessage 处理器，挂载到 WebView2
            if (_webView.CoreWebView2 != null)
            {
                var handler = Program.ServiceProvider?.GetService<Handlers.WebMessageHandler>();
                handler?.Initialize(_webView);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"WebView2 初始化失败: {ex.Message}",
                "错误",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    /// <summary>
    /// 初始化菜单栏
    /// </summary>
    private void InitializeMenu()
    {
        var menuStrip = new MenuStrip();

        // 视图菜单
        var viewMenu = new ToolStripMenuItem("视图");

        // 【科学方案三】强制刷新菜单项
        var refreshMenuItem = new ToolStripMenuItem("强制刷新 (清除缓存)", null, async (s, e) =>
        {
            try
            {
                await _webView2Host.ForceReloadAsync();
                MessageBox.Show("缓存已清除，页面已刷新", "完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"刷新失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        });
        refreshMenuItem.ShortcutKeys = Keys.F5 | Keys.Control;
        refreshMenuItem.ToolTipText = "Ctrl+F5 强制刷新";

        viewMenu.DropDownItems.Add(refreshMenuItem);

        // 开发工具菜单项（仅DEBUG模式）
#if DEBUG
        viewMenu.DropDownItems.Add(new ToolStripSeparator());

        var devToolsMenuItem = new ToolStripMenuItem("开发者工具", null, (s, e) =>
        {
            _webView.CoreWebView2?.OpenDevToolsWindow();
        });
        devToolsMenuItem.ShortcutKeys = Keys.F12;
        viewMenu.DropDownItems.Add(devToolsMenuItem);

        // 清除缓存菜单项
        var clearCacheMenuItem = new ToolStripMenuItem("清除缓存", null, async (s, e) =>
        {
            try
            {
                await _webView2Host.ClearCacheAsync();
                MessageBox.Show("缓存已清除", "完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"清除缓存失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        });
        viewMenu.DropDownItems.Add(clearCacheMenuItem);
#endif

        menuStrip.Items.Add(viewMenu);

        // 将菜单栏添加到窗体
        Controls.Add(menuStrip);
        MainMenuStrip = menuStrip;
    }

    private async void MainForm_FormClosing(object? sender, FormClosingEventArgs e)
    {
        await _webView2Host.DisposeAsync();
    }

    private void InitializeComponent()
    {
        SuspendLayout();
        //
        // MainForm
        //
        AutoScaleDimensions = new SizeF(7F, 17F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(1280, 720);
        Name = "MainForm";
        StartPosition = FormStartPosition.CenterScreen;
        Text = "Acme Product";
        ResumeLayout(false);
    }
}
