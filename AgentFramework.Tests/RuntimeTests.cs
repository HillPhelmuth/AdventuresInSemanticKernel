using System.Runtime.CompilerServices;
using System.Text.Json;
using AgentFrameworkLibrary;
using AgentFrameworkOrchestration;
using AgentToolComponents;
using AgentToolComponents.Models;
using AgentFrameworkLibrary.Services;
using Microsoft.Agents.AI;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace AgentFramework.Tests;

public sealed class RuntimeTests
{
    private static RuntimeConfiguration Config(string? directory = null) => new(new ConfigurationBuilder().AddInMemoryCollection(
        new Dictionary<string, string?> { ["AgentFramework:MemoryDirectory"] = directory, ["OpenAI:EmbeddingDimensions"] = "2" }).Build());

    [Theory]
    [InlineData("OpenAI", "https://api.openai.com/v1")]
    [InlineData("OpenRouter", "https://openrouter.ai/api/v1")]
    public void ProviderEndpointsAreExplicit(string provider, string endpoint) =>
        Assert.Equal(endpoint, new ModelSettings { Provider = provider }.GetEndpoint().ToString());

    [Theory]
    [InlineData("AzureOpenAI")]
    [InlineData("GoogleAI")]
    [InlineData("MistralAI")]
    public void RetiredProvidersFailClearly(string provider) =>
        Assert.Throws<NotSupportedException>(() => new ModelSettings { Provider = provider }.GetEndpoint());

    [Fact]
    public void InsecureNonLocalEndpointIsRejected() =>
        Assert.Throws<ArgumentException>(() => new ModelSettings { Endpoint = "http://example.org" }.GetEndpoint());

    [Fact]
    public async Task SessionsRetainHistoryAndResetClearsIt()
    {
        var client = new FakeChatClient();
        await using var conversation = new AgentConversation(AgentFactory.Create(client, new AgentDefinition(), []));
        await Drain(conversation.RunAsync("first"));
        await Drain(conversation.RunAsync("second"));
        Assert.Equal(2, client.Requests.Last().Count(m => m.Role == ChatRole.User));
        await conversation.ResetAsync();
        await Drain(conversation.RunAsync("fresh"));
        Assert.Single(client.Requests.Last(), m => m.Role == ChatRole.User);
        Assert.DoesNotContain(client.Requests.Last(), m => m.Text == "first");
    }

    [Fact]
    public async Task SeparateConversationsDoNotShareHistory()
    {
        var client = new FakeChatClient();
        await using var a = new AgentConversation(AgentFactory.Create(client, new AgentDefinition(), []));
        await using var b = new AgentConversation(AgentFactory.Create(client, new AgentDefinition(), []));
        await Drain(a.RunAsync("secret for A"));
        await Drain(b.RunAsync("input for B"));
        Assert.DoesNotContain(client.Requests.Last(), m => m.Text.Contains("secret for A"));
    }

