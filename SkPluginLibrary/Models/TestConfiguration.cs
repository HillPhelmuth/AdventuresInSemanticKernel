// Copyright (c) Microsoft. All rights reserved.

using Microsoft.Extensions.Configuration;
using System.Runtime.CompilerServices;
using SkPluginLibrary.Reliability;

namespace SkPluginLibrary.Models;

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
    private static CoreAISettings SetCore()
    {
        return CoreSettings?.Service switch
        {
            "OpenAI" => new CoreAISettings
            {
                ApiKey = OpenAI.ApiKey,
                Gpt35ModelId = OpenAI.Gpt35ModelId,
                Gpt4ModelId = OpenAI.Gpt4ModelId,
                ImageModelId = OpenAI.ImageModelId,
                PlannerModelId = OpenAI.PlannerModelId
            },
            "AzureOpenAI" => new CoreAISettings
            {
                ServiceId = AzureOpenAI.ServiceId,
                Gpt35DeploymentName = AzureOpenAI.Gpt35DeploymentName,
                Gpt4DeploymentName = AzureOpenAI.Gpt4DeploymentName,
                PlannerDeploymentName = AzureOpenAI.PlannerDeploymentName,
                Endpoint = AzureOpenAI.Endpoint,
                ApiKey = AzureOpenAI.ApiKey,
                Gpt35ModelId = AzureOpenAI.ModelId,
                Gpt4ModelId = AzureOpenAI.Gpt35ModelId,
                ImageModelId = AzureOpenAI.ImageModelId,
                PlannerModelId = AzureOpenAI.PlannerModelId,
            },
            _ => throw new ConfigurationNotFoundException("CoreSettings")
        };
    }
    public static (bool isValid, string message) Validate()
    {
        var isAzure = CoreSettings?.Service == "AzureOpenAI";
        if (isAzure)
        {
            if (string.IsNullOrWhiteSpace(CoreAISettings.ApiKey)) return (false, "missing ApiKey");
            if (string.IsNullOrEmpty(CoreAISettings.Endpoint)) return (false, "missing Endpoint");
            var missingDeployments = string.IsNullOrWhiteSpace(CoreAISettings.Gpt35DeploymentName) &&
                                     string.IsNullOrWhiteSpace(CoreAISettings.Gpt4DeploymentName) &&
                                     string.IsNullOrWhiteSpace(CoreAISettings.PlannerDeploymentName);
            if (missingDeployments) return (false, "missing DeploymentName");
            var missingModelId = string.IsNullOrWhiteSpace(CoreAISettings.Gpt35ModelId) &&
                                 string.IsNullOrWhiteSpace(CoreAISettings.Gpt4ModelId) &&
                                 string.IsNullOrWhiteSpace(CoreAISettings.PlannerModelId);
            return missingModelId ? (false, "missing Modelname") : (true, "");
        }
        if (string.IsNullOrWhiteSpace(CoreAISettings.ApiKey)) return (false, "missing ApiKey");
        var missingModel = string.IsNullOrWhiteSpace(CoreAISettings.Gpt35ModelId) &&
                             string.IsNullOrWhiteSpace(CoreAISettings.Gpt4ModelId) &&
                             string.IsNullOrWhiteSpace(CoreAISettings.PlannerModelId);
        return missingModel ? (false, "missing ModelId") : (true,"");
    }
    public static CoreAISettings CoreAISettings => SetCore();
    private static OpenAIConfig? _openAi;
    public static OpenAIConfig? OpenAI
    {
        get => _openAi ?? LoadSection<OpenAIConfig>();
        set => _openAi = value;
    }

    private static AzureOpenAIConfig? _azureOpenAI;
    public static AzureOpenAIConfig? AzureOpenAI
    {
        get => _azureOpenAI ?? LoadSection<AzureOpenAIConfig>();
        set => _azureOpenAI = value;
    }

    private static AzureOpenAIEmbeddingsConfig? _azureOpenAIEmbeddings;
    public static AzureOpenAIEmbeddingsConfig? AzureOpenAIEmbeddings
    {
        get => _azureOpenAIEmbeddings ?? LoadSection<AzureOpenAIEmbeddingsConfig>();
        set => _azureOpenAIEmbeddings = value;
    }

    private static AzureAISearchConfig? _acs;
    public static AzureAISearchConfig? AzureAISearch
    {
        get => _acs ?? LoadSection<AzureAISearchConfig>();
        set => _acs = value;
    }

    private static QdrantConfig? _qdrant;
    public static QdrantConfig? Qdrant
    {
        get => _qdrant ?? LoadSection<QdrantConfig>();
        set => _qdrant = value;
    }

    private static WeaviateConfig? _weaviate;
    public static WeaviateConfig? Weaviate
    {
        get => _weaviate ?? LoadSection<WeaviateConfig>();
        set => _weaviate = value;
    }

    private static KeyVaultConfig? _keyVault;
    public static KeyVaultConfig? KeyVault
    {
        get => _keyVault ?? LoadSection<KeyVaultConfig>();
        set => _keyVault = value;
    }

    private static HuggingFaceConfig? _huggingFace;
    public static HuggingFaceConfig? HuggingFace
    {
        get => _huggingFace ?? LoadSection<HuggingFaceConfig>();
        set => _huggingFace = value;
    }

    private static PineconeConfig? _pinecone;
    public static PineconeConfig? Pinecone
    {
        get => _pinecone ?? LoadSection<PineconeConfig>();
        set => _pinecone = value;
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

    private static PostgresConfig? _postgres;
    public static PostgresConfig? Postgres
    {
        get => _postgres ?? LoadSection<PostgresConfig>();
        set => _postgres = value;
    }

    private static RedisConfig? _redis;
    public static RedisConfig? Redis
    {
        get => _redis ?? LoadSection<RedisConfig>();
        set => _redis = value;
    }

    private static JiraConfig? _jira;
    public static JiraConfig? Jira
    {
        get => _jira ?? LoadSection<JiraConfig>();
        set => _jira = value;
    }

    private static ChromaConfig? _chroma;
    public static ChromaConfig? Chroma
    {
        get => _chroma ?? LoadSection<ChromaConfig>();
        set => _chroma = value;
    }

    private static KustoConfig? _kusto;
    public static KustoConfig? Kusto
    {
        get => _kusto ?? LoadSection<KustoConfig>();
        set => _kusto = value;
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
    private static GoogleAIConfig? _googleAI;
    public static GoogleAIConfig? GoogleAI
    {
        get => _googleAI ?? LoadSection<GoogleAIConfig>();
        set => _googleAI = value;
    }
    
    private static MistralAIConfig? _mistralAI;
    public static MistralAIConfig? MistralAI
	{
		get => _mistralAI ?? LoadSection<MistralAIConfig>();
		set => _mistralAI = value;
	}

    private static T? LoadSection<T>([CallerMemberName] string? caller = null)
    {
        if (s_instance == null)
        {
            throw new InvalidOperationException(
                "TestConfiguration must be initialized with a call to Initialize(IConfigurationRoot) before accessing configuration values.");
        }

        if (string.IsNullOrEmpty(caller))
        {
            return default(T);
        }


        return s_instance._configRoot.GetSection(caller).Get<T>() ?? throw new ConfigurationNotFoundException(caller);
    }

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor.
    public class OpenAIConfig
    {
        public string Gpt35ModelId { get; set; }
        public string Gpt4ModelId { get; set; }
        public string EmbeddingModelId { get; set; }
        public string ApiKey { get; set; }
        public string ImageModelId { get; set; }
        public string PlannerModelId { get; set; }
    }

    public class AzureOpenAIConfig
    {
        public string ServiceId { get; set; }
        public string Gpt35DeploymentName { get; set; }
        public string Gpt4DeploymentName { get; set; }
        public string Endpoint { get; set; }
        public string ApiKey { get; set; }
        public string ModelId { get; set; }
        public string Gpt35ModelId { get; set; }
        public string ImageModelId { get; set; }
        public string PlannerModelId { get; set; }
        public string PlannerDeploymentName { get; set; }
    }

    public class AzureOpenAIEmbeddingsConfig
    {
        public string DeploymentName { get; set; }
        public string Endpoint { get; set; }
        public string ApiKey { get; set; }
    }

    public class AzureAISearchConfig
    {
        public string Endpoint { get; set; }
        public string ApiKey { get; set; }
        public string IndexName { get; set; }
    }

    public class QdrantConfig
    {
        public string Endpoint { get; set; }
        public string Port { get; set; }
    }

    public class WeaviateConfig
    {
        public string Scheme { get; set; }
        public string Endpoint { get; set; }
        public string Port { get; set; }
        public string ApiKey { get; set; }
        public string Version { get; set; }
    }
    
    public class KeyVaultConfig
    {
        public string Endpoint { get; set; }
        public string ClientId { get; set; }
        public string ClientSecret { get; set; }
    }

    public class HuggingFaceConfig
    {
        public string ApiKey { get; set; }
        public string ModelId { get; set; }
    }

    public class PineconeConfig
    {
        public string ApiKey { get; set; }
        public string Environment { get; set; }
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
    public class GoogleAIConfig
    {
        public string ApiKey { get; set; }
        public string EmbeddingModelId { get; set; }
        public GeminiConfig Gemini { get; set; }

        public class GeminiConfig
        {
            public string ModelId { get; set; }
        }
    }
    public class MistralAIConfig
    {
        public string ApiKey { get; set; }
    }
    
    public class PostgresConfig
    {
        public string ConnectionString { get; set; }
    }

    public class RedisConfig
    {
        public string Configuration { get; set; }
    }

    public class JiraConfig
    {
        public string ApiKey { get; set; }
        public string Email { get; set; }
        public string Domain { get; set; }
    }

    public class ChromaConfig
    {
        public string Endpoint { get; set; }
    }

    public class KustoConfig
    {
        public string ConnectionString { get; set; }
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

#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor.
}
// ReSharper disable once InconsistentNaming
public class CoreAISettings
{
    public string? ServiceId { get; set; }
    public string? Gpt35DeploymentName { get; set; }
    public string? Gpt4DeploymentName { get; set; }
    public string? Endpoint { get; set; }
    public string? ApiKey { get; set; }
    public string? Gpt35ModelId { get; set; }
    public string? Gpt4ModelId { get; set; }
    public string? ImageModelId { get; set; }
    public string? PlannerModelId { get; set; }
    public string? PlannerDeploymentName { get; set; }
}

public static class Validations
{
    public static bool IsValid(this TestConfiguration.WeaviateConfig weaviateConfig)
    {
        return !string.IsNullOrEmpty(weaviateConfig.Scheme) && !string.IsNullOrEmpty(weaviateConfig.Endpoint) && !string.IsNullOrEmpty(weaviateConfig.ApiKey);
    }
}