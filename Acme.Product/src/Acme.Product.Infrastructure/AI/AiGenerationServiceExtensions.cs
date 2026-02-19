using Acme.Product.Core.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Acme.Product.Infrastructure.AI;

public static class AiGenerationServiceExtensions
{
    public static IServiceCollection AddAiFlowGeneration(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // 注册 IOptions<AiGenerationOptions>（供 AiConfigStore 初始化时读取 appsettings.json 默认值）
        services.Configure<AiGenerationOptions>(
            configuration.GetSection(AiGenerationOptions.SectionName));

        // 注册运行时配置管理器（单例：启动时从 ai_config.json 加载，不存在则从 appsettings.json 迁移）
        services.AddSingleton<AiConfigStore>();

        // 注册 HttpClient
        services.AddHttpClient<AiApiClient>(client =>
        {
            // 给 AI 生成留出充足的响应时间（模型生成工作流较慢）
            client.Timeout = TimeSpan.FromSeconds(120);
        });

        // 注册核心组件
        services.AddScoped<PromptBuilder>();
        services.AddScoped<IAiFlowValidator, AiFlowValidator>();
        services.AddScoped<AutoLayoutService>();
        services.AddScoped<IAiFlowGenerationService, AiFlowGenerationService>();
        services.AddScoped<GenerateFlowMessageHandler>();

        return services;
    }
}
