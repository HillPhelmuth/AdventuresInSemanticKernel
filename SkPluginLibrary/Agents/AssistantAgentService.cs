using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Experimental.Agents;
using System.Text;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Azure.AI.OpenAI;
using SemanticKernelAgentOrchestration.Models;
using SkPluginLibrary.Agents.Examples;
using SkPluginLibrary.Models.Hooks;

namespace SkPluginLibrary.Agents;

public class AssistantAgentService : IAsyncDisposable
{
    private static readonly List<IAgent> Agents = [];
    //public event Action<AgentMessage>? ChatMessage;
    public event Action<ChatHistory>? ChatHistoryUpdate;
    private ChatHistory _chatHistory = [];
    private AgentProxy? _chatAgent; 
    private string _currentRespondent = "Chat Agent";
    public bool IsRunning => Agents.Count > 0;
    private readonly ILogger<AssistantAgentService> _logger;
    public AssistantAgentService(ILogger<AssistantAgentService> logger)
    {
        _logger = logger;
        logger.LogInformation("Assistant Agent Service Started");
    }
    public async IAsyncEnumerable<string> ExecuteSimpleAgentChatStream(string input, AgentProxy agent, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        _chatAgent = agent;
        var kernel = GenerateKernel(false, agent.Plugins);
        OpenAIPromptExecutionSettings settings = new() { ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions, ChatSystemPrompt = _chatAgent?.SystemPrompt ?? "Assist the user to the best of your ability with the tools available.", Temperature = 1.0 };
        await foreach (var p in ExecuteToolCallChatStream(input, kernel, settings, cancellationToken))
        {
            yield return p;
        }

        ChatHistoryUpdate?.Invoke(_chatHistory);
    }
    public async IAsyncEnumerable<string> ExecuteChatSequence(string input, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (Agents.Count == 0)
        {
            yield return "No agents found!";
            yield break;
        }
        
        var kernel = GenerateKernel();
        OpenAIPromptExecutionSettings settings = new() { ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions, ChatSystemPrompt = _chatAgent?.SystemPrompt ?? "Assist the user to the best of your ability with the tools available.", Temperature = 1.0 };
        await foreach (var p in ExecuteToolCallChatStream(input, kernel, settings, cancellationToken))
        {
            yield return p;
        }

        ChatHistoryUpdate?.Invoke(_chatHistory);
    
    }

    private async IAsyncEnumerable<string> ExecuteToolCallChatStream(string input, Kernel kernel, PromptExecutionSettings settings,
      [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var chat = kernel.GetRequiredService<IChatCompletionService>();
        _chatHistory.AddUserMessage(input);
        var sentRespondant = false;
        await foreach (StreamingChatMessageContent streamingChatMessageContent in chat.GetStreamingChatMessageContentsAsync(_chatHistory, settings, kernel, cancellationToken))
        {
            var update = (OpenAIStreamingChatMessageContent)streamingChatMessageContent;
            var toolCalls = update.ToolCallUpdates;
            if (toolCalls.Any(x => x.FunctionName is not null))
            {
                var respondant = toolCalls.First(x => x.FunctionName is not null).FunctionName.Replace("_Ask", "");
                if (!respondant.Equals(_currentRespondent, StringComparison.InvariantCultureIgnoreCase))
                {
                    
                    yield return $"<em>{respondant}</em>:<br/> ";                   
                    _currentRespondent = respondant;
                }
            }
            var sb = new StringBuilder();
            //if (!sentRespondant)
            //{
            //    sb.AppendLine($"{_currentRespondent}: ");
            //    yield return $"<em>{_currentRespondent}</em>:<br/> ";
            //    sentRespondant = true;
            //}

            if (update.Content is not null)
            {
                sb.Append(update.Content);
                yield return update.Content;
            }
            _chatHistory.AddAssistantMessage(sb.ToString());
        }
    }


    private Kernel GenerateKernel(bool hasAgents = true, List<KernelPlugin>? plugins = null)
    {
        var kernelBuilder = Kernel.CreateBuilder();
        kernelBuilder.Services.AddLogging(builder => builder.AddConsole());

        var kernel = kernelBuilder.AddAIChatCompletion(AIModel.Gpt4O).Build();
        if (hasAgents)
        {
            foreach (var agent in Agents)
            {
                kernel.Plugins.Add(agent.AsPlugin());
            }
        }
        if (plugins is not null)
        {
            kernel.Plugins.AddRange(plugins);
        }
        var functionHook = new FunctionFilterHook();
        functionHook.FunctionInvoking += HandleFunctionInvokingFilter;
        functionHook.FunctionInvoked += HandleFunctionInvokedFilter;
        return kernel;
    }

    private static async Task<IAgent> GenerateAgent(string name, string description, string? instructions = null, List<KernelPlugin>? plugins = null)
    {
        var agentBuilder = new AgentBuilder()
            .WithOpenAIChatCompletion(TestConfiguration.OpenAI.Gpt35ModelId, TestConfiguration.OpenAI.ApiKey)
            .WithName(name)
            .WithDescription(description)
            .WithInstructions(instructions ?? "");
            //.BuildAsync();
        if (plugins is not null)
        {
            agentBuilder.WithPlugins(plugins);
        }
        return Track(await agentBuilder.BuildAsync());
    }
    private void HandleFunctionInvokingFilter(object? sender, FunctionInvocationContext context)
    {
        var function = context.Function;
        Console.WriteLine($"Function Arguments: {context.Arguments.AsJson()}");
        Console.WriteLine($"Function {function.Name} Invoking");
    }
    private void HandleFunctionInvokedFilter(object? sender, FunctionInvocationContext context)
    {
        var function = context.Function;
        Console.WriteLine($"\n---------Function {function.Name} Invoked-----------\nResults:\n{context.Result.Result()}\n----------------------------");
    }
    private static IAgent Track(IAgent agent)
    {
        Console.WriteLine($"Agent {agent.Name} added");
        Agents.Add(agent);

        return agent;
    }
    private static async Task RemoveAgents()
    {
        foreach (var agent in Agents)
        {
            Console.WriteLine($"Agent {agent.Name} removed");
            await agent.DeleteAsync();
        }
    }
    
    public async ValueTask DisposeAsync()
    {
        await RemoveAgents();
    }
}