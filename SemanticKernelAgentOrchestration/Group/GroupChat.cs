using ChatComponents;
using Microsoft.Extensions.AI;
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
        var functionHook = new AgentFunctionMiddleware();
        functionHook.FunctionInvoking += (sender, context) =>
        {
            Console.WriteLine($"Function Invoking: {context.Function.Name}");
        };
        functionHook.FunctionInvoked += (sender, context) =>
        {
            Console.WriteLine($"Function Invoked: {context.Function.Name}");
        };
        Admin.FunctionMiddleware.Add(functionHook);
        foreach (var agent in InteractiveAgents)
        {
            agent.FunctionMiddleware.Add(functionHook);
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
        - {speakerList}

        ### Conversation history

        - {conversationHistory}

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
            new ChatMessage(ChatRole.User, userInput) { AuthorName = "User" }
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
        if (adminResponse is not null) groupConversion.Add(adminResponse);
        ChatAgent lastSpeaker = Admin;
        while (round < maxRound)
        {
            if (ct.IsCancellationRequested) break;
            var nextSpeaker = _isRoundRobin ? NextSpeakerInOrder(lastSpeaker) : await SelectNextSpeaker(lastSpeaker, groupConversion, agents, ct);
            
            var agentResponse = await nextSpeaker.RunAgentAsync(groupConversion, cancellationToken: ct);
            lastSpeaker = nextSpeaker;
            if (agentResponse is null) break;
            groupConversion.Add(agentResponse);
            if (agentResponse?.Text?.Contains(_endStatement) == true) break;
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
            nextSpeaker = await AutoSelectNextAgent(groupConversion, agents.Where(x => x.Name != lastSpeaker.Name), ct);
        }

        return nextSpeaker;
    }

    private async Task<ChatAgent> AutoSelectNextAgent(ChatHistory groupConversion, IEnumerable<IChatAgent> agents, CancellationToken ct)
    {
        var options = new ChatOptions
        {
            Temperature = 0.0f,
            MaxOutputTokens = 128,
            StopSequences = [":"],
        };
        var prompt = BuildNextAgentPrompt(groupConversion, agents);
        try
        {
            var response = await Admin.ChatClient.GetResponseAsync(prompt, options, ct);
            var name = response.Text[5..];
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

    private static string BuildNextAgentPrompt(ChatHistory groupConversion, IEnumerable<IChatAgent> interactiveAgents)
    {
        var groupConvoHistory = string.Join("\n ", groupConversion?.Select(message => $"From: \n{message?.AuthorName}\n### Message\n {message?.Text}\n") ?? []);
        var speakerList = string.Join("\n ", interactiveAgents.Select(interactiveagent => $"### Name\n{interactiveagent?.Name}\n### Description\n {interactiveagent?.Description}\n"));
        return NextAgentPromptTemplate.Replace("{speakerList}", speakerList).Replace("{conversationHistory}", groupConvoHistory);
    }
}
