using Microsoft.SemanticKernel;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SkPluginLibrary.Agents.Models
{
    public class AgentProxy
    {
        public string Id { get; } = Guid.NewGuid().ToString();
        public string? Name { get; set; }
        public string Description { get; set; } = "";
        public string Instructions { get; set; } = "";
        public string SystemPrompt => $"""
            You are {Name}.
            You are assisting the user as {Description}.
            [Instructions]
            {Instructions}
            [end Instructions]
            """;
        public List<KernelPlugin> Plugins { get; set; } = [];
        public int Order { get; set; }
        //public List<AgentProxy>? SubAgents { get; set; }
        //public AgentInteractionType AgentInteractionType { get; set; }
        public bool IsPrimary { get; set; }
    }
    public class AgentExecutionRequest
    {
        public AgentInteractionType AgentInteractionType { get; set; }
        public List<AgentProxy> Agents { get; set; } = [];
        public string Description { get; set; } = "";
        public string Stop { get; set; } = "";
        internal (bool valid, string message) Validate()
        {
            var hasProperAgents = AgentInteractionType switch
            {
                AgentInteractionType.SingleAgent when Agents.Count != 1 => false,
                AgentInteractionType.AgentWithSubAgentsAsPlugins when Agents.Count < 2 => false,
                //AgentInteractionType.AgentWithSubAgentsOnThread when Agents.Count < 2 => false,
                _ => true
            };
            if (!hasProperAgents)
            {
                return (false, $"Incorrect number of agents for interaction type {AgentInteractionType}");
            }

            return AgentInteractionType switch
            {
                //AgentInteractionType.AgentWithSubAgentsOnThread when Agents.All(x => !x.IsPrimary) => (false,
                //    $"{AgentInteractionType} requires a primary agent"),
                //AgentInteractionType.AgentWithSubAgentsOnThread when string.IsNullOrWhiteSpace(Stop) ||
                //                                                     !Agents.Find(x => x.IsPrimary)!.Instructions
                //                                                         .Contains(Stop) => (false,
                //    $"{AgentInteractionType} requires a STOP string that matches instructions"),
                AgentInteractionType.ChatWithAgentsAsOpenAiToolFunctions when Agents.All(x => !x.IsPrimary) => (false,
                                   $"{AgentInteractionType} requires a primary agent"),
                AgentInteractionType.AgentWithSubAgentsAsPlugins when Agents.All(x => !x.IsPrimary) => (false,
                    $"{AgentInteractionType} requires a primary agent"),
                _ => (true, "Valid")
            };
        }
    }
    public enum AgentInteractionType
    {
        [Description("None")]
        None,
        [Description("Single Agent with standard plugins")]
        SingleAgent,
        [Description("Primary Agent with sub-agents added as plugins")]
        AgentWithSubAgentsAsPlugins,
        //[Description("Primary Agent with sub-agents added to primary agent thread")]
        //AgentWithSubAgentsOnThread,
        [Description("Chat interaction with all Agents called as an OpenAI functions")]
        ChatWithAgentsAsOpenAiToolFunctions
    }
}
