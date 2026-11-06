using System.Net;
using System.Text;
using System.Text.Json;
using AgentFrameworkLibrary;
using AgentFrameworkLibrary.Services;
using AgentFrameworkOrchestration;
using AgentToolComponents;
using AgentToolComponents.Components;
using AgentToolComponents.Models;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AgentFramework.Tests;

public sealed class FeatureTests
{
    private static RuntimeConfiguration Config(Dictionary<string, string?>? values = null) =>
        new(new ConfigurationBuilder().AddInMemoryCollection(values ?? []).Build());

    private static ToolCatalog Catalog(FakeChatClient? client = null)
    {
        var config = Config(); var modal = new AskUserService();
        return new(config, new ApiTools(new FakeHttpFactory(new FakeHandler()), config), new CompilerService(),
            new ScriptService(), new AskUserPlugin(modal), new BlazorReplPlugin(new RazorSnippetCompiler(), modal),
            client is null ? null : _ => client);
    }

    [Fact]
    public async Task CatalogSchemasAndTypedMathWorkWithoutAModel()
    {
        var catalog = Catalog();
        var tool = catalog.Tools.Single(t => t.Name == "MathApiPlugin_Multiply");
        Assert.Equal("number", tool.Function.JsonSchema.GetProperty("properties").GetProperty("a").GetProperty("type").GetString());
        Assert.Equal(42d, Assert.IsType<JsonElement>(await tool.Function.InvokeAsync(new AIFunctionArguments { ["a"] = 6d, ["b"] = 7d })).GetDouble());
        Assert.All(catalog.Tools.Where(t => t.Group is "CodeExecutorPlugin" or "BlazorReplPlugin"), t => Assert.True(t.RequiresApproval));
        Assert.Throws<InvalidOperationException>(() => catalog.Resolve(new AgentDefinition { PluginNames = ["Retired"] }, new()));
        Assert.Throws<NotSupportedException>(() => catalog.Resolve(new AgentDefinition { PluginNames = ["MathApiPlugin"] }, new() { SupportsTools = false }));
        Assert.Equal(2, catalog.Resolve(new AgentDefinition { PluginNames = ["MathApiPlugin"], ExcludedFunctionNames = ["Multiply"] }, new()).Count);
        Assert.Throws<InvalidOperationException>(() => catalog.Resolve(new AgentDefinition { ExcludedFunctionNames = ["not-a-function"] }, new()));
    }

    [Fact]
    public async Task PromptBindingUsesDefaultsClockAndExplicitContext()
    {
        var client = new FakeChatClient();
        var tool = Catalog(client).Tools.Single(t => t.Name == "ChatPlugin_ChatGPT");
        await tool.Function.InvokeAsync(new AIFunctionArguments { ["input"] = "$5 \\ dollars", ["context"] = "Source text", ["firstName"] = "Example" });
        var prompt = client.Requests.Single().Last().Text;
        Assert.Contains("Source text", prompt);
        Assert.Contains("Example", prompt);
        Assert.Contains("$5 \\ dollars", prompt);
        Assert.Contains(DateTime.UtcNow.ToString("yyyy-MM-dd"), prompt);
        Assert.DoesNotContain("{{", prompt);
    }

    [Theory]
    [InlineData(true, 1)]
    [InlineData(false, 0)]
    public async Task WorkflowApprovalsResumeWithoutBypassingTheHuman(bool approve, int expected)
    {
        var calls = 0; var decisions = 0;
        var tool = new ApprovalRequiredAIFunction(AIFunctionFactory.Create(() => { calls++; return "done"; }, "write"));
        var workflow = GroupWorkflow.Build([AgentFactory.Create(new FakeChatClient { Tool = "write" }, new(), [tool])], GroupTransition.Sequential, 1, "");
        await Drain(GroupWorkflow.RunAsync(workflow, "write", (request, token) =>
        {
            Assert.Equal(0, calls); decisions++; return Task.FromResult(approve);
        }));
        Assert.Equal(expected, calls); Assert.Equal(1, decisions);
    }

