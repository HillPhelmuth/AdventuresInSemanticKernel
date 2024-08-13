using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using SemanticKernelAgentOrchestration.Models;

namespace SemanticKernelAgentOrchestration.Group;

public class GroupChat : IGroupChat
{

    public GroupChat(ChatAgent admin, List<ChatAgent> agents, TransitionGraph? transitionGraph = null, string endStatement = "[STOP]", bool isRoundRobin = false)
    {
        _transitionGraph = transitionGraph;
        InteractiveAgents = agents;
        Admin = admin;
        _endStatement = endStatement;
        var interactives = InteractiveAgents.ToList();
        interactives.Insert(0, Admin);
        _allAgents = interactives;
        _isRoundRobin = isRoundRobin;
        var functionHook = new AgentFunctionFilters();
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
    internal ChatHistory ChatHistory => AgentChatHistory;
    public ChatHistory AgentChatHistory { get; set; } = [];
    public List<ChatAgent> InteractiveAgents { get; }
    private List<ChatAgent> _allAgents = [];
    public ChatAgent Admin { get; }
    
   
    private const string NextAgentPromptTemplate = """
        You are in a role play game. Carefully read the conversation history and carry on the conversation, always starting with 'From {name}:'.
        The available roles are:
        - {{$speakerList}}

        ### Conversation history

        - {{$conversationHistory}}

        Each message MUST start with 'From {name}:', e.g:
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
    public void AddAgent(ChatAgent agent)
    {
		InteractiveAgents.Add(agent);
		_allAgents.Add(agent);
	}
    public async Task<ChatHistory> CallAsync(ChatHistory? conversation = null,
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
        ChatAgent lastSpeaker = Admin;
        while (round < maxRound)
        {
            if (ct.IsCancellationRequested) break;
            var nextSpeaker = _isRoundRobin ? NextSpeakerInOrder(lastSpeaker) : await SelectNextSpeaker(lastSpeaker, groupConversion, agents, ct);
            
            var agentResponse = await nextSpeaker.RunAgentAsync(groupConversion, cancellationToken: ct);
            lastSpeaker = nextSpeaker;
            if (agentResponse is null) break;
            groupConversion.Add(agentResponse);
            if (agentResponse?.Content?.Contains(_endStatement) == true) break;
            round++;
        }
        return groupConversion;
    }
    private ChatAgent NextSpeakerInOrder(ChatAgent currentSpeaker)
    {
        var index = _allAgents.IndexOf(currentSpeaker);
        if (index == -1)
        {
            throw new ArgumentException("The agent is not in the group chat", nameof(currentSpeaker));
        }

        var nextIndex = (index + 1) % _allAgents.Count;
        return _allAgents[nextIndex];
    }
    private async Task<ChatAgent> SelectNextSpeaker(ChatAgent lastSpeaker,
	    ChatHistory groupConversion, IEnumerable<ChatAgent> agents,
	    CancellationToken ct)
    {
        ChatAgent nextSpeaker;
        if (_transitionGraph != null)
        {
            var availableNextAgents = await _transitionGraph.TransitToNextAvailableAgentsAsync(lastSpeaker, groupConversion);
            var nextAgents = availableNextAgents.ToList();
            if (nextAgents.Count == 1)
            {
                nextSpeaker = nextAgents.First();
            }
            else
            {
                nextSpeaker = await AutoSelectNextAgent(groupConversion, nextAgents, ct);
            }
        }
        else
        {
            nextSpeaker = await AutoSelectNextAgent(groupConversion, agents, ct);
        }

        return nextSpeaker;
    }

    private async Task<ChatAgent> AutoSelectNextAgent(ChatHistory groupConversion, IEnumerable<IChatAgent> agents, CancellationToken ct)
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
            var nextAgentName = await chat.GetChatMessageContentAsync(chatHistory, settings, cancellationToken:ct);
            var name = nextAgentName!.ToString()[5..];
            Console.WriteLine("AutoSelectNextAgent: " + name);
            var nextAgent = InteractiveAgents.FirstOrDefault(interactive => interactive.Name.Equals(name, StringComparison.InvariantCultureIgnoreCase)) ?? Admin;
            return nextAgent;
        }
        catch (Exception exception)
        {
	        if (exception is not TaskCanceledException taskCanceledException) throw;
	        Console.WriteLine(taskCanceledException);
			return Admin;
        }
    }

    private static KernelArguments UpdateKernelArguments(ChatHistory groupConversion, IEnumerable<IChatAgent> interactiveAgents, OpenAIPromptExecutionSettings settings)
    {
        var groupConvoHistory = string.Join("\n ", groupConversion?.Select(message => $"From: \n{message?.AuthorName}\n### Message\n {message?.Content}\n") ?? []);
        var kernelArgs = new KernelArguments(settings)
        {
            ["speakerList"] = string.Join("\n ", interactiveAgents.Select(interactiveagent => $"### Name\n{interactiveagent?.Name}\n### Description\n {interactiveagent?.Description}\n")),
            ["conversationHistory"] = groupConvoHistory
        };
        return kernelArgs;
    }
}
