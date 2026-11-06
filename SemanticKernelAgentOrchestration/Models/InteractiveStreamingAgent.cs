using ChatComponents;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using SemanticKernelAgentOrchestration.Models.Events;

namespace SemanticKernelAgentOrchestration.Models
{
    public class InteractiveStreamingAgent : ChatAgent
    {
        public InteractiveStreamingAgent(AgentProxy agent, IChatClient chatClient, bool engageuser = false) : base(agent, chatClient)
        {
            Plugins = agent.Plugins;
            if (engageuser)
                AddInteractivePlugin();
        }
        public string Id { get; } = Guid.NewGuid().ToString();
        public List<ToolPlugin> Plugins { get; set; }
        public void AddInteractivePlugin()
        {
            ExtraTools.Add(AIFunctionFactory.Create(GetHumanInputAsync, "AskUser", "Ask user for information, or request clarification from user."));
        }
        public override async Task<ChatMessage?> RunAgentAsync(ChatHistory chatHistory,
            ChatOptions? settings = null, CancellationToken cancellationToken = default)
        {
            var aiAgent = BuildAgent(settings);
            var messages = PrepareMessages(chatHistory);
            var text = new System.Text.StringBuilder();
            AgentResponseUpdate? lastUpdate = null;
            var hasStarted = false;
            try
            {
                await foreach (var update in aiAgent.RunStreamingAsync(messages, cancellationToken: cancellationToken))
                {
                    text.Append(update.Text);
                    update.AuthorName = Name;
                    lastUpdate = update;
                    OnAgentResponse(new AgentResponseArgs(update) { IsStartToken = !hasStarted });
                    hasStarted = true;
                }
                if (lastUpdate is not null)
                {
                    var finalUpdate = new AgentResponseUpdate(ChatRole.Assistant, "") { AuthorName = Name };
                    OnAgentResponse(new AgentResponseArgs(finalUpdate) { IsCompleted = true });
                }
            }
            catch (OperationCanceledException canceledException)
            {
                Console.WriteLine(canceledException);
                return null;
            }
            return new AgentMessage(ChatRole.Assistant, text.ToString(), Name);
        }
	}
}
