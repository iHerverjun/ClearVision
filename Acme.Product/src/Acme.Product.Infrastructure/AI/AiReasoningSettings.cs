using System.Text.Json.Serialization;

namespace Acme.Product.Infrastructure.AI;

public static class AiReasoningModes
{
    public const string Auto = "auto";
    public const string Off = "off";
    public const string On = "on";

    public static string Normalize(string? mode)
    {
        var normalized = mode?.Trim().ToLowerInvariant();
        return normalized switch
        {
            Off => Off,
            On => On,
            _ => Auto
        };
    }
}

public static class AiReasoningEfforts
{
    public const string Low = "low";
    public const string Medium = "medium";
    public const string High = "high";

    public static string Normalize(string? effort)
    {
        var normalized = effort?.Trim().ToLowerInvariant();
        return normalized switch
        {
            Low => Low,
            High => High,
            _ => Medium
        };
    }
}

public sealed class AiReasoningSettings
{
    public string Mode { get; set; } = AiReasoningModes.Auto;

    public string Effort { get; set; } = AiReasoningEfforts.Medium;

    public AiReasoningSettings Clone()
    {
        return new AiReasoningSettings
        {
            Mode = Mode,
            Effort = Effort
        };
    }

    public AiReasoningSettings Normalize()
    {
        Mode = AiReasoningModes.Normalize(Mode);
        Effort = AiReasoningEfforts.Normalize(Effort);
        return this;
    }
}

public sealed class AiReasoningSupportInfo
{
    public string FamilyId { get; init; } = AiReasoningModelFamilyCatalog.FamilyUnknown;

    public string FamilyName { get; init; } = "Unknown";

    public string[] AllowedModes { get; init; } = [AiReasoningModes.Auto];

    public string[] AllowedEfforts { get; init; } = [AiReasoningEfforts.Medium];

    public string HelpText { get; init; } = "Keep reasoning in Auto for unrecognized model families.";

    public bool SupportsExplicitMode => AllowedModes.Any(mode => mode is AiReasoningModes.On or AiReasoningModes.Off);

    public bool SupportsEffort => AllowedEfforts.Length > 1 || AllowedEfforts[0] != AiReasoningEfforts.Medium;

    public bool IsModelLockedOn => AllowsMode(AiReasoningModes.On) && !AllowsMode(AiReasoningModes.Off);

    [JsonIgnore]
    public bool AllowsNonAutoMode => SupportsExplicitMode;

    [JsonIgnore]
    public string DefaultMode { get; init; } = AiReasoningModes.Auto;

    public bool AllowsMode(string? mode)
    {
        var normalized = AiReasoningModes.Normalize(mode);
        return AllowedModes.Contains(normalized, StringComparer.OrdinalIgnoreCase);
    }

    public bool AllowsEffort(string? effort)
    {
        var normalized = AiReasoningEfforts.Normalize(effort);
        return AllowedEfforts.Contains(normalized, StringComparer.OrdinalIgnoreCase);
    }

    public string NormalizeMode(string? mode)
    {
        var normalized = AiReasoningModes.Normalize(mode);
        if (AllowsMode(normalized))
            return normalized;

        if (AllowsMode(AiReasoningModes.Auto))
            return AiReasoningModes.Auto;

        return AllowedModes.FirstOrDefault() ?? AiReasoningModes.Auto;
    }

    public string NormalizeEffort(string? effort)
    {
        var normalized = AiReasoningEfforts.Normalize(effort);
        if (AllowsEffort(normalized))
            return normalized;

        if (AllowsEffort(AiReasoningEfforts.Medium))
            return AiReasoningEfforts.Medium;

        return AllowedEfforts.FirstOrDefault() ?? AiReasoningEfforts.Medium;
    }

    public string GetEffectiveMode(string? requestedMode)
    {
        var normalized = NormalizeMode(requestedMode);
        return normalized == AiReasoningModes.Auto ? DefaultMode : normalized;
    }

    public bool AllowsTemperature(string? requestedMode)
    {
        return FamilyId != AiReasoningModelFamilyCatalog.FamilyOpenAiGpt5
            || GetEffectiveMode(requestedMode) == AiReasoningModes.Off;
    }
}

public static class AiReasoningModelFamilyCatalog
{
    public const string FamilyOpenAiGpt5 = "openai_gpt5";
    public const string FamilyAnthropicClaude = "anthropic_claude";
    public const string FamilyDeepSeekChat = "deepseek_chat";
    public const string FamilyDeepSeekReasonerLocked = "deepseek_reasoner_locked";
    public const string FamilyGlmToggle = "glm_toggle";
    public const string FamilyGlmThinkingLocked = "glm_thinking_locked";
    public const string FamilyKimiDualModeMetadataOnly = "kimi_dual_mode_metadata_only";
    public const string FamilyKimiThinkingLocked = "kimi_thinking_locked";
    public const string FamilyUnknown = "unknown";

