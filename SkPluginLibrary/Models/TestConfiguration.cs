// Copyright (c) Microsoft. All rights reserved.

using Microsoft.Extensions.Configuration;
using System.Runtime.CompilerServices;

namespace SkPluginLibrary.Models;

public sealed class TestConfiguration
{
    private IConfiguration _configRoot;
    private static TestConfiguration? s_instance;

    private TestConfiguration(IConfiguration configRoot)
    {
        this._configRoot = configRoot;
    }

    public static void Initialize(IConfiguration configRoot)
    {
        s_instance = new TestConfiguration(configRoot);
    }

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

    private static ACSConfig? _acs;
    public static ACSConfig? ACS
    {
        get => _acs ?? LoadSection<ACSConfig>();
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



    private static T? LoadSection<T>([CallerMemberName] string? caller = null)
    {
        if (s_instance == null)
        {
            throw new InvalidOperationException(
                "TestConfiguration must be initialized with a call to Initialize(IConfigurationRoot) before accessing configuration values.");
        }

        if (string.IsNullOrEmpty(caller))
        {
            throw new ArgumentNullException(nameof(caller));
        }


        return s_instance._configRoot.GetSection(caller).Get<T>();
    }

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor.
    public class OpenAIConfig
    {
        public string ModelId { get; set; }
        public string ChatModelId { get; set; }
        public string EmbeddingModelId { get; set; }
        public string ApiKey { get; set; }
    }

    public class AzureOpenAIConfig
    {
        public string ServiceId { get; set; }
        public string DeploymentName { get; set; }
        public string ChatDeploymentName { get; set; }
        public string Endpoint { get; set; }
        public string ApiKey { get; set; }
    }

    public class AzureOpenAIEmbeddingsConfig
    {
        public string DeploymentName { get; set; }
        public string Endpoint { get; set; }
        public string ApiKey { get; set; }
    }

    public class ACSConfig
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
        public string TenantId { get; set; }
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
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor.
}