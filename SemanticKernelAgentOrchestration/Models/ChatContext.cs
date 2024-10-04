using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;

namespace SemanticKernelAgentOrchestration.Models
{
    public class ChatContext
    {
        public List<ChatMessageContent> ChatMessages { get; set; } = [];
        public ChatAgent? ActiveAgent { get; set; }
        public bool IsTranstionNext { get; set; }
        public bool IsIntentTranstion { get; set; }
    }
}
