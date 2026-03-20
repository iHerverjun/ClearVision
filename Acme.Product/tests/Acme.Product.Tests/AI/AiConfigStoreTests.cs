// AiConfigStoreTests.cs
// AiConfigStore 单元测试
// 作者：蘅芜君

using System;
using System.IO;
using System.Linq;
using Acme.Product.Infrastructure.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Acme.Product.Tests.AI;

public class AiConfigStoreTests : IDisposable
{
    private readonly string _testDir;
    private readonly string _testModelsFile;
    private readonly string _testLegacyFile;
    private readonly Microsoft.Extensions.Logging.ILogger<AiConfigStore> _mockLogger;
    private readonly IOptions<AiGenerationOptions> _mockOptions;

    public AiConfigStoreTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"cv-ai-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);
        _testModelsFile = Path.Combine(_testDir, "ai_models.json");
        _testLegacyFile = Path.Combine(_testDir, "ai_config.json");

        _mockLogger = Substitute.For<Microsoft.Extensions.Logging.ILogger<AiConfigStore>>();
        _mockOptions = Options.Create(new AiGenerationOptions
        {
            Provider = "TestProvider",
            ApiKey = "TestKey",
            Model = "TestModel",
            BaseUrl = "http://test",
            TimeoutSeconds = 60
        });