    [Fact]
    public async Task WorkflowPromptSelectionIsSchemaConstrainedAndBounded()
    {
        var selector = new FakeChatClient { Answers = new Queue<string>(["""{"index":1}""", """{"index":0}"""]) };
        var a = new FakeChatClient(); var b = new FakeChatClient();
        var workflow = GroupWorkflow.Build([AgentFactory.Create(a, new() { Name = "A" }, []),
            AgentFactory.Create(b, new() { Name = "B" }, [])], GroupTransition.PromptBased, 2, "", selector);
        await Drain(GroupWorkflow.RunAsync(workflow, "begin", (_, _) => Task.FromResult(false)));
        Assert.Single(a.Requests); Assert.Single(b.Requests);
        Assert.IsType<ChatResponseFormatJson>(selector.LastOptions?.ResponseFormat);
    }

    [Fact]
    public async Task InvalidWorkflowSelectionFailsInsteadOfPickingADefault()
    {
        var workflow = GroupWorkflow.Build([AgentFactory.Create(new FakeChatClient(), new(), [])],
            GroupTransition.PromptBased, 2, "", new FakeChatClient { Answer = """{"index":9}""" });
        await Assert.ThrowsAnyAsync<Exception>(() => Drain(GroupWorkflow.RunAsync(workflow, "begin", (_, _) => Task.FromResult(false))));
    }

    [Fact]
    public async Task HubAndSpokeVisitsThePrimaryBetweenOtherParticipants()
    {
        var order = new List<string>();
        var agents = new[] { "A", "B", "C" }.Select(name => AgentFactory.Create(
            new FakeChatClient { OnRequest = () => order.Add(name) }, new() { Name = name }, [])).Cast<AIAgent>().ToList();
        await Drain(GroupWorkflow.RunAsync(GroupWorkflow.Build(agents, GroupTransition.HubAndSpoke, 5, ""), "begin", (_, _) => Task.FromResult(false)));
        Assert.Equal(["A", "B", "A", "C", "A"], order);
    }

    [Fact]
    public async Task StopTextTerminatesOnAssistantOutputNotTheInitialPrompt()
    {
        var a = new FakeChatClient { Answer = "[STOP]" };
        var b = new FakeChatClient();
        var workflow = GroupWorkflow.Build([AgentFactory.Create(a, new() { Name = "A" }, []),
            AgentFactory.Create(b, new() { Name = "B" }, [])], GroupTransition.Sequential, 9, "[STOP]");
        await Drain(GroupWorkflow.RunAsync(workflow, "Please finish with [STOP].", (_, _) => Task.FromResult(false)));
        Assert.Single(a.Requests); Assert.Empty(b.Requests);
    }

