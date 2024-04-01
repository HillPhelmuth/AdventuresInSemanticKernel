using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace SkPluginLibrary.Agents.Models
{
    public class UserProxyAgent : InteractiveAgentBase
    {
        public UserProxyAgent(AgentProxy agent, Kernel kernel) : base(agent, kernel)
        {
        }
        public override async Task<AgentMessage?> RunAgentAsync(List<AgentMessage> chatHistory, PromptExecutionSettings? settings = null, CancellationToken cancellationToken = default)
        {
            var content = await GetHumanInputAsync();
            var agentChatMessageContent = new AgentMessage(AuthorRole.User, content, Name);
            return agentChatMessageContent;
        }
    }
}
