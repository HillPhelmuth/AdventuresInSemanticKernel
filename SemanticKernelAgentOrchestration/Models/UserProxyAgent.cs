using ChatComponents;
using Microsoft.Extensions.AI;

namespace SemanticKernelAgentOrchestration.Models
{
    public class UserProxyAgent(AgentProxy agent, IChatClient chatClient) : ChatAgent(agent, chatClient)
    {
        public override async Task<ChatMessage?> RunAgentAsync(ChatHistory chatHistory,
            ChatOptions? settings = null, CancellationToken cancellationToken = default)
        {
            var content = await GetHumanInputAsync();
            var agentChatMessageContent = new AgentMessage(ChatRole.User, content, Name);
            return agentChatMessageContent;
        }
    }
}
