using Acme.Product.Application.Services;
using Acme.Product.Desktop.Endpoints;
using Acme.Product.Desktop.Handlers;
using Acme.Product.Desktop.Middleware;
using Acme.Product.Infrastructure.AI;
using Acme.Product.Infrastructure.Logging;
using Acme.Product.Infrastructure.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using System.Data;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Acme.Product.Desktop;

static class Program
{
    private const int MinWebPort = 5000;
    private const int MaxWebPort = 5010;
    private static IHost? _host;
    private static int _webPort = 0;

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool SetDllDirectory(string lpPathName);

    public static IServiceProvider? ServiceProvider => _host?.Services;

    [STAThread]
    static void Main()
    {
        AppDomain.CurrentDomain.AssemblyResolve += (sender, args) =>
        {
            var assemblyName = new AssemblyName(args.Name).Name;
            var candidatePath = Path.Combine(AppContext.BaseDirectory, assemblyName + ".dll");
            if (File.Exists(candidatePath))
            {
                try
                {
                    return Assembly.LoadFrom(candidatePath);
                }
                catch
                {
                    // Fall back to default resolution.
                }
            }

            return null;
        };

        SetDllDirectory(AppContext.BaseDirectory);

        try
        {
            System.Windows.Forms.Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            System.Windows.Forms.Application.ThreadException += (s, e) =>
            {
                MessageBox.Show(
                    $"UI线程异常:\n{e.Exception.Message}\n\n{e.Exception.StackTrace}",
                    "错误",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            };
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                var ex = e.ExceptionObject as Exception;
                MessageBox.Show(
                    $"未处理异常:\n{ex?.Message}\n\n{ex?.StackTrace}",
                    "严重错误",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            };

            StartWebServer();

            System.Windows.Forms.Application.EnableVisualStyles();
            System.Windows.Forms.Application.SetCompatibleTextRenderingDefault(false);
            System.Windows.Forms.Application.SetHighDpiMode(HighDpiMode.SystemAware);

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Console()
                .WriteTo.File("logs/log.txt", rollingInterval: RollingInterval.Day)
                .CreateLogger();

            var mainForm = new MainForm();
            System.Windows.Forms.Application.Run(mainForm);

            StopWebServer().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Application Error: {ex}");
        }
    }

    static void StartWebServer()
    {
        try
        {
            _webPort = FindAvailablePort(MinWebPort, MaxWebPort);

            var builder = WebApplication.CreateBuilder();

            builder.Services.AddVisionServices(builder.Configuration);
            builder.Services.AddAiFlowGeneration(builder.Configuration);
            builder.Services.AddSingleton<WebMessageHandler>();
            builder.Services.AddSingleton<Acme.Product.Core.Interfaces.IProjectFlowStorage, JsonFileProjectFlowStorage>();
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

                var cameraManager = services.GetRequiredService<Acme.Product.Core.Cameras.ICameraManager>();
                var configService = services.GetRequiredService<Acme.Product.Core.Interfaces.IConfigurationService>();
                var config = configService.LoadAsync().Result;
                cameraManager.LoadBindings(config.Cameras, config.ActiveCameraId);
            }

            var wwwrootPath = GetWwwRootPath();
            if (Directory.Exists(wwwrootPath))
            {
                var provider = new PhysicalFileProvider(wwwrootPath);
                app.UseDefaultFiles(new DefaultFilesOptions
                {
                    FileProvider = provider,
                    DefaultFileNames = new List<string> { "index.html" }
                });
                app.UseStaticFiles(new StaticFileOptions { FileProvider = provider });
            }

            app.UseCors();
            app.UseMiddleware<AuthMiddleware>();

            app.MapGet("/health", () => Results.Ok(new { Status = "Healthy", Port = _webPort }));
            app.MapAuthEndpoints();
            app.MapUserEndpoints();
            app.MapVisionApiEndpoints();
            app.MapSettingsEndpoints();
            app.MapPlcEndpoints();

            RegisterExtendedApiEndpoints(app);
            app.MapAutoTuneEndpoints();
            app.MapInspectionEventEndpoints();

            // Start Kestrel before loading the desktop UI so WebView2 does not
            // race the embedded backend on slower industrial PCs.
            app.StartAsync().GetAwaiter().GetResult();
            _host = app;

            Debug.WriteLine($"Web服务器已启动: http://localhost:{_webPort}");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"启动Web服务器失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static void RegisterExtendedApiEndpoints(WebApplication app)
    {
        app.MapPost("/api/demo/create", async (DemoProjectService demoService) =>
        {
            try
            {
                var project = await demoService.CreateDemoProjectAsync();
                return Results.Ok(project);
            }
            catch (Exception ex)
            {
                return Results.Problem($"创建演示工程失败: {ex.Message}");
            }
        });

        app.MapPost("/api/demo/create-simple", async (DemoProjectService demoService) =>
        {
            try
            {
                var project = await demoService.CreateSimpleDemoProjectAsync();
                return Results.Ok(project);
            }
            catch (Exception ex)
            {
                return Results.Problem($"创建简单演示工程失败: {ex.Message}");
            }
        });

        app.MapGet("/api/demo/guide", (DemoProjectService demoService) =>
        {
            return Results.Ok(demoService.GetDemoGuide());
        });

        app.MapGet("/api/analysis/statistics/{projectId}", async (
            Guid projectId,
            DateTime? startTime,
            DateTime? endTime,
            string? status,
            string? defectType,
            Acme.Product.Application.Services.IResultAnalysisService analysisService) =>
        {
            return Results.Ok(await analysisService.GetStatisticsAsync(projectId, startTime, endTime, status, defectType));
        });

        app.MapGet("/api/analysis/defect-distribution/{projectId}", async (
            Guid projectId,
            DateTime? startTime,
            DateTime? endTime,
            string? status,
            string? defectType,
            Acme.Product.Application.Services.IResultAnalysisService analysisService) =>
        {
            return Results.Ok(await analysisService.GetDefectDistributionAsync(projectId, startTime, endTime, status, defectType));
        });

        app.MapGet("/api/analysis/trend/{projectId}", async (
            Guid projectId,
            string interval,
            DateTime startTime,
            DateTime endTime,
            string? status,
            string? defectType,
            Acme.Product.Application.Services.IResultAnalysisService analysisService) =>
        {
            if (!Enum.TryParse<Acme.Product.Application.Services.TrendInterval>(interval, true, out var trendInterval))
            {
                return Results.BadRequest($"无效间隔: {interval}");
            }

            return Results.Ok(await analysisService.GetTrendAnalysisAsync(projectId, trendInterval, startTime, endTime, status, defectType));
        });

        app.MapGet("/api/analysis/report/{projectId}", async (
            Guid projectId,
            DateTime? startTime,
            DateTime? endTime,
            string? status,
            string? defectType,
            Acme.Product.Application.Services.IResultAnalysisService analysisService) =>
        {
            return Results.Ok(await analysisService.GenerateReportAsync(projectId, startTime, endTime, status, defectType));
        });
    }

    static async Task StopWebServer()
    {
        if (_host != null)
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            await _host.StopAsync(cts.Token);
            _host.Dispose();
        }
    }

    static int FindAvailablePort(int startPort, int endPort)
    {
        for (int port = startPort; port <= endPort; port++)
        {
            if (IsPortAvailable(port))
            {
                return port;
            }
        }

        throw new Exception("未找到可用端口");
    }

    static bool IsPortAvailable(int port)
    {
        try
        {
            using var client = new System.Net.Sockets.TcpClient();
            client.Connect("127.0.0.1", port);
            return false;
        }
        catch
        {
            return true;
        }
    }

    static string GetWwwRootPath()
    {
        var basePath = AppContext.BaseDirectory;
        var devPath = Path.Combine(basePath, "..", "..", "..", "wwwroot");
        if (Directory.Exists(devPath))
        {
            return Path.GetFullPath(devPath);
        }

        return Path.GetFullPath(Path.Combine(basePath, "wwwroot"));
    }

    public static int GetWebPort() => _webPort;

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
