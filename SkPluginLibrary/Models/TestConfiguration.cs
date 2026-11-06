using Microsoft.Extensions.Configuration;
using System.Runtime.CompilerServices;
using SkPluginLibrary.Reliability;

namespace SkPluginLibrary.Models;

/// <summary>
/// Strongly typed access to the application's configuration sections.
/// Model providers: <c>OpenAI</c> and <c>OpenRouter</c> (an OpenAI-compatible gateway).
/// </summary>
public sealed class TestConfiguration
{
    private IConfiguration _configRoot;
    private static TestConfiguration? s_instance;

    private TestConfiguration(IConfiguration configRoot)
    {
        _configRoot = configRoot;
    }

    public static void Initialize(IConfiguration configRoot)
    {
        s_instance = new TestConfiguration(configRoot);
        var (isValid, message) = Validate();
        if (!isValid)
            throw new ConfigurationNotFoundException(message);
    }

    /// <summary>The settings for whichever provider <c>CoreSettings:Service</c> selects.</summary>
    private static CoreAISettings SetCore()
    {
        return CoreSettings?.Service switch
        {
            "OpenAI" => new CoreAISettings
            {
                ApiKey = OpenAI!.ApiKey,
                Gpt35ModelId = OpenAI.Gpt4MiniModelId,
                Gpt4ModelId = OpenAI.Gpt4ModelId,
                ImageModelId = OpenAI.ImageModelId,
                PlannerModelId = OpenAI.PlannerModelId
            },
            "OpenRouter" => new CoreAISettings
            {
                ApiKey = OpenRouter!.ApiKey,
                Endpoint = OpenRouter.Endpoint,
                Gpt35ModelId = OpenRouter.ModelId,
                Gpt4ModelId = OpenRouter.ModelId,
                PlannerModelId = OpenRouter.ModelId
            },
            _ => throw new ConfigurationNotFoundException("CoreSettings")
        };
    }

    public static (bool isValid, string message) Validate()
    {
        if (string.IsNullOrWhiteSpace(CoreAISettings.ApiKey)) return (false, "missing ApiKey");
        var missingModel = string.IsNullOrWhiteSpace(CoreAISettings.Gpt35ModelId) &&
                           string.IsNullOrWhiteSpace(CoreAISettings.Gpt4ModelId) &&
                           string.IsNullOrWhiteSpace(CoreAISettings.PlannerModelId);
        return missingModel ? (false, "missing ModelId") : (true, "");
    }

    public static CoreAISettings CoreAISettings => SetCore();

    private static OpenAIConfig? _openAi;
    public static OpenAIConfig? OpenAI
    {
        get => _openAi ?? LoadSection<OpenAIConfig>();
        set => _openAi = value;
    }

    private static OpenRouterConfig? _openRouter;
    public static OpenRouterConfig? OpenRouter
    {
        get => _openRouter ?? LoadSection<OpenRouterConfig>();
        set => _openRouter = value;
    }

    private static BingConfig? _bing;
    public static BingConfig? Bing
    {
        get => _bing ?? LoadSection<BingConfig>();
        set => _bing = value;
    }

    private static GoogleConfig? _google;
    public static GoogleConfig? Google
    {
        get => _google ?? LoadSection<GoogleConfig>();
        set => _google = value;
    }

    private static GithubConfig? _github;
    public static GithubConfig? Github
    {
        get => _github ?? LoadSection<GithubConfig>();
        set => _github = value;
    }

    private static JiraConfig? _jira;
    public static JiraConfig? Jira
    {
        get => _jira ?? LoadSection<JiraConfig>();
        set => _jira = value;
    }

    private static SqliteConfig? _sqlite;
    public static SqliteConfig? Sqlite
    {
        get => _sqlite ?? LoadSection<SqliteConfig>();
        set => _sqlite = value;
    }

    private static CoreSettingsConfig? _coreSettings;
    public static CoreSettingsConfig? CoreSettings
    {
        get => _coreSettings ?? LoadSection<CoreSettingsConfig>();
        set => _coreSettings = value;
    }

    private static T? LoadSection<T>([CallerMemberName] string? caller = null)
    {
        if (s_instance == null)
        {
            return default;
        }

        return s_instance._configRoot.GetSection(caller!).Get<T>() ?? throw new ConfigurationNotFoundException(caller!);
    }

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor.
    public class OpenAIConfig
    {
        public string Gpt4MiniModelId { get; set; }
        public string Gpt4ModelId { get; set; }
        public string EmbeddingModelId { get; set; }
        public string ApiKey { get; set; }
        public string ImageModelId { get; set; }
        public string PlannerModelId { get; set; }
        public string ChatModelId { get; set; }
    }

    public class OpenRouterConfig
    {
        public string ApiKey { get; set; }
        public string ModelId { get; set; } = "openai/gpt-4.1-mini";
        public string Endpoint { get; set; } = "https://openrouter.ai/api/v1";
    }

    public class BingConfig
    {
        public string ApiKey { get; set; }
    }

    public class GoogleConfig
    {
        public string ApiKey { get; set; }
        public string SearchEngineId { get; set; }
    }

    public class GithubConfig
    {
        public string PAT { get; set; }
    }

    public class JiraConfig
    {
        public string Email { get; set; }
        public string ApiKey { get; set; }
        public string Domain { get; set; }
    }

    public class SqliteConfig
    {
        public string ConnectionString { get; set; }
        public string ChatContentConnectionString { get; set; }
    }

    public class CoreSettingsConfig
    {
        public string Service { get; set; }
    }
#pragma warning restore CS8618
}

// ReSharper disable once InconsistentNaming
public class CoreAISettings
{
    public string? Endpoint { get; set; }
    public string? ApiKey { get; set; }
    public string? Gpt35ModelId { get; set; }
    public string? Gpt4ModelId { get; set; }
    public string? ImageModelId { get; set; }
    public string? PlannerModelId { get; set; }
}