    private static readonly string[] AutoOnlyModes = [AiReasoningModes.Auto];
    private static readonly string[] AutoAndOnModes = [AiReasoningModes.Auto, AiReasoningModes.On];
    private static readonly string[] FullModeSet = [AiReasoningModes.Auto, AiReasoningModes.Off, AiReasoningModes.On];
    private static readonly string[] MediumOnlyEfforts = [AiReasoningEfforts.Medium];
    private static readonly string[] LowMediumHighEfforts = [AiReasoningEfforts.Low, AiReasoningEfforts.Medium, AiReasoningEfforts.High];
    private static readonly string[] HighOnlyEfforts = [AiReasoningEfforts.High];
    private static readonly string[] MediumHighEfforts = [AiReasoningEfforts.Medium, AiReasoningEfforts.High];

    public static AiReasoningSupportInfo Resolve(AiModelConfig? model)
    {
        if (model == null)
            return CreateUnknown();

        return Resolve(model.Provider, model.Model, model.BaseUrl, model.Protocol);
    }

    public static AiReasoningSupportInfo Resolve(
        string? provider,
        string? model,
        string? baseUrl,
        string? protocol = null)
    {
        var providerKey = (provider ?? string.Empty).Trim().ToLowerInvariant();
        var modelKey = (model ?? string.Empty).Trim().ToLowerInvariant();
        var protocolKey = (protocol ?? string.Empty).Trim().ToLowerInvariant();
        var hostKey = ExtractHost(baseUrl);

        if (protocolKey == AiModelConfig.ProtocolAnthropic || providerKey.Contains("anthropic"))
        {
            return CreateSupport(
                FamilyAnthropicClaude,
                "Anthropic Claude",
                FullModeSet,
                LowMediumHighEfforts,
                AiReasoningModes.Auto,
                "Claude 支持显式 thinking；本软件会把 Low / Medium / High 映射到固定的 thinking budget。");
        }

        if (IsDeepSeekReasoner(modelKey, hostKey))
        {
            return CreateSupport(
                FamilyDeepSeekReasonerLocked,
                "DeepSeek Reasoner",
                AutoAndOnModes,
                MediumOnlyEfforts,
                AiReasoningModes.On,
                "DeepSeek reasoner 在当前链路按固定思考模型处理，可保持 Auto 或显式 On，Off 不支持。");
        }

        if (IsDeepSeek(providerKey, modelKey, hostKey))
        {
            return CreateSupport(
                FamilyDeepSeekChat,
                "DeepSeek Chat",
                FullModeSet,
                MediumOnlyEfforts,
                AiReasoningModes.Auto,
                "DeepSeek 聊天模型支持 thinking 开关；当前版本不暴露强度档位。");
        }

        if (IsGlmThinkingLocked(modelKey, hostKey))
        {
            return CreateSupport(
                FamilyGlmThinkingLocked,
                "GLM Thinking",
                AutoAndOnModes,
                MediumOnlyEfforts,
                AiReasoningModes.On,
                "该 GLM 模型名按固定思考变体处理，可保持 Auto 或 On。");
        }

        if (IsGlm(providerKey, modelKey, hostKey))
        {
            return CreateSupport(
                FamilyGlmToggle,
                "GLM",
                FullModeSet,
                MediumOnlyEfforts,
                AiReasoningModes.Auto,
                "GLM 支持显式 thinking 开关；当前版本不暴露强度档位。");
        }

        if (IsKimiThinkingLocked(modelKey, hostKey))
        {
            return CreateSupport(
                FamilyKimiThinkingLocked,
                "Kimi Thinking",
                AutoAndOnModes,
                MediumOnlyEfforts,
                AiReasoningModes.On,
                "该 Kimi 模型名按固定思考变体处理，Off 不支持。");
        }

        if (IsKimi(providerKey, modelKey, hostKey))
        {
            return CreateSupport(
                FamilyKimiDualModeMetadataOnly,
                "Kimi",
                AutoOnlyModes,
                MediumOnlyEfforts,
                AiReasoningModes.Auto,
                "Kimi 已纳入模型族预设；当前版本只做识别与校验，暂不映射显式 On / Off，请保持 Auto。");
        }

        if (TryResolveOpenAiGpt5(modelKey, out var support))
        {
            return support;
        }

        return CreateUnknown();
    }

    private static AiReasoningSupportInfo CreateUnknown() => CreateSupport(
        FamilyUnknown,
        "Unknown",
        AutoOnlyModes,
        MediumOnlyEfforts,
        AiReasoningModes.Auto,
        "当前模型族未识别，建议保持 Auto，以免覆盖厂商默认行为。");

    private static AiReasoningSupportInfo CreateSupport(
        string familyId,
        string familyName,
        IEnumerable<string> allowedModes,
        IEnumerable<string> allowedEfforts,
        string defaultMode,
        string helpText)
    {
        return new AiReasoningSupportInfo
        {
            FamilyId = familyId,
            FamilyName = familyName,
            AllowedModes = allowedModes
                .Select(AiReasoningModes.Normalize)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            AllowedEfforts = allowedEfforts
                .Select(AiReasoningEfforts.Normalize)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            DefaultMode = AiReasoningModes.Normalize(defaultMode),
            HelpText = helpText
        };
    }

