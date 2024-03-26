using System.Text.Json;
using System.Text.Json.Serialization;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using SkPluginLibrary.Agents.Extensions;
using SkPluginLibrary.Agents.Models;
using SkPluginLibrary.Models.Hooks;

namespace SkPluginLibrary.Agents.Group;

public class GroupChat : IGroupChat
{

    public GroupChat(InteractiveAgentBase admin, List<InteractiveAgentBase> agents, TransitionGraph? transitionGraph = null)
    {
        _transitionGraph = transitionGraph;
        InteractiveAgents = agents;
        Admin = admin;
        var functionHook = new FunctionFilterHook();
        functionHook.FunctionInvoking += (sender, context) =>
        {
            Console.WriteLine($"Function Invoking: {context.Function.Name}");
        };
        functionHook.FunctionInvoked += (sender, context) =>
        {
            Console.WriteLine($"Function Invoked: {context.Function.Name}");
        };
        var promptHook = new PromptFilterHook();
        promptHook.PromptRendered += (sender, context) =>
        {
            Console.WriteLine($"Prompt Rendered: {context.RenderedPrompt}");
        };
        promptHook.PromptRendering += (sender, context) =>
        {
            Console.WriteLine($"Prompt Rendering for: {context.Function.Name}");
        };
        Admin.Kernel.FunctionFilters.Add(functionHook);
        Admin.Kernel.PromptFilters.Add(promptHook);
        foreach (var agent in InteractiveAgents)
        {
            agent.Kernel.FunctionFilters.Add(functionHook);
            agent.Kernel.PromptFilters.Add(promptHook);
        }
     
    }
   
    private TransitionGraph? _transitionGraph;
    internal ChatHistory ChatHistory => AgentChatHistory.AsChatHistory();
    public List<AgentMessage> AgentChatHistory { get; set; } = [];
    public List<InteractiveAgentBase> InteractiveAgents { get; }
    public InteractiveAgentBase Admin { get; }
    
   
    private const string NextAgentPromptTemplate = """
        You are in a role play game. Carefully read the conversation history and carry on the conversation, always starting with 'From {name}:'.
        The available roles are:
        - {{$speakerList}}

        ### Conversation history

        - {{$conversationHistory}}

        Each message MUST start with 'From name:', e.g:
        From admin:
        //your message//.
        """;
    public void AddInitializeMessage(AgentMessage message)
    {
        Console.WriteLine($"Agent {message.AgentName} Init message");
        AgentChatHistory.Add(message);
    }
    public Task<List<AgentMessage>> CallAsync(string userInput, int maxRound = 10, CancellationToken ct = default)
    {
       
        var groupConversion = new List<AgentMessage>
        {
            new(AuthorRole.User, userInput, "User")
        };
        return CallAsync(groupConversion, maxRound, ct);
    }
    public async Task<List<AgentMessage>> CallAsync(List<AgentMessage>? conversation = null,
        int maxRound = 10, CancellationToken ct = default)
    {
        var agents = InteractiveAgents.Concat([Admin]).Reverse().ToList();
        var groupConversion = new List<AgentMessage>();
        if (conversation != null)
        {
            groupConversion.AddRange(conversation);
        }


        var round = 0;
        var adminResponse = await Admin.RunAgentAsync(groupConversion, cancellationToken: ct);
        groupConversion.Add(adminResponse);
        IInteractiveAgent lastSpeaker = Admin;
        while (round < maxRound)
        {
            if (ct.IsCancellationRequested) break;
            var nextSpeaker = await SelectNextSpeaker(lastSpeaker, groupConversion, agents, ct);
            var agentResponse = await nextSpeaker.RunAgentAsync(groupConversion, cancellationToken: ct);
            lastSpeaker = nextSpeaker;
            groupConversion.Add(agentResponse);
            round++;
        }
        return groupConversion;
    }

    private async Task<IInteractiveAgent> SelectNextSpeaker(IInteractiveAgent lastSpeaker, IEnumerable<AgentMessage> groupConversion, IEnumerable<InteractiveAgentBase> agents,
        CancellationToken ct)
    {
        IInteractiveAgent nextSpeaker;
        if (_transitionGraph != null)
        {
            var availableNextAgents = await _transitionGraph.TransitToNextAvailableAgentsAsync(lastSpeaker, groupConversion);
            if (availableNextAgents.Count() == 1)
            {
                nextSpeaker = availableNextAgents.First();
            }
            else
            {
                nextSpeaker = await AutoSelectNextAgent(groupConversion, availableNextAgents, ct);
            }
        }
        else
        {
            nextSpeaker = await AutoSelectNextAgent(groupConversion, agents, ct);
        }

        return nextSpeaker;
    }

    private async Task<InteractiveAgentBase> AutoSelectNextAgent(IEnumerable<AgentMessage> groupConversion, IEnumerable<IInteractiveAgent> agents, CancellationToken ct)
    {
        var settings = new OpenAIPromptExecutionSettings
        {
            
            Temperature = 0.0,
            MaxTokens = 128,
            StopSequences = [":"],
        };
        var kernelArgs = UpdateKernelArguments(groupConversion, agents, settings);
        var promptFactory = new KernelPromptTemplateFactory();
        var templateConfig = new PromptTemplateConfig(NextAgentPromptTemplate);
        var prompt = await promptFactory.Create(templateConfig).RenderAsync(Admin.Kernel, kernelArgs, ct);
        var chat = Admin.Kernel.GetRequiredService<IChatCompletionService>();
        var chatHistory = new ChatHistory(prompt);
        //var functionResponse = await Admin.Kernel.InvokeAsync(function, kernelArgs, ct);
        try
        {
            var nextAgentName = await chat.GetChatMessageContentAsync(chatHistory, settings, cancellationToken:ct)/* functionResponse.GetValue<string>()*/;
            var name = nextAgentName!.ToString()[5..];
            Console.WriteLine("AutoSelectNextAgent: " + name);
            var nextAgent = InteractiveAgents.FirstOrDefault(interactive => interactive.Agent.Name.Equals(name, StringComparison.InvariantCultureIgnoreCase)) ?? Admin;
            return nextAgent;
        }
        catch (TaskCanceledException exception)
        {
            Console.WriteLine(exception.Message);
            return Admin;
        }
        catch (Exception e)
        {
            throw;
        }
    }

    private static KernelArguments UpdateKernelArguments(IEnumerable<AgentMessage> groupConversion, IEnumerable<IInteractiveAgent> interactiveAgents, OpenAIPromptExecutionSettings settings)
    {
        var groupConvoHistory = string.Join("\n ", groupConversion?.Select(message => $"From: \n{message?.AgentName}\n### Message\n {message?.Content}\n") ?? Array.Empty<string>());
        var kernelArgs = new KernelArguments(settings)
        {
            ["speakerList"] = string.Join("\n ", interactiveAgents.Select(interactiveagent => $"### Name\n{interactiveagent?.Name}\n### Description\n {interactiveagent?.Description}\n")),
            ["conversationHistory"] = groupConvoHistory
        };
        return kernelArgs;
    }
}
public record Message(string Text, string Sender, DateTime TimeStamp);