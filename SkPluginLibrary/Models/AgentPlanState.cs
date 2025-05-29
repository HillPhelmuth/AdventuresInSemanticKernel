using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SkPluginLibrary.Plugins.NativePlugins;

namespace SkPluginLibrary.Models;

public class AgentPlanState
{
    public AgentPlan ActivePlan { get; set; } = new();
}