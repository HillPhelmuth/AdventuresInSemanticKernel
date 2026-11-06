using Microsoft.Extensions.AI;

namespace SemanticKernelAgentOrchestration.Models
{
    public class ChatContext
    {
        public List<ChatMessage> ChatMessages { get; set; } = [];
        public ChatAgent? ActiveAgent { get; set; }
        public bool IsTranstionNext { get; set; }
        public bool IsIntentTranstion { get; set; }
    }
}
