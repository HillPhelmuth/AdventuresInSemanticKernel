using System.Text;
using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using SemanticKernelAgentOrchestration.Models;
using SkPluginLibrary.Agents.Models;
using SkPluginLibrary.Agents.Models.Events;
using SkPluginLibrary.Services;

namespace SkPluginLibrary.Agents.SkAgents;

/// <summary>
/// A multi-agent group chat built on Agent Framework's group-chat workflow. Who speaks next is decided by a
/// custom <see cref="GroupChatManager"/> (hub-and-spoke, sequential, or chosen by an LLM prompt), and an
/// optional <see cref="UserProxySkAgent"/> puts a human in the conversation.
/// </summary>
public class ChatCompletionGroup
{
    public event EventHandler<SkAgentInputRequestArgs>? AgentInputRequest;

    private readonly List<AIAgent> _activeAgents = [];
    private readonly int _maxIterations;
    private readonly string _stop;
    private readonly GroupTransitionType _transitionType;
    private readonly IChatClient _chatClient;
    public event Action<string>? MessageSent;

    public ChatCompletionGroup(List<AgentProxy> agents, GroupTransitionType transitionType, int maxIterations = 10, string stop = "approve")
    {
        _transitionType = transitionType;
        _maxIterations = maxIterations;
        _stop = stop;
        _chatClient = ChatClientFactory.Create();
        var middleware = new AgentFunctionMiddleware();
        middleware.FunctionInvoking += (_, e) => WriteLine(InvokingExpando(JsonSerializer.Serialize(e.Arguments), e.Function.Name));
        middleware.FunctionInvoked += (_, e) => WriteLine(ResultsExpando(KernelPluginExtensions.ResultToString(e.Result), e.Function.Name));
        _activeAgents.AddRange(agents.Where(x => !x.IsUserProxy).Select(y => CreateAgent(y, middleware)));
        var userProxy = agents.FirstOrDefault(x => x.IsUserProxy);
        if (userProxy is not null)
        {
            var proxy = new UserProxySkAgent(string.IsNullOrEmpty(userProxy.Name) ? "User" : userProxy.Name, userProxy.Description);
            proxy.AgentInputRequest += HandleInput;
            _activeAgents.Add(proxy);
        }
    }

    private AIAgent CreateAgent(AgentProxy proxy, AgentFunctionMiddleware middleware)
    {
        var tools = proxy.Plugins.SelectMany(p => p.Functions
            .Where(f => !proxy.ExcludedFunctionNames.Contains(f.Name)).Select(p.Prefixed)).ToList();
        var agent = CoreKernelService.CreateAgent(_chatClient, proxy.Instructions, tools, proxy.Name.Replace(" ", "_"),
            new ChatOptions { AllowMultipleToolCalls = true }, [middleware]);
        return agent;
    }

    public void HandleInput(object? sender, SkAgentInputRequestArgs e)
    {
        AgentInputRequest?.Invoke(this, e);
    }

    public async Task ExecuteCustomAgentGroup(string initialInput, CancellationToken cancellationToken = default)
    {
        await RunAsync(initialInput, cancellationToken: cancellationToken);
    }

    private void WriteLine(string output)
    {
        MessageSent?.Invoke(output);
    }
    private const string ReviewerName = "ArtDirector";
    private const string ReviewerInstructions =
        """
        You are an art director who has opinions about copywriting born of a love for David Ogilvy.
        The goal is to determine is the given copy is acceptable to print.
        If so, state that it is approved.
        If not, provide insight on how to refine suggested copy without example.
        """;

    private const string CopyWriterName = "Writer";
    private const string CopyWriterInstructions =
        """
        You are a copywriter with ten years of experience and are known for brevity and a dry humor.
        You're laser focused on the goal at hand. Don't waste time with chit chat.
        The goal is to refine and decide on the single best copy as an expert in the field.
        Consider suggestions when refining an idea.
        """;

    public async Task RunExample(string? input = null)
    {
        input ??= "concept: maps made out of egg cartons.";
        var reviewer = CoreKernelService.CreateAgent(_chatClient, ReviewerInstructions, name: ReviewerName);
        var writer = CoreKernelService.CreateAgent(_chatClient, CopyWriterInstructions, name: CopyWriterName);
        var userProxy = new UserProxySkAgent("User");
        userProxy.AgentInputRequest += HandleInput;
        _activeAgents.Clear();
        _activeAgents.AddRange([writer, reviewer, userProxy]);
        await RunAsync(input, [reviewer, userProxy]);
    }

