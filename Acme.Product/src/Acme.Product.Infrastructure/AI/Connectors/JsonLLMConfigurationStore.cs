using System.Text.Json;

namespace Acme.Product.Infrastructure.AI.Connectors;

public class JsonLLMConfigurationStore : ILLMConfigurationStore
{
    private readonly string _filePath;
    private readonly object _lock = new();

    public JsonLLMConfigurationStore()
    {
        var appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ClearVision");
        if (!Directory.Exists(appData))
            Directory.CreateDirectory(appData);
        _filePath = Path.Combine(appData, "llm_profiles.json");
    }

    private LLMConfigurationList LoadAllProfiles()
    {
        lock (_lock)
        {
            if (!File.Exists(_filePath))
            {
                return new LLMConfigurationList();
            }

            try
            {
                var json = File.ReadAllText(_filePath);
                return JsonSerializer.Deserialize<LLMConfigurationList>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new LLMConfigurationList();
            }
            catch
            {
                return new LLMConfigurationList();
            }
        }
    }

    private void SaveAllProfiles(LLMConfigurationList list)
    {
        lock (_lock)
        {
            var json = JsonSerializer.Serialize(list, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_filePath, json);
        }
    }

    public Task<LLMConfiguration?> LoadAsync(string profileName)
    {
        var list = LoadAllProfiles();
        var profile = list.Profiles.FirstOrDefault(p => p.ProfileName == profileName);
        return Task.FromResult(profile);
    }

    public Task<LLMConfiguration?> GetActiveProfileAsync()
    {
        var list = LoadAllProfiles();
        if (string.IsNullOrEmpty(list.ActiveProfile))
        {
            var profile = list.Profiles.Where(p => p.IsEnabled).OrderBy(p => p.Priority).FirstOrDefault();
            return Task.FromResult(profile);
        }
        else
        {
            var profile = list.Profiles.FirstOrDefault(p => p.ProfileName == list.ActiveProfile && p.IsEnabled);
            return Task.FromResult(profile);
        }
    }

    public Task SetActiveProfileAsync(string profileName)
    {
        var list = LoadAllProfiles();
        list.ActiveProfile = profileName;
        SaveAllProfiles(list);
        return Task.CompletedTask;
    }

    public Task SaveAsync(LLMConfiguration config)
    {
        var list = LoadAllProfiles();
        var existing = list.Profiles.FirstOrDefault(p => p.ProfileName == config.ProfileName);
        if (existing != null)
        {
            list.Profiles.Remove(existing);
        }
        list.Profiles.Add(config);

        if (list.Profiles.Count == 1 && string.IsNullOrEmpty(list.ActiveProfile))
        {
            list.ActiveProfile = config.ProfileName;
        }

        SaveAllProfiles(list);
        return Task.CompletedTask;
    }

    public Task<List<string>> ListProfilesAsync()
    {
        var list = LoadAllProfiles();
        return Task.FromResult(list.Profiles.Select(p => p.ProfileName).ToList());
    }
}