    [Fact]
    public async Task HumanParticipantWaitsForAnActualResponseAndSupportsCancellation()
    {
        var waiting = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var answer = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var human = new HumanParticipantClient("Reviewer", (prompt, token) => { waiting.SetResult(); return answer.Task.WaitAsync(token); });
        var workflow = GroupWorkflow.Build([AgentFactory.Create(human, new() { Name = "Reviewer" }, [])], GroupTransition.Sequential, 1, "");
        var run = Drain(GroupWorkflow.RunAsync(workflow, "begin", (_, _) => Task.FromResult(false)));
        await waiting.Task; Assert.False(run.IsCompleted); answer.SetResult("human answer"); await run;
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => human.GetResponseAsync([new ChatMessage(ChatRole.User, "again")], cancellationToken: cancellation.Token));
    }

    [Fact]
    public async Task CancellingAHumanWorkflowCannotReportSuccessfulCompletion()
    {
        var waiting = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var answer = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var human = new HumanParticipantClient("Reviewer", (prompt, token) => { waiting.TrySetResult(); return answer.Task.WaitAsync(token); });
        var workflow = GroupWorkflow.Build([AgentFactory.Create(human, new() { Name = "Reviewer" }, [])], GroupTransition.Sequential, 1, "");
        using var cancellation = new CancellationTokenSource();
        var run = Drain(GroupWorkflow.RunAsync(workflow, "begin", (_, _) => Task.FromResult(false), cancellation.Token));
        await waiting.Task; cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);
    }

    [Fact]
    public async Task CancelledApprovalsNeverExecuteAndResetExpiresThem()
    {
        var calls = 0;
        var tool = new ApprovalRequiredAIFunction(AIFunctionFactory.Create(() => ++calls, "write"));
        await using var conversation = new AgentConversation(AgentFactory.Create(new FakeChatClient { Tool = "write" }, new(), [tool]));
        await Drain(conversation.RunAsync("write"));
        var request = Assert.Single(conversation.PendingApprovals);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Drain(conversation.RunAsync("bypass")));
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Drain(conversation.RespondAsync(request.RequestId, true, cancellation.Token)));
        await conversation.ResetAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => Drain(conversation.RespondAsync(request.RequestId, true)));
        Assert.Equal(0, calls);
    }

    [Fact]
    public async Task CancelledStreamsReleaseTheirSessionGate()
    {
        var waiting = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var conversation = new AgentConversation(AgentFactory.Create(new FakeChatClient { Started = waiting, Release = release }, new(), []));
        using var cancellation = new CancellationTokenSource();
        var run = Drain(conversation.RunAsync("wait", cancellation.Token));
        await waiting.Task; cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);
        await conversation.ResetAsync();
        release.SetResult();
        await Drain(conversation.RunAsync("fresh"));
    }

    [Fact]
    public async Task HttpProviderMapsEndpointModelAndGenerationOptions()
    {
        var handler = new FakeHandler { Body = """{"id":"test","object":"chat.completion","created":0,"model":"test-model","choices":[{"index":0,"message":{"role":"assistant","content":"answer"},"finish_reason":"stop"}],"usage":{"prompt_tokens":1,"completion_tokens":1,"total_tokens":2}}""" };
        using var http = new HttpClient(handler);
        var profile = new ModelSettings { Provider = "OpenRouter", ApiKey = "test-key-not-a-secret", Endpoint = "https://provider.invalid/api/v1", Model = "test-model" };
        using var chat = profile.CreateClient(http).GetChatClient(profile.Model).AsIChatClient();
        Assert.Equal("answer", (await chat.GetResponseAsync("hello", new ChatOptions { Temperature = 0.2f, MaxOutputTokens = 7 })).Text);
        Assert.Equal("https://provider.invalid/api/v1/chat/completions", handler.Uri?.ToString());
        using var body = JsonDocument.Parse(handler.RequestBody!);
        Assert.Equal("test-model", body.RootElement.GetProperty("model").GetString());
        Assert.Equal(0.2, body.RootElement.GetProperty("temperature").GetDouble(), 5);
        Assert.Equal(7, body.RootElement.TryGetProperty("max_completion_tokens", out var limit) ? limit.GetInt32() : body.RootElement.GetProperty("max_tokens").GetInt32());
    }

    [Fact]
    public void TokenOptionsMapDirectlyToNativeOpenAIOptions()
    {
        var config = Config(); config.Current.SupportsLogprobs = true;
        var options = new TokenService(config).CreateCompletionOptions(0.3f, 0.8f, 20, true, new Dictionary<int, int> { [42] = -90 });
        Assert.True(options.IncludeLogProbabilities); Assert.Equal(5, options.TopLogProbabilityCount);
        Assert.Equal(20, options.MaxOutputTokenCount); Assert.Equal(-90, options.LogitBiases[42]);
        Assert.Throws<ArgumentOutOfRangeException>(() => new TokenService(config).CreateCompletionOptions(float.NaN, 1, 10, false));
    }

    [Fact]
    public async Task NativeLogProbabilityRequestAndResponseShapesArePreserved()
    {
        var config = Config(); config.Current.SupportsLogprobs = true;
        var handler = new FakeHandler { Body = """{"id":"test","object":"chat.completion","created":0,"model":"gpt-4.1-mini","choices":[{"index":0,"message":{"role":"assistant","content":"x"},"finish_reason":"stop","logprobs":{"content":[{"token":"x","logprob":-0.1,"bytes":[120],"top_logprobs":[{"token":"x","logprob":-0.1,"bytes":[120]}]}]}}],"usage":{"prompt_tokens":1,"completion_tokens":1,"total_tokens":2}}""" };
        using var http = new HttpClient(handler);
        var settings = new ModelSettings { ApiKey = "test-key-not-a-secret", Endpoint = "https://provider.invalid/v1" };
        var options = new TokenService(config).CreateCompletionOptions(0.3f, 0.8f, 20, true, new Dictionary<int, int> { [42] = -90 });
        var result = (await settings.CreateClient(http).GetChatClient(settings.Model)
            .CompleteChatAsync([new OpenAI.Chat.UserChatMessage("test")], options)).Value;
        Assert.Equal("x", Assert.Single(result.ContentTokenLogProbabilities).Token);
        using var body = JsonDocument.Parse(handler.RequestBody!);
        Assert.True(body.RootElement.GetProperty("logprobs").GetBoolean());
        Assert.Equal(5, body.RootElement.GetProperty("top_logprobs").GetInt32());
        Assert.Equal(-90, body.RootElement.GetProperty("logit_bias").GetProperty("42").GetInt32());
    }

    [Fact]
    public async Task TypedHttpToolsPreserveJsonAndPropagateFailures()
    {
        var handler = new FakeHandler { Body = """{"items":[{"title":"result","link":"https://example.org"}]}""" };
        var config = Config(new() { ["Google:ApiKey"] = "test-key-not-a-secret", ["Google:SearchEngineId"] = "test-engine" });
        var apis = new ApiTools(new FakeHttpFactory(handler), config);
        Assert.Single((await apis.SearchAsync("a & b")).GetProperty("items").EnumerateArray());
        Assert.DoesNotContain("test-key", handler.Uri!.ToString());
        Assert.Contains("q=a%20%26%20b", handler.Uri.OriginalString);
        Assert.Equal("test-key-not-a-secret", handler.ApiKeyHeader);
        await Assert.ThrowsAsync<InvalidOperationException>(() => new ApiTools(new FakeHttpFactory(handler), Config()).SearchAsync("query"));
        await Assert.ThrowsAsync<HttpRequestException>(() => new ApiTools(new FakeHttpFactory(new FakeHandler { Status = HttpStatusCode.BadGateway }), config).WikipediaAsync("query"));
        await Assert.ThrowsAsync<ArgumentException>(() => apis.ReadPageAsync("http://localhost/private"));
    }

    [Fact]
    public void BrowserConfigurationImportsOriginalProtectedPayloadShape()
    {
        var legacy = JsonSerializer.Deserialize<JsonElement>("""[{"Name":"ApiKey","Value":"test-key-not-a-secret"},{"Name":"ModelId","Value":"test-model"}]""");
        var settings = Config().ReadBrowserSettings("OpenRouter", legacy);
        Assert.Equal("OpenRouter", settings.Provider); Assert.Equal("test-model", settings.Model);
        Assert.Equal("test-key-not-a-secret", settings.ApiKey);
    }

    [Fact]
    public async Task UserPromptsHandleRefusalInvalidInputAndCancellation()
    {
        var service = new AskUserService(); var plugin = new AskUserPlugin(service);
        service.CloseSelf();
        await Assert.ThrowsAsync<ArgumentException>(() => plugin.AskUserAsync("question", "wrong"));
        var refused = plugin.AskUserAsync("question"); service.Close(false);
        Assert.Equal("user refused to answer!", await refused);
        using var cancellation = new CancellationTokenSource();
        var prompt = plugin.AskUserAsync("question", cancellationToken: cancellation.Token);
        cancellation.Cancel(); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => prompt);
        var next = service.OpenAsync<SimpleInput>(); service.Close(false); await next;
    }

    [Fact]
    public async Task CompilersCaptureEachRunAndRestoreOutputAfterFailure()
    {
        var compiler = new CompilerService();
        Assert.Contains("first", await compiler.SubmitCode("""System.Console.WriteLine("first");""", CompileResources.PortableExecutableReferences));
        var second = await compiler.SubmitCode("""System.Console.WriteLine("second");""", CompileResources.PortableExecutableReferences);
        Assert.Contains("second", second); Assert.DoesNotContain("first", second);
        var script = new ScriptService();
        await Assert.ThrowsAsync<InvalidOperationException>(() => script.EvaluateAsync("""throw new System.InvalidOperationException("expected");"""));
        Assert.Contains("after", await script.EvaluateAsync("""System.Console.WriteLine("after");"""));
    }

    [Fact]
    public async Task MultiFileBlazorCompilationPreservesTheExistingRepl()
    {
        var result = await new RazorSnippetCompiler().CompileComponentsAsync(new Dictionary<string, string>
        {
            ["_Imports.razor"] = "@using Microsoft.AspNetCore.Components",
            ["Demo.razor"] = "<h1>Demo</h1><Counter />",
            ["Counter.razor"] = "<p>Counter</p>"
        }, "Demo");
        Assert.DoesNotContain(result.Diagnostics, d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error);
        Assert.NotNull(result.TryGetComponentType("Demo"));
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => new RazorSnippetCompiler().CompileComponentsAsync(
            new Dictionary<string, string> { ["Broken.razor"] = "@code { private int x = ; }" }, "Broken"));
        Assert.Contains("Blazor compilation failed", error.Message);
    }

    [Theory]
    [InlineData(HistoryVisibility.Self)]
    [InlineData(HistoryVisibility.LastMessage)]
    public async Task WorkflowHistoryModesReachTheModelAsConfigured(HistoryVisibility visibility)
    {
        var a = new FakeChatClient { Answer = "first speaker" };
        var b = new FakeChatClient { Answer = "other speaker" };
        var workflow = GroupWorkflow.Build([
            AgentFactory.Create(a, new() { Name = "A", ChatHistoryType = visibility }, []),
            AgentFactory.Create(b, new() { Name = "B" }, [])
        ], GroupTransition.Sequential, 3, "");
        await Drain(GroupWorkflow.RunAsync(workflow, "initial question", (_, _) => Task.FromResult(false)));
        var input = a.Requests.Last();
        if (visibility == HistoryVisibility.Self) Assert.DoesNotContain(input, m => m.Text == "other speaker");
        else
        {
            Assert.Contains(input, m => m.Text == "other speaker");
            Assert.DoesNotContain(input, m => m.Text == "initial question");
        }
    }

    [Fact]
    public async Task WorkflowFailuresRetainTheOriginalError()
    {
        var workflow = GroupWorkflow.Build([AgentFactory.Create(new FakeChatClient
        { Failure = new InvalidOperationException("expected model failure") }, new(), [])], GroupTransition.Sequential, 1, "");
        var error = await Assert.ThrowsAnyAsync<Exception>(() => Drain(GroupWorkflow.RunAsync(workflow, "begin", (_, _) => Task.FromResult(false))));
        Assert.Contains("expected model failure", error.ToString());
    }

    [Fact]
    public void CircuitProfilesAndMemoriesAreScoped()
    {
        var services = new ServiceCollection().AddSingleton<IConfiguration>(new ConfigurationBuilder().Build()).AddAgentFrameworkServices();
        using var provider = services.BuildServiceProvider();
        using var a = provider.CreateScope(); using var b = provider.CreateScope();
        var first = a.ServiceProvider.GetRequiredService<RuntimeConfiguration>();
        var second = b.ServiceProvider.GetRequiredService<RuntimeConfiguration>();
        first.Current.Model = "a-only";
        Assert.NotEqual(first.Current.Model, second.Current.Model);
        Assert.NotEqual(a.ServiceProvider.GetRequiredService<MemoryService>().Owner, b.ServiceProvider.GetRequiredService<MemoryService>().Owner);
    }

    [Fact]
    public async Task GeneratedTeamsCannotGrantThemselvesToolsModelsOrHumanIdentity()
    {
        var json = """[{"Name":"Writer","Description":"Write","Instructions":"Write","IsPrimary":true,"PluginNames":["CodeExecutorPlugin"],"GptModel":"expensive-model","IsUserProxy":true},{"Name":"Reviewer","Description":"Review","Instructions":"Review","IsPrimary":false}]""";
        var config = Config(); var memory = new MemoryService(config, new FakeEmbeddings());
        var core = new CoreAgentService(config, Catalog(), memory, _ => new FakeChatClient { Answer = json });
        var team = await core.GenerateParticipantsAsync("task");
        Assert.Equal(2, team.Count);
        Assert.All(team, a => { Assert.Empty(a.PluginNames); Assert.Null(a.GptModel); Assert.False(a.IsUserProxy); });
        var invalid = new CoreAgentService(config, Catalog(), memory, _ => new FakeChatClient { Answer = "[]" });
        await Assert.ThrowsAsync<InvalidDataException>(() => invalid.GenerateParticipantsAsync("task"));
    }

    [Fact]
    public async Task ClusteringUsesRealHdbscanAndGeneratesSummariesThroughTheAgent()
    {
        var config = Config(); config.Current.EmbeddingDimensions = 2;
        var memory = new MemoryService(config, new FakeEmbeddings());
        for (var i = 0; i < 10; i++)
        {
            await memory.PutAsync("clusters", new MemoryItem("a" + i, "east", [1f, 0.001f * i], memory.ModelKey));
            await memory.PutAsync("clusters", new MemoryItem("b" + i, "north", [0.001f * i, 1f], memory.ModelKey));
        }
        var core = new CoreAgentService(config, Catalog(), memory, _ => new FakeChatClient { Answer = "Cluster summary" });
        var groups = await core.ClusterAsync("clusters", 2, 3, "Cosine");
        Assert.Equal(20, groups.Sum(g => g.Items.Count));
        Assert.True(groups.Count(g => g.Cluster != -1) >= 2);
        Assert.All(groups.Where(g => g.Cluster != -1), g => Assert.Equal("Cluster summary", g.Summary));
        Assert.Equal(["clusters"], await memory.CollectionsAsync());
    }

    [Fact]
    public async Task DisplayedNonBilledSamplesActuallyRun()
    {
        var compiler = new CompilerService();
        foreach (var sample in new[] { "HumanInput", "Retrieval" })
        {
            var source = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Gallery", sample + ".cs"));
            var output = await compiler.SubmitCode(source, CompileResources.PortableExecutableReferences);
            Assert.False(string.IsNullOrWhiteSpace(output));
        }
    }

    private static async Task Drain(IAsyncEnumerable<AgentResponseUpdate> stream) { await foreach (var _ in stream) { } }
}

internal sealed class FakeHandler : HttpMessageHandler
{
    public string Body { get; init; } = "{}";
    public HttpStatusCode Status { get; init; } = HttpStatusCode.OK;
    public Uri? Uri { get; private set; }
    public string? RequestBody { get; private set; }
    public string? ApiKeyHeader { get; private set; }
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Uri = request.RequestUri;
        RequestBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        ApiKeyHeader = request.Headers.TryGetValues("X-Goog-Api-Key", out var values) ? values.Single() : null;
        return new HttpResponseMessage(Status) { Content = new StringContent(Body, Encoding.UTF8, "application/json") };
    }
}

internal sealed class FakeHttpFactory(HttpMessageHandler handler) : IHttpClientFactory
{
    public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
}