    private static bool TryResolveOpenAiGpt5(string modelKey, out AiReasoningSupportInfo support)
    {
        if (IsGpt52Pro(modelKey))
        {
            support = CreateSupport(
                FamilyOpenAiGpt5,
                "OpenAI GPT-5.2 Pro",
                AutoAndOnModes,
                MediumHighEfforts,
                AiReasoningModes.On,
                "GPT-5.2 Pro 在当前通道仅允许 Auto / On，强度可选 Medium 或 High；Off 不支持。");
            return true;
        }

        if (IsGpt5Pro(modelKey))
        {
            support = CreateSupport(
                FamilyOpenAiGpt5,
                "OpenAI GPT-5 Pro",
                AutoAndOnModes,
                HighOnlyEfforts,
                AiReasoningModes.On,
                "GPT-5 Pro 在当前通道仅允许 Auto / On，且强度固定为 High。");
            return true;
        }

        if (IsGpt52(modelKey))
        {
            support = CreateSupport(
                FamilyOpenAiGpt5,
                "OpenAI GPT-5.2",
                FullModeSet,
                LowMediumHighEfforts,
                AiReasoningModes.Off,
                "GPT-5.2 默认 reasoning_effort 为 none，Auto / Off 可保留默认，On 可选 Low / Medium / High。");
            return true;
        }

        if (IsGpt51(modelKey))
        {
            support = CreateSupport(
                FamilyOpenAiGpt5,
                "OpenAI GPT-5.1",
                FullModeSet,
                LowMediumHighEfforts,
                AiReasoningModes.Off,
                "GPT-5.1 默认 reasoning_effort 为 none，Auto / Off 可保留默认，On 可选 Low / Medium / High。");
            return true;
        }

        if (IsOpenAiGpt5(modelKey))
        {
            support = CreateSupport(
                FamilyOpenAiGpt5,
                "OpenAI GPT-5",
                AutoAndOnModes,
                LowMediumHighEfforts,
                AiReasoningModes.On,
                "当前 GPT-5 变体默认开启 reasoning；可保持 Auto 或显式 On，Off / none 不支持。");
            return true;
        }

        support = CreateUnknown();
        return false;
    }

    private static string ExtractHost(string? baseUrl)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
            return string.Empty;

        return Uri.TryCreate(baseUrl.Trim(), UriKind.Absolute, out var uri)
            ? uri.Host.ToLowerInvariant()
            : string.Empty;
    }

    private static bool IsOpenAiGpt5(string modelKey)
    {
        return modelKey.StartsWith("gpt-5", StringComparison.OrdinalIgnoreCase)
            || modelKey.Contains("gpt5", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsGpt51(string modelKey)
    {
        return modelKey.StartsWith("gpt-5.1", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsGpt52(string modelKey)
    {
        return modelKey.StartsWith("gpt-5.2", StringComparison.OrdinalIgnoreCase)
            && !IsGpt52Pro(modelKey);
    }

    private static bool IsGpt5Pro(string modelKey)
    {
        return modelKey.Equals("gpt-5-pro", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsGpt52Pro(string modelKey)
    {
        return modelKey.Equals("gpt-5.2-pro", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsDeepSeek(string providerKey, string modelKey, string hostKey)
    {
        return providerKey.Contains("deepseek")
            || hostKey.Contains("deepseek")
            || modelKey.StartsWith("deepseek-", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsDeepSeekReasoner(string modelKey, string hostKey)
    {
        return modelKey.Contains("deepseek-reasoner", StringComparison.OrdinalIgnoreCase)
            || (hostKey.Contains("deepseek") && modelKey.Contains("reasoner", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsGlm(string providerKey, string modelKey, string hostKey)
    {
        return providerKey.Contains("glm")
            || providerKey.Contains("bigmodel")
            || hostKey.Contains("bigmodel")
            || modelKey.StartsWith("glm", StringComparison.OrdinalIgnoreCase)
            || modelKey.Contains("zhipu", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsGlmThinkingLocked(string modelKey, string hostKey)
    {
        return (hostKey.Contains("bigmodel") || modelKey.StartsWith("glm", StringComparison.OrdinalIgnoreCase))
            && modelKey.Contains("thinking", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsKimi(string providerKey, string modelKey, string hostKey)
    {
        return providerKey.Contains("kimi")
            || providerKey.Contains("moonshot")
            || hostKey.Contains("moonshot")
            || modelKey.StartsWith("kimi", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsKimiThinkingLocked(string modelKey, string hostKey)
    {
        return (hostKey.Contains("moonshot") || modelKey.StartsWith("kimi", StringComparison.OrdinalIgnoreCase))
            && modelKey.Contains("thinking", StringComparison.OrdinalIgnoreCase);
    }
}
