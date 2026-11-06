using System.Text.Json.Serialization;
using System.Runtime.CompilerServices;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI;
using System.ClientModel;

namespace AgentFrameworkOrchestration;

public sealed class ModelSettings
{
    public string Provider { get; set; } = "OpenAI";
    public string ApiKey { get; set; } = "";
    public string Model { get; set; } = "gpt-4.1-mini";
    public string Endpoint { get; set; } = "";
    public string EmbeddingModel { get; set; } = "text-embedding-3-small";
    public int EmbeddingDimensions { get; set; } = 1536;
    public bool SupportsTools { get; set; } = true;
    public bool SupportsLogprobs { get; set; }

    public Uri GetEndpoint()
    {
        if (Provider is not ("OpenAI" or "OpenRouter"))
            throw new NotSupportedException($"Provider '{Provider}' is retired. Select OpenAI or OpenRouter.");
        var uri = new Uri(string.IsNullOrWhiteSpace(Endpoint)
            ? Provider == "OpenRouter" ? "https://openrouter.ai/api/v1" : "https://api.openai.com/v1"
            : Endpoint);
        if (uri.Scheme != Uri.UriSchemeHttps && !(uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback))
            throw new ArgumentException("Model endpoints must use HTTPS.");
        if (uri.UserInfo.Length != 0) throw new ArgumentException("Do not embed credentials in model endpoint URLs.");
        return uri;
    }

    public void Validate()
    {
        _ = GetEndpoint();
        ArgumentException.ThrowIfNullOrWhiteSpace(ApiKey, nameof(ApiKey));
        ArgumentException.ThrowIfNullOrWhiteSpace(Model, nameof(Model));
        if (EmbeddingDimensions <= 0) throw new ArgumentOutOfRangeException(nameof(EmbeddingDimensions));
    }

    public OpenAIClient CreateClient(HttpClient? httpClient = null)
    {
        Validate();
        var options = new OpenAIClientOptions { Endpoint = GetEndpoint() };
        if (httpClient is not null) options.Transport = new System.ClientModel.Primitives.HttpClientPipelineTransport(httpClient);
        return new OpenAIClient(new ApiKeyCredential(ApiKey), options);
    }
}

[JsonConverter(typeof(JsonStringEnumConverter<HistoryVisibility>))]
public enum HistoryVisibility { All, Self, LastMessage, SelfAndUser = Self, None = LastMessage }
public enum GroupTransition { Sequential, HubAndSpoke, PromptBased }

public sealed class AgentDefinition
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "Assistant";
    public string Description { get; set; } = "";
    public string Instructions { get; set; } = "Assist the user using the selected tools.";
    public string? GptModel { get; set; }
    public List<string> PluginNames { get; set; } = [];
    public List<string> ExcludedFunctionNames { get; set; } = [];
    public bool IsPrimary { get; set; }
    public bool IsUserProxy { get; set; }
    public HistoryVisibility ChatHistoryType { get; set; }

    public void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Id);
        ArgumentException.ThrowIfNullOrWhiteSpace(Name);
        if (PluginNames is null || ExcludedFunctionNames is null) throw new ArgumentException("Tool groups and excluded functions cannot be null.");
        if (!Enum.IsDefined(ChatHistoryType)) throw new ArgumentException("Unknown conversation history mode.");
        if (GptModel is "Gpt35" or "Gpt4") GptModel = null;
    }
}

public static class AgentFactory
{
    public static ChatClientAgent Create(IChatClient client, AgentDefinition definition, IList<AITool> tools)
    {
        definition.Validate();
        var historyClient = new HistoryClient(client, definition.Name, definition.ChatHistoryType);
        return new ChatClientAgent(historyClient, new ChatClientAgentOptions
        {
            Id = definition.Id, Name = definition.Name, Description = definition.Description,
            ChatOptions = new ChatOptions { Instructions = definition.Instructions, Tools = tools }
        });
    }

    public static IChatClient CreateClient(ModelSettings settings, AgentDefinition definition)
    {
        settings.Validate();
        definition.Validate();
        return settings.CreateClient().GetChatClient(definition.GptModel ?? settings.Model).AsIChatClient();
    }

    private sealed class HistoryClient(IChatClient inner, string name, HistoryVisibility visibility)
        : DelegatingChatClient(inner)
    {
        private List<ChatMessage> Filter(IEnumerable<ChatMessage> messages)
        {
            var all = messages.ToList();
            if (visibility == HistoryVisibility.All) return all;
            var system = all.Where(m => m.Role == ChatRole.System).ToList();
            var conversation = all.Where(m => m.Role != ChatRole.System).ToList();
            if (visibility == HistoryVisibility.LastMessage)
            {
                var latest = conversation.FindLastIndex(m => m.Role != ChatRole.Tool && !string.IsNullOrEmpty(m.Text)
                    && !m.Contents.Any(c => c is ToolCallContent or ToolResultContent));
                system.AddRange(conversation.Skip(Math.Max(0, latest)));
            }
            else
            {
                var calls = conversation.Where(m => m.AuthorName is null || m.AuthorName == name)
                    .SelectMany(m => m.Contents.OfType<ToolCallContent>()).Select(c => c.CallId).ToHashSet();
                system.AddRange(conversation.Where(m => m.Role == ChatRole.Tool
                    ? m.Contents.OfType<ToolResultContent>().Any(c => calls.Contains(c.CallId))
                    : m.AuthorName is null || m.AuthorName == name));
            }
            return system;
        }

        public override async Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            var response = await base.GetResponseAsync(Filter(messages), options, cancellationToken);
            foreach (var message in response.Messages.Where(m => m.Role == ChatRole.Assistant)) message.AuthorName ??= name;
            return response;
        }

        public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages,
            ChatOptions? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await foreach (var update in base.GetStreamingResponseAsync(Filter(messages), options, cancellationToken))
            {
                update.AuthorName ??= name;
                yield return update;
            }
        }
    }
}
