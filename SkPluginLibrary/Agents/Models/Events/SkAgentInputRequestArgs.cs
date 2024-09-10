using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SkPluginLibrary.Agents.SkAgents;

namespace SkPluginLibrary.Agents.Models.Events
{
    public class SkAgentInputRequestArgs(UserProxySkAgent agent) : EventArgs
    {
        public UserProxySkAgent Agent { get; set; } = agent;
    }
}