    [Theory]
    [InlineData(true, 1)]
    [InlineData(false, 0)]
    public async Task SideEffectsWaitForBoundApproval(bool approve, int expected)
    {
        var calls = 0;
        var client = new FakeChatClient { Tool = "write" };
        var tool = new ApprovalRequiredAIFunction(AIFunctionFactory.Create(() => { calls++; return "done"; }, "write"));
        await using var conversation = new AgentConversation(AgentFactory.Create(client, new AgentDefinition(), [tool]));
        await Drain(conversation.RunAsync("write"));
        Assert.Equal(0, calls);
        var request = Assert.Single(conversation.PendingApprovals);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Drain(conversation.RespondAsync("invented", true)));
        await Drain(conversation.RespondAsync(request.RequestId, approve));
        Assert.Equal(expected, calls);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Drain(conversation.RespondAsync(request.RequestId, true)));
    }

    [Fact]
    public async Task ConcurrentRunsAreRejected()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var client = new FakeChatClient { Started = started, Release = release };
        await using var conversation = new AgentConversation(AgentFactory.Create(client, new AgentDefinition(), []));
        var first = Drain(conversation.RunAsync("first"));
        await started.Task;
        await Assert.ThrowsAsync<InvalidOperationException>(() => Drain(conversation.RunAsync("second")));
        release.SetResult();
        await first;
    }

    [Theory]
    [InlineData(GroupTransition.Sequential)]
    [InlineData(GroupTransition.HubAndSpoke)]
    public async Task WorkflowHonorsTurnLimit(GroupTransition transition)
    {
        var a = new FakeChatClient();
        var b = new FakeChatClient();
        var workflow = GroupWorkflow.Build([
            AgentFactory.Create(a, new AgentDefinition { Name = "A" }, []),
            AgentFactory.Create(b, new AgentDefinition { Name = "B" }, [])
        ], transition, 3, "[STOP]");
        await Drain(GroupWorkflow.RunAsync(workflow, "begin", (_, _) => Task.FromResult(false)));
        Assert.Equal(3, a.Requests.Count + b.Requests.Count);
    }

    [Fact]
    public void WorkflowRejectsUnboundedRuns() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => GroupWorkflow.Build(
            [AgentFactory.Create(new FakeChatClient(), new AgentDefinition(), [])], GroupTransition.Sequential, 0, ""));

    [Fact]
    public async Task MemoryRanksScoresFiltersAndIsolatesCollections()
    {
        var config = Config();
        var memory = new MemoryService(config, new FakeEmbeddings());
        await memory.AddAsync("a", "east", "one");
        await memory.AddAsync("a", "north", "two");
        await memory.AddAsync("other", "east", "one");
        var matches = await memory.SearchAsync("a", "east", 1, 0.9);
        Assert.Equal("east", Assert.Single(matches).Item.Text);
        Assert.Equal(1, matches[0].Score, 6);
        Assert.Empty(await memory.SearchAsync("a", "east", 5, 0.1, "two"));
        await memory.DeleteAsync("a");
        Assert.Empty(await memory.ListAsync("a"));
        Assert.Single(await memory.ListAsync("other"));
    }

    [Fact]
    public async Task SqlitePersistsOnlyNewIndexesAndIsolatesOwners()
    {
        var directory = Path.Combine(Path.GetTempPath(), "af-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var legacy = Path.Combine(directory, "SemanticKernelDb.db");
        await File.WriteAllTextAsync(legacy, "legacy bytes must remain intact");
        try
        {
            var config = Config(directory);
            var first = new MemoryService(config, new FakeEmbeddings()) { Storage = MemoryStorage.Sqlite };
            await first.AddAsync("a", "east");
            var restored = new MemoryService(config, new FakeEmbeddings()) { Storage = MemoryStorage.Sqlite, Owner = first.Owner };
            Assert.Single(await restored.ListAsync("a"));
            var other = new MemoryService(config, new FakeEmbeddings()) { Storage = MemoryStorage.Sqlite };
            Assert.Empty(await other.ListAsync("a"));
            await restored.DeleteAsync("a");
            Assert.Empty(await first.ListAsync("a"));
            Assert.Equal("legacy bytes must remain intact", await File.ReadAllTextAsync(legacy));
        }
        finally { Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); Directory.Delete(directory, true); }
    }

    [Fact]
    public void EmbeddingMismatchesFailRatherThanReturnFalseMatches()
    {
        Assert.Throws<ArgumentException>(() => MemoryService.Cosine([1], [1, 0]));
        Assert.Throws<ArgumentException>(() => MemoryService.Cosine([0, 0], [1, 0]));
        Assert.Throws<InvalidOperationException>(() => MemoryService.Rank(
            [new MemoryItem("x", "text", [1, 0], "old")], [1, 0], "new", 1, 0));
    }

    [Theory]
    [InlineData("")]
    [InlineData("# Heading\n\nA simple paragraph with some words.\n\nAnother paragraph.")]
    [InlineData("Unicode: \u4e16\u754c \U0001f600 \u00e9. Long words abcdefghijklmnopqrstuvwxyz repeated several times.")]
    public void ChunkingPreservesTextAndEnforcesMeasuredBudgets(string text)
    {
        var tokenizer = new TokenService(Config());
        var chunks = tokenizer.Chunk(text, 5, 10, 0);
        Assert.Equal(text, string.Concat(chunks.Select(c => c.Text)));
        Assert.All(chunks, chunk => Assert.InRange(tokenizer.Count(chunk.Text), 1, 10));
        Assert.DoesNotContain(chunks, chunk => chunk.Text.Contains('\ufffd'));
    }

    [Fact]
    public void ChunkOverlapIsBoundedAndMakesForwardProgress()
    {
        var tokenizer = new TokenService(Config());
        var text = string.Join(" ", Enumerable.Range(0, 200).Select(i => "word" + i));
        var chunks = tokenizer.Chunk(text, 10, 20, 5);
        Assert.InRange(chunks.Count, 2, 200);
        Assert.All(chunks, chunk => Assert.InRange(chunk.Tokens, 1, 20));
        Assert.Throws<ArgumentException>(() => tokenizer.Chunk(text, 10, 20, 20));
    }

    [Fact]
    public async Task UnsupportedTokenOptionsFailBeforeNetworkCalls()
    {
        var config = Config();
        config.Current.Provider = "OpenRouter";
        config.Current.SupportsLogprobs = false;
        var tokens = new TokenService(config);
        await Assert.ThrowsAsync<NotSupportedException>(() => tokens.CompleteWithOptionsAsync("test", 1, 1, 10, true));
        await Assert.ThrowsAsync<NotSupportedException>(() => tokens.CompleteWithOptionsAsync("test", 1, 1, 10, false, new Dictionary<int, int> { [1] = 10 }));
    }

    [Fact]
    public void SavedAgentDefinitionsRoundTripAndLegacyHistoryValuesWork()
    {
        var original = new AgentDefinition { PluginNames = ["TimePlugin"], ExcludedFunctionNames = ["TimePlugin_UtcNow"], IsPrimary = true, ChatHistoryType = HistoryVisibility.Self };
        var restored = JsonSerializer.Deserialize<AgentDefinition>(JsonSerializer.Serialize(original))!;
        Assert.Equal(original.Id, restored.Id);
        Assert.Equal(original.ExcludedFunctionNames, restored.ExcludedFunctionNames);
        var legacy = JsonSerializer.Deserialize<AgentDefinition>("""{"Name":"Legacy","GptModel":"Gpt4","ChatHistoryType":1}""")!;
        legacy.Validate();
        Assert.Null(legacy.GptModel);
        Assert.Equal(HistoryVisibility.Self, legacy.ChatHistoryType);
    }

    [Fact]
    public void DisplayTranscriptAggregatesStreamingUpdates()
    {
        var state = new ChatComponents.ChatState();
        state.AddUserMessage("hi"); state.AddAssistantMessage("a"); state.UpdateAssistantMessage("b");
        Assert.Equal("ab", state.ChatHistory.Last().Text);
        Assert.Equal(2, state.ChatHistory.Count);
        state.Reset();
        Assert.Empty(state.ChatHistory);
    }

    [Fact]
    public void GalleryCompilesTheExactPublishedSources()
    {
        var paths = Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "Gallery"), "*.cs");
        Assert.Equal(6, paths.Length);
        var references = CompileResources.PortableExecutableReferences;
        foreach (var path in paths)
        {
            var compilation = CSharpCompilation.Create("Sample_" + Path.GetFileNameWithoutExtension(path),
                [CSharpSyntaxTree.ParseText(File.ReadAllText(path))], references,
                new CSharpCompilationOptions(OutputKind.ConsoleApplication, usings: ["System", "System.Collections.Generic", "System.Linq", "System.Threading.Tasks"]));
            using var stream = new MemoryStream();
            var result = compilation.Emit(stream);
            Assert.True(result.Success, path + "\n" + string.Join("\n", result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)));
        }
    }

    private static async Task Drain(IAsyncEnumerable<AgentResponseUpdate> stream) { await foreach (var _ in stream) { } }
}

