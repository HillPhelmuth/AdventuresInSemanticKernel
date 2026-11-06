using AgentFrameworkOrchestration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using AgentToolComponents;
using AgentToolComponents.Models;
using AgentFrameworkLibrary.Services;
using System.Text.Json;

namespace AgentFrameworkLibrary;

public sealed class RuntimeConfiguration(IConfiguration configuration)
{
    public ModelSettings Current { get; set; } = Load(configuration, configuration["CoreSettings:Service"] ?? "OpenAI");
    public ModelSettings ForProvider(string provider) => Load(configuration, provider);
    public string? Get(string key) => configuration[key];

    public ModelSettings ReadBrowserSettings(string provider, JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            var settings = value.Deserialize<ModelSettings>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? throw new InvalidDataException("Invalid stored configuration.");
            settings.Provider = provider;
            return settings;
        }
        if (value.ValueKind != JsonValueKind.Array) throw new InvalidDataException("Invalid stored configuration format.");
        var properties = value.EnumerateArray().ToDictionary(item => item.GetProperty("Name").GetString()!,
            item => item.GetProperty("Value").ToString(), StringComparer.OrdinalIgnoreCase);
        var legacy = ForProvider(provider);
        legacy.ApiKey = properties.GetValueOrDefault("ApiKey", legacy.ApiKey);
        legacy.Model = properties.GetValueOrDefault("ModelId", properties.GetValueOrDefault("Gpt4MiniModelId",
            properties.GetValueOrDefault("Gpt4ModelId", legacy.Model)));
        legacy.Endpoint = properties.GetValueOrDefault("Endpoint", legacy.Endpoint);
        legacy.EmbeddingModel = properties.GetValueOrDefault("EmbeddingModelId",
            properties.GetValueOrDefault("Ada002ModelId", legacy.EmbeddingModel));
        return legacy;
    }

    private static ModelSettings Load(IConfiguration config, string provider) => new()
    {
        Provider = provider,
        ApiKey = config[$"{provider}:ApiKey"] ?? "",
        Model = config[$"{provider}:ModelId"] ?? config[$"{provider}:Gpt4MiniModelId"] ??
            (provider == "OpenRouter" ? "openai/gpt-4.1-mini" : "gpt-4.1-mini"),
        Endpoint = config[$"{provider}:Endpoint"] ?? "",
        EmbeddingModel = config[$"{provider}:EmbeddingModelId"] ?? "text-embedding-3-small",
        EmbeddingDimensions = int.TryParse(config[$"{provider}:EmbeddingDimensions"], out var dimensions) ? dimensions : 1536,
        SupportsTools = true,
        SupportsLogprobs = provider == "OpenAI"
    };
}

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAgentFrameworkServices(this IServiceCollection services)
    {
        services.AddScoped<RuntimeConfiguration>();
        services.AddScoped<ToolCatalog>();
        services.AddScoped<CoreAgentService>();
        services.AddScoped<MemoryService>();
        services.AddScoped<TokenService>();
        services.AddScoped<ApiTools>();
        services.AddScoped<MediaService>();
        services.AddScoped<CompilerService>();
        services.AddScoped<ScriptService>();
        services.AddScoped<BlazorReplPlugin>();
        services.AddScoped<AskUserPlugin>();
        services.AddScoped<RazorSnippetCompiler>();
        return services;
    }
}
