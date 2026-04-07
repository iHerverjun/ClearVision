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
        Assert.NotNull(defaultModel.Reasoning);
        Assert.Equal(AiReasoningModes.Auto, defaultModel.Reasoning!.Mode);
        Assert.Equal(AiReasoningEfforts.Medium, defaultModel.Reasoning.Effort);
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
        Assert.NotNull(migrated.Reasoning);
        Assert.Equal(AiReasoningModes.Auto, migrated.Reasoning!.Mode);
        Assert.Equal(AiReasoningEfforts.Medium, migrated.Reasoning.Effort);
    }

    [Fact]
    public void Update_WithReasoningSettings_PersistsNormalizedReasoning()
    {
        var store = CreateStore();
        store.Add(new AiModelConfig
        {
            Id = "reasoning-model",
            Name = "Reasoning Model",
            Provider = "OpenAI Compatible",
            Model = "gpt-5.4"
        });

        var updated = store.Update("reasoning-model", new AiModelConfig
        {
            Name = "Reasoning Model",
            Provider = "OpenAI Compatible",
            Model = "gpt-5.4",
            Reasoning = new AiReasoningSettings
            {
                Mode = "ON",
                Effort = "HIGH"
            }
        });

        Assert.NotNull(updated);
        Assert.NotNull(updated!.Reasoning);
        Assert.Equal(AiReasoningModes.On, updated.Reasoning!.Mode);
        Assert.Equal(AiReasoningEfforts.High, updated.Reasoning.Effort);
    }

    [Fact]
    public void Update_WhenLockedThinkingModelIsTurnedOff_ShouldThrow()
    {
        var store = CreateStore();
        store.Add(new AiModelConfig
        {
            Id = "deepseek-reasoner",
            Name = "DeepSeek Reasoner",
            Provider = "OpenAI Compatible",
            Model = "deepseek-reasoner"
        });

        var ex = Assert.Throws<InvalidOperationException>(() => store.Update("deepseek-reasoner", new AiModelConfig
        {
            Name = "DeepSeek Reasoner",
            Provider = "OpenAI Compatible",
            Model = "deepseek-reasoner",
            Reasoning = new AiReasoningSettings
            {
                Mode = AiReasoningModes.Off,
                Effort = AiReasoningEfforts.Medium
            }
        }));
        Assert.Contains("不支持关闭 reasoning / thinking", ex.Message);
    }

    [Fact]
    public void Update_WhenValidationFails_ShouldNotMutateStoredModel()
    {
        var store = CreateStore();
        store.Add(new AiModelConfig
        {
            Id = "gpt-5-legacy",
            Name = "GPT 5 Legacy",
            Provider = "OpenAI Compatible",
            Model = "gpt-5.4",
            Reasoning = new AiReasoningSettings
            {
                Mode = AiReasoningModes.On,
                Effort = AiReasoningEfforts.High
            }
        });

        var ex = Assert.Throws<InvalidOperationException>(() => store.Update("gpt-5-legacy", new AiModelConfig
        {
            Name = "GPT 5 Legacy",
            Provider = "OpenAI Compatible",
            Model = "gpt-5.4",
            Reasoning = new AiReasoningSettings
            {
                Mode = AiReasoningModes.Off,
                Effort = AiReasoningEfforts.Medium
            }
        }));

        Assert.Contains("不支持关闭 reasoning / thinking", ex.Message);
        var persisted = store.GetById("gpt-5-legacy");
        Assert.NotNull(persisted);
        Assert.Equal(AiReasoningModes.On, persisted!.Reasoning!.Mode);
        Assert.Equal(AiReasoningEfforts.High, persisted.Reasoning!.Effort);
    }

    [Fact]
    public void Update_WhenGpt51ReasoningIsTurnedOff_ShouldAllowNoneMode()
    {
        var store = CreateStore();
        store.Add(new AiModelConfig
        {
            Id = "gpt-5-1",
            Name = "GPT 5.1",
            Provider = "OpenAI Compatible",
            Model = "gpt-5.1"
        });

        var updated = store.Update("gpt-5-1", new AiModelConfig
        {
            Name = "GPT 5.1",
            Provider = "OpenAI Compatible",
            Model = "gpt-5.1",
            Reasoning = new AiReasoningSettings
            {
                Mode = AiReasoningModes.Off,
                Effort = AiReasoningEfforts.Medium
            }
        });

        Assert.NotNull(updated);
        Assert.Equal(AiReasoningModes.Off, updated!.Reasoning!.Mode);
    }

    [Fact]
    public void Update_WhenGpt5ProUsesNonHighEffort_ShouldThrow()
    {
        var store = CreateStore();
        store.Add(new AiModelConfig
        {
            Id = "gpt-5-pro",
            Name = "GPT 5 Pro",
            Provider = "OpenAI Compatible",
            Model = "gpt-5-pro",
            Reasoning = new AiReasoningSettings
            {
                Mode = AiReasoningModes.On,
                Effort = AiReasoningEfforts.High
            }
        });

        var ex = Assert.Throws<InvalidOperationException>(() => store.Update("gpt-5-pro", new AiModelConfig
        {
            Name = "GPT 5 Pro",
            Provider = "OpenAI Compatible",
            Model = "gpt-5-pro",
            Reasoning = new AiReasoningSettings
            {
                Mode = AiReasoningModes.On,
                Effort = AiReasoningEfforts.Medium
            }
        }));

        Assert.Contains("仅支持 High 思考强度", ex.Message);
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