        CleanupFiles();
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testDir))
            {
                Directory.Delete(_testDir, true);
            }
        }
        catch
        {
            // ignore cleanup failures in tests
        }
    }

    private void CleanupFiles()
    {
        if (File.Exists(_testModelsFile))
            File.Delete(_testModelsFile);
        if (File.Exists(_testLegacyFile))
            File.Delete(_testLegacyFile);
    }

    private AiConfigStore CreateStore()
    {
        return new AiConfigStore(_mockOptions, _mockLogger, _testDir);
    }

    [Fact]
    public void Constructor_WhenNoFiles_CreatesDefaultFromOptions()
    {
        // Arrange & Act
        var store = CreateStore();
        var all = store.GetAll();

        // Assert
        Assert.Single(all);
        var defaultModel = all[0];
        Assert.Equal("系统默认模型", defaultModel.Name);
        Assert.Equal("TestProvider", defaultModel.Provider);
        Assert.Equal("TestKey", defaultModel.ApiKey);
        Assert.Equal("TestModel", defaultModel.Model);
        Assert.Equal("http://test", defaultModel.BaseUrl);
        Assert.Equal(60000, defaultModel.TimeoutMs);
        Assert.True(defaultModel.IsActive);
    }

    [Fact]
    public void Add_Model_Then_GetAll_ReturnsIt()
    {
        var store = CreateStore();
        store.GetAll(); // Ignore default

        var newModel = new AiModelConfig { Id = "test1", Name = "New Model" };
        store.Add(newModel);

        var all = store.GetAll();
        Assert.Equal(2, all.Count);
        Assert.Contains(all, m => m.Id == "test1");
    }

    [Fact]
    public void Delete_LastModel_ThrowsException()
    {
        var store = CreateStore();
        var defaultModel = store.GetAll().First();

        var ex = Assert.Throws<InvalidOperationException>(() => store.Delete(defaultModel.Id));
        Assert.Equal("至少需保留一个模型配置", ex.Message);
    }

    [Fact]
    public void Delete_ActiveModel_ActivatesFirstRemaining()
    {
        var store = CreateStore();
        var defaultModel = store.GetAll().First();

        var secondModel = new AiModelConfig { Id = "test2", IsActive = false };
        store.Add(secondModel);

        Assert.True(store.GetById(defaultModel.Id).IsActive);

        // Act
        store.Delete(defaultModel.Id);

        // Assert
        var remaining = store.GetAll();
        Assert.Single(remaining);
        Assert.Equal("test2", remaining[0].Id);
        Assert.True(remaining[0].IsActive);
    }

    [Fact]
    public void SetActive_UpdatesActiveFlag()
    {
        var store = CreateStore();
        var defaultModel = store.GetAll().First();

        var modelA = new AiModelConfig { Id = "A", IsActive = false };
        var modelB = new AiModelConfig { Id = "B", IsActive = false };
        store.Add(modelA);
        store.Add(modelB);

        // Act
        store.SetActive("B");

        // Assert
        Assert.False(store.GetById(defaultModel.Id).IsActive);
        Assert.False(store.GetById("A").IsActive);
        Assert.True(store.GetById("B").IsActive);
    }

    [Fact]
    public void Update_WithNullApiKey_PreservesOldKey()
    {
        var store = CreateStore();
        var model = new AiModelConfig { Id = "test3", ApiKey = "RealSecretKey" };
        store.Add(model);

        var updateReq = new AiModelConfig { ApiKey = null, Name = "Updated Name" };

        // Act
        var updated = store.Update("test3", updateReq);

        // Assert
        Assert.Equal("Updated Name", updated.Name);
        Assert.Equal("RealSecretKey", updated.ApiKey); // Old key preserved
    }

    [Fact]
    public void Update_WithEmptyApiKey_PreservesOldKey()
    {
        var store = CreateStore();
        var model = new AiModelConfig { Id = "test4", ApiKey = "RealSecretKey" };
        store.Add(model);

        var updateReq = new AiModelConfig { ApiKey = "", Name = "Updated Name" };

        // Act
        var updated = store.Update("test4", updateReq);

        // Assert
        Assert.Equal("RealSecretKey", updated.ApiKey); // Old key preserved
    }

    [Fact]
    public void Get_ReturnsActiveModelAsOptions()
    {
        var store = CreateStore();
        var modelA = new AiModelConfig { Id = "A", Provider = "ProviderA", Model = "ModelA", IsActive = false };
        var modelB = new AiModelConfig { Id = "B", Provider = "ProviderB", Model = "ModelB", IsActive = true };

        store.Add(modelA);
        store.Add(modelB);
        store.SetActive("B");

        var options = store.Get();
        Assert.Equal("ProviderB", options.Provider);
        Assert.Equal("ModelB", options.Model);
    }

    [Fact]
    public void Migration_FromOldSingleConfig()
    {
        // 模拟一个旧版的 ai_config.json
        var oldConfigJson = "{\"Provider\":\"LegacyProvider\",\"ApiKey\":\"LegacyKey\",\"Model\":\"LegacyModel\",\"BaseUrl\":\"http://legacy\",\"TimeoutSeconds\":120,\"MaxRetries\":3,\"MaxTokens\":2048,\"Temperature\":0.5}";
        File.WriteAllText(_testLegacyFile, oldConfigJson);

        var store = CreateStore();

        var all = store.GetAll();
        Assert.Single(all);
        var migrated = all[0];

        Assert.Equal("model_migrated", migrated.Id);
        Assert.Equal("LegacyProvider", migrated.Provider);
        Assert.Equal("LegacyKey", migrated.ApiKey);
        Assert.Equal("LegacyModel", migrated.Model);
        Assert.Equal("http://legacy", migrated.BaseUrl);
        Assert.Equal(120000, migrated.TimeoutMs);
        Assert.True(migrated.IsActive);
    }

    [Fact]
    public void ResetToDefaults_ShouldReplaceModelsAndDeleteLegacyFile()
    {
        File.WriteAllText(_testLegacyFile, "{\"Provider\":\"LegacyProvider\",\"ApiKey\":\"LegacyKey\",\"Model\":\"LegacyModel\",\"TimeoutSeconds\":30}");

        var store = CreateStore();
        store.Add(new AiModelConfig
        {
            Id = "custom-model",
            Name = "Custom Model",
            Provider = "custom-provider",
            ApiKey = "custom-key",
            Model = "custom-model-name"
        });
        store.SetActive("custom-model");

        var resetModels = store.ResetToDefaults();

        Assert.Single(resetModels);
        Assert.Single(store.GetAll());
        var defaultModel = store.GetAll().Single();
        Assert.Equal("model_default", defaultModel.Id);
        Assert.Equal("系统默认模型", defaultModel.Name);
        Assert.Equal("TestProvider", defaultModel.Provider);
        Assert.Equal("TestKey", defaultModel.ApiKey);
        Assert.Equal("TestModel", defaultModel.Model);
        Assert.True(defaultModel.IsActive);
        Assert.False(File.Exists(_testLegacyFile));
    }
}
