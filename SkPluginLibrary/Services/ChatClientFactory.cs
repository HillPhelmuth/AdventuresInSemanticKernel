using System.ClientModel;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using OpenAI;

namespace SkPluginLibrary.Services;

/// <summary>
/// Creates the Microsoft.Extensions.AI <see cref="IChatClient"/> (and embedding generators) that every
/// Agent Framework agent in this app sits on top of. Two providers are supported:
/// <list type="bullet">
/// <item><description><b>OpenAI</b> - the default.</description></item>
/// <item><description><b>OpenRouter</b> - an OpenAI-compatible gateway, reached through the same OpenAI client with a custom endpoint.</description></item>
/// </list>
/// </summary>
public static class ChatClientFactory
{
    public const string OpenAIProvider = "OpenAI";
    public const string OpenRouterProvider = "OpenRouter";

    /// <summary>Works out which provider (and which provider-specific model name) serves a model.</summary>
    public static (string Provider, string ModelName) Resolve(AIModel model)
    {
        var name = model.GetOpenAIModelName();
        var providers = model.GetModelProvidors().ToList();
        if (providers.Contains(OpenRouterProvider)) return (OpenRouterProvider, name);
        // An OpenAI model can still be served through OpenRouter when that is the configured service.
        if (TestConfiguration.CoreSettings?.Service == OpenRouterProvider) return (OpenRouterProvider, $"openai/{name}");
        return (OpenAIProvider, name);
    }

    public static OpenAIClient CreateOpenAIClient(string provider = OpenAIProvider)
    {
        if (provider == OpenRouterProvider)
        {
            var config = TestConfiguration.OpenRouter!;
            return new OpenAIClient(new ApiKeyCredential(config.ApiKey), new OpenAIClientOptions { Endpoint = new Uri(config.Endpoint) });
        }
        return new OpenAIClient(TestConfiguration.OpenAI!.ApiKey);
    }

    /// <summary>Creates a chat client for a model, with automatic function (tool) invocation enabled.</summary>
    public static IChatClient Create(AIModel model = AIModel.Gpt41Mini, ILoggerFactory? loggerFactory = null, int? maxIterations = null)
    {
        var (provider, modelName) = Resolve(model);
        return Create(modelName, provider, loggerFactory, maxIterations);
    }

    public static IChatClient Create(string modelName, string provider = OpenAIProvider, ILoggerFactory? loggerFactory = null, int? maxIterations = null)
    {
        var client = CreateOpenAIClient(provider).GetChatClient(modelName).AsIChatClient();
        var builder = client.AsBuilder();
        if (loggerFactory is not null) builder.UseLogging(loggerFactory);
        return builder.UseFunctionInvocation(loggerFactory, c =>
        {
            if (maxIterations is { } max) c.MaximumIterationsPerRequest = max;
        }).Build();
    }

    /// <summary>Creates an embedding generator (always OpenAI; OpenRouter does not serve embeddings).</summary>
    public static IEmbeddingGenerator<string, Embedding<float>> CreateEmbeddingGenerator(string? model = null)
    {
        model ??= TestConfiguration.OpenAI!.EmbeddingModelId ?? "text-embedding-3-small";
        return CreateOpenAIClient(OpenAIProvider).GetEmbeddingClient(model).AsIEmbeddingGenerator();
    }
}
