using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace SemanticKernelAgentOrchestration.Models
{
    public class UserProxyAgent(AgentProxy agent, Kernel kernel) : ChatAgent(agent, kernel)
    {
	    public override async Task<ChatMessageContent?> RunAgentAsync(ChatHistory chatHistory,
	        PromptExecutionSettings? settings = null, CancellationToken cancellationToken = default)
        {
            var content = await GetHumanInputAsync();
            var agentChatMessageContent = new AgentMessage(AuthorRole.User, content, Name);
            return agentChatMessageContent;
        }
    }
}
