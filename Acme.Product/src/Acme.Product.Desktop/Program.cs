// Program.cs
// 应用程序入口
// 作者：蘅芜君

using Acme.Product.Application.Services;
using Acme.Product.Desktop.Handlers;
using Acme.Product.Infrastructure.Logging;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Acme.Product.Desktop.Endpoints;
using Acme.Product.Desktop.Middleware;
using Acme.Product.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Acme.Product.Core.Enums;
using Acme.Product.Infrastructure.AI;
using System.Data;

namespace Acme.Product.Desktop;

static class Program
{
    private const int MinWebPort = 5000;
    private const int MaxWebPort = 5010;
    private const string InitialAdminUsername = "admin";
    private const string InitialAdminPasswordEnvVar = "CLEARVISION_INITIAL_ADMIN_PASSWORD";
    private static IHost? _host;
    private static int _webPort = 0;

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool SetDllDirectory(string lpPathName);

    /// <summary>
    /// 获取服务提供者
    /// </summary>
    public static IServiceProvider? ServiceProvider => _host?.Services;

    /// <summary>
    /// 应用程序的主入口点。
    /// </summary>
    [STAThread]
    static void Main()
    {
        // 【最优先】注册程序集解析器，确保华睿 SDK 的托管依赖能从应用目录加载
        // MVSDK_Net.dll (.NET Framework 4.0) 内部引用 CLIDelegate.dll，
        // 但 .NET 8 的 deps.json 探测不知道它们的存在，需要手动解析
        AppDomain.CurrentDomain.AssemblyResolve += (sender, args) =>
        {
            var assemblyName = new System.Reflection.AssemblyName(args.Name).Name;
            var candidatePath = Path.Combine(AppContext.BaseDirectory, assemblyName + ".dll");
            if (File.Exists(candidatePath))
            {
                try
                { return Assembly.LoadFrom(candidatePath); }
                catch { /* 加载失败则回退到默认解析 */ }
            }
            return null;
        };

        // 设置原生 DLL 搜索路径，确保华睿 SDK 的 MVSDKmd.dll 等可被找到
        SetDllDirectory(AppContext.BaseDirectory);

        try
        {
            // 添加全局异常处理
            System.Windows.Forms.Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            System.Windows.Forms.Application.ThreadException += (s, e) =>
            {
                MessageBox.Show($"UI线程异常:\n{e.Exception.Message}\n\n{e.Exception.StackTrace}",
                    "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            };
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                var ex = e.ExceptionObject as Exception;
                MessageBox.Show($"未处理异常:\n{ex?.Message}\n\n{ex?.StackTrace}",
                    "严重错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            };

            // 启动本地 Web 服务器
            StartWebServer();

            // 配置应用程序
            System.Windows.Forms.Application.EnableVisualStyles();
            System.Windows.Forms.Application.SetCompatibleTextRenderingDefault(false);
            System.Windows.Forms.Application.SetHighDpiMode(HighDpiMode.SystemAware);

            // 配置 Serilog
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Console()
                .WriteTo.File("logs/log.txt", rollingInterval: RollingInterval.Day)
                .CreateLogger();

            // 启动主窗体
            var mainForm = new MainForm();
            System.Windows.Forms.Application.Run(mainForm);

            // 关闭 Web 服务器
            StopWebServer().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Application Error: {ex}");
        }
    }

    /// <summary>
    /// 启动本地 Web 服务器
    /// </summary>
    static void StartWebServer()
    {
        try
        {
            _webPort = FindAvailablePort(MinWebPort, MaxWebPort);

            var builder = WebApplication.CreateBuilder();

            // 配置服务
            builder.Services.AddVisionServices();
            builder.Services.AddAiFlowGeneration(builder.Configuration);
            builder.Services.AddSingleton<WebMessageHandler>();
            builder.Services.AddSingleton<Acme.Product.Core.Interfaces.IProjectFlowStorage, Acme.Product.Infrastructure.Services.JsonFileProjectFlowStorage>();

            // 注册手眼标定服务
            builder.Services.AddTransient<IPlanarScaleOffsetCalibrationService, PlanarScaleOffsetCalibrationService>();

            builder.Services.ConfigureHttpJsonOptions(options =>
            {
                options.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
                options.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter(allowIntegerValues: true));
            });

            builder.Services.AddCors(options =>
            {
                options.AddDefaultPolicy(policy =>
                {
                    policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
                });
            });

            builder.WebHost.ConfigureKestrel(options =>
            {
                options.ListenLocalhost(_webPort);
            });

            var app = builder.Build();

            // 初始化数据库和加载相机配置
            using (var scope = app.Services.CreateScope())
            {
                var services = scope.ServiceProvider;
                var dbContext = services.GetRequiredService<Acme.Product.Infrastructure.Data.VisionDbContext>();
                if (dbContext.Database.GetMigrations().Any())
                {
                    dbContext.Database.Migrate();
                }
                else
                {
                    dbContext.Database.EnsureCreated();
                }

                if (dbContext.Database.IsSqlite())
                {
                    dbContext.Database.ExecuteSqlRaw("PRAGMA journal_mode=WAL;");
                    dbContext.Database.ExecuteSqlRaw("PRAGMA synchronous=NORMAL;");
                }

                EnsureInspectionResultAnalysisDataColumnAsync(dbContext).GetAwaiter().GetResult();

                // 初始化相机管理器绑定
                var cameraManager = services.GetRequiredService<Acme.Product.Core.Cameras.ICameraManager>();
                var configService = services.GetRequiredService<Acme.Product.Core.Interfaces.IConfigurationService>();
                var config = configService.LoadAsync().Result;
                cameraManager.LoadBindings(config.Cameras, config.ActiveCameraId);

                // 初始化默认管理员
                InitializeDefaultAdminAsync(services).Wait();
            }

            // 配置中间件
            var wwwrootPath = GetWwwRootPath();
            if (Directory.Exists(wwwrootPath))
            {
                var provider = new PhysicalFileProvider(wwwrootPath);
                app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = provider, DefaultFileNames = new List<string> { "index.html" } });
                app.UseStaticFiles(new StaticFileOptions { FileProvider = provider });
            }

            app.UseCors();
            app.UseMiddleware<AuthMiddleware>();

            // 注册端点
            app.MapGet("/health", () => Results.Ok(new { Status = "Healthy", Port = _webPort }));
            app.MapAuthEndpoints();
            app.MapUserEndpoints();
            app.MapVisionApiEndpoints();
            app.MapSettingsEndpoints();
            app.MapPlcEndpoints();

            // 结果分析和演示工程 API
            RegisterExtendedApiEndpoints(app);

            // 【Phase 4】LLM 闭环验证 - 自动调参端点
            app.MapAutoTuneEndpoints();

            // 【架构修复 v2】检测事件 SSE 端点
            app.MapInspectionEventEndpoints();

            _host = app;

            Task.Run(async () =>
            {
                try
                { await app.RunAsync(); }
                catch (Exception ex) { Debug.WriteLine($"Web服务器错误: {ex.Message}"); }
            });

            Debug.WriteLine($"Web服务器已启动: http://localhost:{_webPort}");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"启动Web服务器失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    /// <summary>
    /// 注册额外的 API 端点（演示工程、分析等）
    /// </summary>
    private static void RegisterExtendedApiEndpoints(WebApplication app)
    {
        // 演示工程接口
        app.MapPost("/api/demo/create", async (DemoProjectService demoService) =>
        {
            try
            { var project = await demoService.CreateDemoProjectAsync(); return Results.Ok(project); }
            catch (Exception ex) { return Results.Problem($"创建演示工程失败: {ex.Message}"); }
        });

        app.MapPost("/api/demo/create-simple", async (DemoProjectService demoService) =>
        {
            try
            { var project = await demoService.CreateSimpleDemoProjectAsync(); return Results.Ok(project); }
            catch (Exception ex) { return Results.Problem($"创建简单演示工程失败: {ex.Message}"); }
        });

        app.MapGet("/api/demo/guide", (DemoProjectService demoService) =>
        {
            return Results.Ok(demoService.GetDemoGuide());
        });

        // 结果分析 API
        app.MapGet("/api/analysis/statistics/{projectId}", async (Guid projectId, DateTime? startTime, DateTime? endTime, string? status, string? defectType, Acme.Product.Application.Services.IResultAnalysisService analysisService) =>
        {
            return Results.Ok(await analysisService.GetStatisticsAsync(projectId, startTime, endTime, status, defectType));
        });

        app.MapGet("/api/analysis/defect-distribution/{projectId}", async (Guid projectId, DateTime? startTime, DateTime? endTime, string? status, string? defectType, Acme.Product.Application.Services.IResultAnalysisService analysisService) =>
        {
            return Results.Ok(await analysisService.GetDefectDistributionAsync(projectId, startTime, endTime, status, defectType));
        });

        app.MapGet("/api/analysis/trend/{projectId}", async (Guid projectId, string interval, DateTime startTime, DateTime endTime, string? status, string? defectType, Acme.Product.Application.Services.IResultAnalysisService analysisService) =>
        {
            if (!Enum.TryParse<Acme.Product.Application.Services.TrendInterval>(interval, true, out var trendInterval))
                return Results.BadRequest($"无效间隔: {interval}");
            return Results.Ok(await analysisService.GetTrendAnalysisAsync(projectId, trendInterval, startTime, endTime, status, defectType));
        });

        app.MapGet("/api/analysis/report/{projectId}", async (Guid projectId, DateTime? startTime, DateTime? endTime, string? status, string? defectType, Acme.Product.Application.Services.IResultAnalysisService analysisService) =>
        {
            return Results.Ok(await analysisService.GenerateReportAsync(projectId, startTime, endTime, status, defectType));
        });
    }

    /// <summary>
    /// 停止 Web 服务器
    /// 【架构修复 v2】配置 60 秒关机超时，确保 Worker 优雅关机
    /// </summary>
    static async Task StopWebServer()
    {
        if (_host != null)
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            await _host.StopAsync(cts.Token);
            _host.Dispose();
        }
    }

    /// <summary>
    /// 查找可用端口
    /// </summary>
    static int FindAvailablePort(int startPort, int endPort)
    {
        for (int port = startPort; port <= endPort; port++)
        {
            if (IsPortAvailable(port))
                return port;
        }
        throw new Exception("未找到可用端口");
    }

    /// <summary>
    /// 检查端口是否可用
    /// </summary>
    static bool IsPortAvailable(int port)
    {
        try
        {
            using var client = new System.Net.Sockets.TcpClient();
            client.Connect("127.0.0.1", port);
            return false;
        }
        catch { return true; }
    }

    /// <summary>
    /// 获取 wwwroot 路径
    /// </summary>
    static string GetWwwRootPath()
    {
        var basePath = AppContext.BaseDirectory;
        var devPath = Path.Combine(basePath, "..", "..", "..", "wwwroot");
        if (Directory.Exists(devPath))
            return Path.GetFullPath(devPath);
        return Path.GetFullPath(Path.Combine(basePath, "wwwroot"));
    }

    /// <summary>
    /// 获取 Web 服务器端口
    /// </summary>
    public static int GetWebPort() => _webPort;

    /// <summary>
    /// 初始化默认管理员账户
    /// </summary>
    static async Task InitializeDefaultAdminAsync(IServiceProvider serviceProvider)
    {
        try
        {
            var userRepository = serviceProvider.GetRequiredService<Acme.Product.Core.Interfaces.IUserRepository>();
            var passwordHasher = serviceProvider.GetRequiredService<Acme.Product.Application.Services.IPasswordHasher>();

            var existingAdmin = await userRepository.GetByUsernameAsync(InitialAdminUsername);
            if (existingAdmin == null)
            {
                var initialPassword = ResolveInitialAdminPassword();
                var adminUser = Acme.Product.Core.Entities.User.Create(
                    InitialAdminUsername,
                    passwordHasher.HashPassword(initialPassword),
                    "系统管理员",
                    UserRole.Admin);
                await userRepository.AddAsync(adminUser);
                NotifyInitialAdminPassword(initialPassword);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"初始化管理员失败: {ex.Message}");
        }
    }

    private static string ResolveInitialAdminPassword()
    {
        var configuredPassword = Environment.GetEnvironmentVariable(InitialAdminPasswordEnvVar);
        if (!string.IsNullOrWhiteSpace(configuredPassword))
        {
            return configuredPassword.Trim();
        }

        return RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
    }

    private static void NotifyInitialAdminPassword(string password)
    {
        Debug.WriteLine($"[Security] Generated initial admin password for '{InitialAdminUsername}'. Change it after first login.");

        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(InitialAdminPasswordEnvVar)))
        {
            return;
        }

