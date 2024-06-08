using System.Text.Json;
using System.Text.Json.Serialization;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.Experimental.Agents;
using SkPluginLibrary.Agents.Extensions;
using SkPluginLibrary.Agents.Models;
using SkPluginLibrary.Models.Hooks;

namespace SkPluginLibrary.Agents.Group;

public class GroupChat : IGroupChat
{

    public GroupChat(InteractiveAgentBase admin, List<InteractiveAgentBase> agents, TransitionGraph? transitionGraph = null, string endStatement = "[STOP]", bool isRoundRobin = false)
    {
        _transitionGraph = transitionGraph;
        InteractiveAgents = agents;
        Admin = admin;
        _endStatement = endStatement;
        var interactives = InteractiveAgents.ToList();
        interactives.Insert(0, Admin);
        _allAgents = interactives;
        _isRoundRobin = isRoundRobin;
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
        Admin.Kernel.FunctionInvocationFilters.Add(functionHook);
        Admin.Kernel.PromptRenderFilters.Add(promptHook);
        foreach (var agent in InteractiveAgents)
        {
            agent.Kernel.FunctionInvocationFilters.Add(functionHook);
            agent.Kernel.PromptRenderFilters.Add(promptHook);
        }
     
    }
    private string _endStatement;
    private bool _isRoundRobin;
    private TransitionGraph? _transitionGraph;
    internal ChatHistory ChatHistory => AgentChatHistory/*.AsChatHistory()*/;
    public ChatHistory AgentChatHistory { get; set; } = [];
    public List<InteractiveAgentBase> InteractiveAgents { get; }
    private List<InteractiveAgentBase> _allAgents = [];
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
        Console.WriteLine($"Agent {message.AuthorName} Init message");
        AgentChatHistory.Add(message);
    }
    public Task<ChatHistory> CallAsync(string userInput, int maxRound = 10, CancellationToken ct = default)
    {
       
        var groupConversion = new ChatHistory
        {
            new(AuthorRole.User, userInput, "User")
        };
        return CallAsync(groupConversion, maxRound, ct);
    }
    public void AddAgent(InteractiveAgentBase agent)
    {
		InteractiveAgents.Add(agent);
		_allAgents.Add(agent);
	}
    public async Task<ChatHistory> CallAsync(ChatHistory conversation = null,
	    int maxRound = 10, CancellationToken ct = default)
    {
        var agents = _allAgents;
        var groupConversion = new ChatHistory();
        if (conversation != null)
        {
            groupConversion.AddRange(conversation);
        }


        var round = 0;
        var adminResponse = await Admin.RunAgentAsync(groupConversion, cancellationToken: ct);
        groupConversion.Add(adminResponse);
        InteractiveAgentBase lastSpeaker = Admin;
        while (round < maxRound)
        {
            if (ct.IsCancellationRequested) break;
            var nextSpeaker = _isRoundRobin ? SelectNextSpeaker(lastSpeaker) : await SelectNextSpeaker(lastSpeaker, groupConversion, agents, ct);
            
            var agentResponse = await nextSpeaker.RunAgentAsync(groupConversion, cancellationToken: ct);
            lastSpeaker = nextSpeaker;
            if (agentResponse is null) break;
            groupConversion.Add(agentResponse);
            if (agentResponse?.Content?.Contains(_endStatement) == true) break;
            round++;
        }
        return groupConversion;
    }
    private InteractiveAgentBase SelectNextSpeaker(InteractiveAgentBase currentSpeaker)
    {
        var index = _allAgents.IndexOf(currentSpeaker);
        if (index == -1)
        {
            throw new ArgumentException("The agent is not in the group chat", nameof(currentSpeaker));
        }

        var nextIndex = (index + 1) % _allAgents.Count;
        return _allAgents[nextIndex];
    }
    private async Task<InteractiveAgentBase> SelectNextSpeaker(InteractiveAgentBase lastSpeaker,
	    ChatHistory groupConversion, IEnumerable<InteractiveAgentBase> agents,
	    CancellationToken ct)
    {
        InteractiveAgentBase nextSpeaker;
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

    private async Task<InteractiveAgentBase> AutoSelectNextAgent(ChatHistory groupConversion, IEnumerable<IInteractiveAgent> agents, CancellationToken ct)
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

    private static KernelArguments UpdateKernelArguments(ChatHistory groupConversion, IEnumerable<IInteractiveAgent> interactiveAgents, OpenAIPromptExecutionSettings settings)
    {
        var groupConvoHistory = string.Join("\n ", groupConversion?.Select(message => $"From: \n{message?.AuthorName}\n### Message\n {message?.Content}\n") ?? Array.Empty<string>());
        var kernelArgs = new KernelArguments(settings)
        {
            ["speakerList"] = string.Join("\n ", interactiveAgents.Select(interactiveagent => $"### Name\n{interactiveagent?.Name}\n### Description\n {interactiveagent?.Description}\n")),
            ["conversationHistory"] = groupConvoHistory
        };
        return kernelArgs;
    }
}
public record Message(string Text, string Sender, DateTime TimeStamp);