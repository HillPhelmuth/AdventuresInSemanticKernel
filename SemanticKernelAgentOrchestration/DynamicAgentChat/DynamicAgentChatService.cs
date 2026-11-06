using System.Runtime.CompilerServices;
using System.Text.Json;
using ChatComponents;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using SemanticKernelAgentOrchestration.Models;
using SemanticKernelAgentOrchestration.Plugins;

namespace SemanticKernelAgentOrchestration.DynamicAgentChat;

/// <summary>
/// A chat that hands the conversation between several Agent Framework agents. Each agent is a
/// <see cref="ChatClientAgent"/> that gets transition tools it can call to pass control onward.
/// </summary>
public class DynamicAgentChatService
{
    private ChatContext _chatContext;
    private List<ChatAgent> _agents = [];
    private readonly IChatClient _chatClient;
    public DynamicAgentChatService(List<AgentProxy> agents, ChatContext chatContext, IChatClient chatClient)
    {
        _chatContext = chatContext;
        foreach (var agent in agents)
        {
            agent.Name = agent.Name.Replace(" ", "_");
            var transitionPlugin = new TransitionPlugin(_chatContext);
            var interactiveAgent = new InteractiveAgent(agent, chatClient);
            interactiveAgent.AddTools(transitionPlugin.AsToolPlugin().Tools);
            _agents.Add(interactiveAgent);
        }
        _chatClient = chatClient;
    }

    public async IAsyncEnumerable<string> ExecuteChat(string input, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        _chatContext.ChatMessages.Add(new ChatMessage(ChatRole.User, input));
        _chatContext.ActiveAgent ??= _agents.First();
        while (true)
        {
            var nextAgent = await SelectNextAgent(_chatContext.ActiveAgent, _chatContext.ChatMessages);
            _chatContext.ActiveAgent = nextAgent;
            var appendToSysPrompt = $"""
                                    If you have completed your Task, please indicate so by invoking 'TransitionToNextAgent' after completing your response.
                                    If the user query requires the attention of another agent, please indicate so by invoking 'IntentTransition'. 
                                    
                                    ## Other Agents
                                    
                                    {string.Join("\n", _agents.Where(x => x.Name != nextAgent.Name).Select(x => x.ToString()))}
                                    """;
            var systemPromptOverride = nextAgent.SystemPromptOverride(appendToSysPrompt);
            Console.WriteLine(systemPromptOverride);
            var aiAgent = nextAgent.BuildAgent(new ChatOptions { Temperature = nextAgent.Temperature }, systemPromptOverride);
            var messages = GetAgentMessages(nextAgent).Select(m => { var c = m.Clone(); c.AuthorName = ChatAgent.EnsurePattern(c.AuthorName); return c; }).ToList();
            var text = new System.Text.StringBuilder();
            yield return $"<strong>{nextAgent.Name}:</strong><br/>";
            await foreach (var update in aiAgent.RunStreamingAsync(messages, cancellationToken: cancellationToken))
            {
                if (string.IsNullOrEmpty(update.Text)) continue;
                text.Append(update.Text);
                yield return update.Text;
            }
            _chatContext.ChatMessages.Add(new ChatMessage(ChatRole.Assistant, text.ToString()) { AuthorName = nextAgent.Name });
            if (!ShouldTranstion())
            {
                yield return "[DONE]";
                break;
            }

            yield return "<hr/>";
        }
    }
    private List<ChatMessage> GetAgentMessages(ChatAgent agent)
    {
        var agentHistoryType = agent.ChatHistoryType;
        return agentHistoryType switch
        {
            ChatHistoryType.SelfAndUser => _chatContext.ChatMessages.Where(x => x.Role == ChatRole.Assistant && x.AuthorName == agent.Name || x.Role == ChatRole.User).ToList(),
            ChatHistoryType.All => _chatContext.ChatMessages.ToList(),
            ChatHistoryType.None => [_chatContext.ChatMessages.Last()],
            _ => throw new ArgumentOutOfRangeException()
        };
    }
    private bool ShouldTranstion()
    {
        if (_chatContext.ChatMessages.TakeLast(2).All(x => x.Role == ChatRole.Assistant))
        {
            Console.WriteLine("Last two messages are from agents");
            return false;
        }

        return _chatContext.IsTranstionNext || _chatContext.IsIntentTranstion;
    }

    public async Task<ChatAgent> SelectNextAgent(ChatAgent currentAgent, List<ChatMessage> messages)
    {
        if (_chatContext.IsTranstionNext)
        {
            // Move to the next agent in the list, wrapping around to the first.
            var nextAgentIndex = _agents.IndexOf(_agents.Find(x => x.Name == currentAgent.Name)!) + 1;
            if (nextAgentIndex >= _agents.Count)
            {
                nextAgentIndex = 0;
            }
            _chatContext.IsTranstionNext = false;
            return _agents[nextAgentIndex];
        }
        if (_chatContext.IsIntentTranstion)
        {
            var chatContextList = messages.Take(4).Select(x => $"Agent: {x.AuthorName ?? "User"}\n\nContent: {x.Text}");
            var chatContext = string.Join("\n\n", chatContextList);
            var agents = string.Join("\n\n", _agents.Select(x => x.ToString()));
            var prompt = NextAgentIntentPrompt.Replace("{{ $chatContext }}", chatContext).Replace("{{ $agents }}", agents);
            // Structured output: the chat client deserializes the model's reply straight into IntentTransition.
            var response = await _chatClient.GetResponseAsync<IntentTransition>(prompt);
            var nextAgent = _agents.FirstOrDefault(x => x.Name == response.Result?.Agent);
            _chatContext.IsIntentTranstion = false;
            return nextAgent ?? _agents.First();
        }

        return currentAgent;
    }

    private const string NextAgentIntentPrompt =
        """
        ## Instructions
        You are the adminstrator of an Agent chat group helping a user with a task. Your objective is to determine the intent of the user first, then determine which agent is best able to fulfill the user's request based on that intent. Use the latest Chat Context and the Agent List to make both determinations.
        
        ## Chat Context
        
        {{ $chatContext }}
        
        ## Agent List
        
        {{ $agents }}
        
        ## Output format
        
        Output should be in the following json format:
        
        ```json
        {
            "Intent": "{user's intent}",
            "Agent": "{appropriate next agent's name}"
        }
        ```
        """;
}
public class IntentTransition
{
    public string? Intent { get; set; }
    public string? Agent { get; set; }
}