    public async Task RunAsync(string input, IEnumerable<AIAgent>? terminationAgents = null, CancellationToken cancellationToken = default)
    {
        // Participants that are allowed to end the chat by saying the stop word.
        terminationAgents ??= _activeAgents.Any(x => x is UserProxySkAgent) ? [_activeAgents.First(x => x is UserProxySkAgent)] : _activeAgents;
        var terminators = terminationAgents.Select(a => a.Name ?? "").ToHashSet();

        var workflow = AgentWorkflowBuilder
            .CreateGroupChatBuilderWith(agents => new SelectionGroupChatManager(agents, _transitionType, _chatClient, _stop, terminators) { MaximumIterationCount = _maxIterations })
            .AddParticipants(_activeAgents)
            .Build();

        List<ChatMessage> messages = [new ChatMessage(ChatRole.User, input) { AuthorName = "User" }];
        var currentAgent = "";
        var outputNumber = 0;
        await using StreamingRun run = await InProcessExecution.RunStreamingAsync(workflow, messages, cancellationToken: cancellationToken);
        await run.TrySendMessageAsync(new TurnToken(emitEvents: true));
        await foreach (WorkflowEvent evt in run.WatchStreamAsync(cancellationToken))
        {
            if (evt is not AgentResponseUpdateEvent update) continue;
            var author = update.Update.AuthorName ?? update.ExecutorId;
            var isCurrent = author == currentAgent;
            if (!isCurrent)
                outputNumber++;
            var text = update.Update.Text;
            var output = isCurrent ? text : $"<br/>{outputNumber}. **{update.Update.Role} - {author}:**\n {text}";
            currentAgent = author;
            if (string.IsNullOrEmpty(text)) continue;

            WriteLine(output);
        }

        WriteLine("\n_IS COMPLETE:_ True");
        WriteLine("[DONE]");
    }

    private static string InvokingExpando(string result, string name)
    {
        var resultsExpando = $"""

                              <details>
                                <summary>{name} Called</summary>
                                
                                <h5>Arguments</h5>
                                <p>
                                {result}
                                </p>
                              
                                <br/>
                              </details>
                              """;
        return resultsExpando;
    }
    private static string ResultsExpando(string result, string name)
    {
        var resultsExpando = $"""

                              <details>
                                <summary>{name} Function Results</summary>
                                
                                <h5>Results</h5>
                                <p>
                                {result}
                                </p>
                              
                                <br/>
                              </details>
                              """;
        return resultsExpando;
    }
}

/// <summary>
/// Decides who speaks next in a group chat and when it ends. Replaces the old selection / termination strategies.
/// </summary>
internal sealed class SelectionGroupChatManager(IReadOnlyList<AIAgent> agents, GroupTransitionType transitionType,
    IChatClient chatClient, string stop, HashSet<string> terminators) : GroupChatManager
{
    private int _spokeIndex = 1;
    private int _sequentialIndex;

    private const string NextAgentPromptTemplate = """
                                                   You are in a role play game. Carefully read the conversation history and carry on the conversation, always starting with 'From {name}:'.
                                                   The available roles are:
                                                   - {speakerList}

                                                   ### Conversation history

                                                   - {conversationHistory}

                                                   Each message MUST start with 'From name:', e.g:
                                                   From admin:
                                                   //your message//.
                                                   """;

    protected override async ValueTask<AIAgent> SelectNextAgentAsync(IReadOnlyList<ChatMessage> history, CancellationToken cancellationToken = default)
    {
        switch (transitionType)
        {
            case GroupTransitionType.HubAndSpoke:
            {
                var admin = agents[0];
                if (history.Count == 0) return admin;
                var last = agents.FirstOrDefault(x => x.Name == history[^1].AuthorName);
                if (last?.Name != admin.Name) return admin;
                var agent = agents[_spokeIndex];
                _spokeIndex = _spokeIndex + 1 >= agents.Count ? 1 : _spokeIndex + 1;
                return agent;
            }
            case GroupTransitionType.PromptBased:
                return await SelectByPromptAsync(history, cancellationToken);
            default:
            {
                var agent = agents[_sequentialIndex % agents.Count];
                _sequentialIndex++;
                return agent;
            }
        }
    }

    private async ValueTask<AIAgent> SelectByPromptAsync(IReadOnlyList<ChatMessage> history, CancellationToken cancellationToken)
    {
        var admin = agents[0];
        var conversation = string.Join("\n ", history.Select(m => $"From: \n{m.AuthorName}\n### Message\n {m.Text}\n"));
        var speakers = string.Join("\n ", agents.Select(a => $"**Name:** {a.Name}\n **Description:** {a.Description}\n"));
        var prompt = NextAgentPromptTemplate.Replace("{speakerList}", speakers).Replace("{conversationHistory}", conversation);
        try
        {
            var response = await chatClient.GetResponseAsync(prompt, new ChatOptions { Temperature = 0.0f, MaxOutputTokens = 128, StopSequences = [":"] }, cancellationToken);
            var name = response.Text.Length > 5 ? response.Text[5..].Trim() : response.Text.Trim();
            Console.WriteLine("AutoSelectNextAgent: " + name);
            return agents.FirstOrDefault(a => string.Equals(a.Name, name, StringComparison.OrdinalIgnoreCase)) ?? admin;
        }
        catch (TaskCanceledException exception)
        {
            Console.WriteLine(exception.Message);
            return admin;
        }
    }

    protected override async ValueTask<bool> ShouldTerminateAsync(IReadOnlyList<ChatMessage> history, CancellationToken cancellationToken = default)
    {
        if (await base.ShouldTerminateAsync(history, cancellationToken)) return true;
        if (history.Count == 0) return false;
        var last = history[^1];
        // Like the old text termination strategy: a designated participant saying the stop word ends the chat.
        var terminate = terminators.Contains(last.AuthorName ?? "") && last.Text.Contains(stop, StringComparison.OrdinalIgnoreCase);
        Console.WriteLine($"TextTermination: {last.AuthorName} - {terminate}");
        return terminate;
    }
}