internal sealed class FakeChatClient : IChatClient
{
    public List<List<ChatMessage>> Requests { get; } = [];
    public string? Tool { get; init; }
    public TaskCompletionSource? Started { get; init; }
    public TaskCompletionSource? Release { get; init; }
    public string Answer { get; init; } = "reply";
    public Queue<string>? Answers { get; init; }
    public Action? OnRequest { get; init; }
    public Exception? Failure { get; init; }
    public ChatOptions? LastOptions { get; private set; }
    public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        Requests.Add(messages.ToList());
        LastOptions = options;
        OnRequest?.Invoke();
        if (Failure is not null) throw Failure;
        return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, Answers is { Count: > 0 } ? Answers.Dequeue() : Answer)));
    }
    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages,
        ChatOptions? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var list = messages.ToList();
        Requests.Add(list);
        LastOptions = options;
        OnRequest?.Invoke();
        if (Failure is not null) throw Failure;
        Started?.TrySetResult();
        if (Release is not null) await Release.Task.WaitAsync(cancellationToken);
        if (Tool is not null && !list.Any(m => m.Contents.Any(c => c is ToolResultContent)))
            yield return new ChatResponseUpdate { Role = ChatRole.Assistant, Contents = [new FunctionCallContent("call-one", Tool, new Dictionary<string, object?>())], FinishReason = ChatFinishReason.ToolCalls };
        else
            yield return new ChatResponseUpdate(ChatRole.Assistant, Answer) { FinishReason = ChatFinishReason.Stop };
    }
    public object? GetService(Type serviceType, object? serviceKey = null) => serviceKey is null && serviceType.IsInstanceOfType(this) ? this : null;
    public void Dispose() { }
}

internal sealed class FakeEmbeddings : IEmbeddingGenerator<string, Embedding<float>>
{
    public Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(IEnumerable<string> values, EmbeddingGenerationOptions? options = null, CancellationToken cancellationToken = default) =>
        Task.FromResult(new GeneratedEmbeddings<Embedding<float>>(values.Select(v => new Embedding<float>(v == "east" ? new float[] { 1, 0 } : new float[] { 0, 1 }))));
    public object? GetService(Type serviceType, object? serviceKey = null) => null;
    public void Dispose() { }
}
