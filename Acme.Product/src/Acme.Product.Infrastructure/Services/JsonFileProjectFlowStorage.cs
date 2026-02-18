// JsonFileProjectFlowStorage.cs
// JsonFileProjectFlowStorage实现
// 作者：蘅芜君

using Acme.Product.Core.Interfaces;

namespace Acme.Product.Infrastructure.Services;

public class JsonFileProjectFlowStorage : IProjectFlowStorage
{
    private readonly string _basePath;

    public JsonFileProjectFlowStorage()
    {
        // 存储在 App_Data/ProjectFlows 目录下
        _basePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "App_Data", "ProjectFlows");
        if (!Directory.Exists(_basePath))
        {
            Directory.CreateDirectory(_basePath);
        }
    }

    public async Task SaveFlowJsonAsync(Guid projectId, string flowJson)
    {
        var filePath = GetFilePath(projectId);
        await File.WriteAllTextAsync(filePath, flowJson);
    }

    public async Task<string?> LoadFlowJsonAsync(Guid projectId)
    {
        var filePath = GetFilePath(projectId);
        if (!File.Exists(filePath))
        {
            return null;
        }

        return await File.ReadAllTextAsync(filePath);
    }

    private string GetFilePath(Guid projectId)
    {
        return Path.Combine(_basePath, $"{projectId}.json");
    }
}
