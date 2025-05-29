using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using SemanticKernelAgentOrchestration.Models;
using SemanticKernelAgentOrchestration.Plugins;
#pragma warning disable SKEXP0010

namespace SemanticKernelAgentOrchestration.DynamicAgentChat;

public class DynamicAgentChatService
{
    private ChatContext _chatContext;
    private List<ChatAgent> _agents = [];
    private readonly Kernel _kernel;
    public DynamicAgentChatService(List<AgentProxy> agents, ChatContext chatContext, Kernel kernel)
    {
        _chatContext = chatContext;
        foreach (var agent in agents)
        {
            agent.Name = agent.Name.Replace(" ", "_");
            var agentKernel = kernel.Clone();
            agentKernel.Plugins.AddRange(agent.Plugins);
            var transitionPlugin = new TransitionPlugin(_chatContext);
            agentKernel.ImportPluginFromObject(transitionPlugin, "TranstionPlugin");
            _agents.Add(new InteractiveAgent(agent, agentKernel));
        }
        _kernel = kernel;
    }

    public async IAsyncEnumerable<string> ExecuteChat(string input, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        
		_chatContext.ChatMessages.Add(new ChatMessageContent(AuthorRole.User, input));
        _chatContext.ActiveAgent ??= _agents.First();
        while (true)
        {
            var nextAgent = await SelectNextAgent(_chatContext.ActiveAgent, _chatContext.ChatMessages);
            _chatContext.ActiveAgent = nextAgent;
            var chatService = nextAgent.Kernel.GetRequiredService<IChatCompletionService>();
            var settings = new OpenAIPromptExecutionSettings { ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions, Temperature = nextAgent.Temperature };
            var appendToSysPrompt = $"""
                                    If you have completed your Task, please indicate so by invoking 'TransitionToNextAgent' after completing your response.
                                    If the user query requires the attention of another agent, please indicate so by invoking 'IntentTransition'. 
                                    
                                    ## Other Agents
                                    
                                    {string.Join("\n", _agents.Where(x => x.Name != nextAgent.Name).Select(x => x.ToString()))}
                                    """;
            var systemPromptOverride = nextAgent.SystemPromptOverride(appendToSysPrompt);
            var promptTemplate = new KernelPromptTemplateFactory();
            var prompt = promptTemplate.Create(new PromptTemplateConfig(systemPromptOverride));
            var finalPrompt = await prompt.RenderAsync(nextAgent.Kernel, cancellationToken: cancellationToken);
            Console.WriteLine(finalPrompt);
            var chatHistory = new ChatHistory(finalPrompt);
            var agentMessages = GetAgentMessages(nextAgent);
            chatHistory.AddRange(agentMessages);
            ChatMessageContent? finalResponse = null;
            yield return $"<strong>{nextAgent.Name}:</strong><br/>";
            await foreach(var response in chatService.GetStreamingChatMessageContentsAsync(chatHistory, settings, nextAgent.Kernel, cancellationToken))
            {
                if (finalResponse is null)
                {
                    finalResponse = new ChatMessageContent(AuthorRole.Assistant, response.Content){AuthorName = nextAgent.Name};

                }
                else
                {
                    finalResponse.Content += response.Content;
                }
                yield return response.Content!;
            }
            _chatContext.ChatMessages.Add(finalResponse);
            if (!ShouldTranstion())
            {
                yield return "[DONE]";
                break;
            }

            yield return "<hr/>";
        }
        
    }
    private List<ChatMessageContent> GetAgentMessages(ChatAgent agent)
    {
        var agentHistoryType = agent.ChatHistoryType;
        return agentHistoryType switch
        {
            ChatHistoryType.SelfAndUser => _chatContext.ChatMessages.Where(x => x.Role == AuthorRole.Assistant && x.AuthorName == agent.Name || x.Role == AuthorRole.User).ToList(),
            ChatHistoryType.All => _chatContext.ChatMessages.ToList(),
            ChatHistoryType.None => [_chatContext.ChatMessages.Last()],
            _ => throw new ArgumentOutOfRangeException()
        };
    }
    private bool ShouldTranstion()
    {
        if (_chatContext.ChatMessages.TakeLast(2).All(x => x.Role == AuthorRole.Assistant))
        {
            Console.WriteLine("Last two messages are from agents");
            return false;
        }

        return _chatContext.IsTranstionNext || _chatContext.IsIntentTranstion;
    }

    public async Task<ChatAgent> SelectNextAgent(ChatAgent currentAgent, List<ChatMessageContent> messages)
    {
        if (_chatContext.IsTranstionNext)
        {
            // If the chat context indicates that the next agent should be transitioned to, return the next agent in the list, otherwise return the first agent in the list
            var nextAgentIndex = _agents.IndexOf(_agents.Find(x => x.Name == currentAgent.Name)) + 1;
            if (nextAgentIndex >= _agents.Count)
            {
                nextAgentIndex = 0;
            }
            _chatContext.IsTranstionNext = false;
            return _agents[nextAgentIndex];
        }
        if (_chatContext.IsIntentTranstion)
        {
            var settings = new OpenAIPromptExecutionSettings { ResponseFormat = typeof(IntentTransition) };
            var chatContextList = messages.Take(4).Select(x => $"Agent: {x.AuthorName ?? "User"}\n\nContent: {x.Content}");
            var chatContext = string.Join("\n\n", chatContextList);
            var agents = string.Join("\n\n", _agents.Select(x => x.ToString()));
            var kernelArgs = new KernelArguments(settings) { ["chatContext"] = chatContext, ["agents"] = agents };
            var responseJson = await _kernel.InvokePromptAsync<string>(NextAgentIntentPrompt, kernelArgs);
            var response = JsonSerializer.Deserialize<IntentTransition>(responseJson);
            var nextAgent = _agents.FirstOrDefault(x => x.Name == response.Agent);
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