        if (!Environment.UserInteractive)
        {
            return;
        }

        MessageBox.Show(
            $"已创建初始管理员账户:\n用户名: {InitialAdminUsername}\n临时密码: {password}\n\n请在首次登录后立即修改密码。",
            "初始管理员密码",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private static async Task EnsureInspectionResultAnalysisDataColumnAsync(Acme.Product.Infrastructure.Data.VisionDbContext dbContext)
    {
        await EnsureTextColumnExistsAsync(dbContext, "InspectionResults", "AnalysisDataJson");
    }

    private static async Task EnsureTextColumnExistsAsync(
        Acme.Product.Infrastructure.Data.VisionDbContext dbContext,
        string tableName,
        string columnName)
    {
        var connection = dbContext.Database.GetDbConnection();
        var shouldCloseConnection = connection.State != ConnectionState.Open;

        if (shouldCloseConnection)
        {
            await connection.OpenAsync();
        }

        try
        {
            await using var pragmaCommand = connection.CreateCommand();
            pragmaCommand.CommandText = $"PRAGMA table_info(\"{tableName}\");";

            await using var reader = await pragmaCommand.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var existingColumn = reader["name"]?.ToString();
                if (string.Equals(existingColumn, columnName, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }

            await reader.CloseAsync();
            var alterSql = $"ALTER TABLE \"{tableName}\" ADD COLUMN \"{columnName}\" TEXT NULL;";
            await dbContext.Database.ExecuteSqlRawAsync(alterSql);
        }
        finally
        {
            if (shouldCloseConnection)
            {
                await connection.CloseAsync();
            }
        }
    }
}
