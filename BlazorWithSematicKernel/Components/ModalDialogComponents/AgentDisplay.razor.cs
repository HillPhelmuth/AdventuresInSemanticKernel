using Microsoft.AspNetCore.Components;
using SkPluginLibrary.Agents.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BlazorWithSematicKernel.Components.ModalDialogComponents
{
    public partial class AgentDisplay : ComponentBase
    {
        [Parameter]
        public AgentProxy? Agent { get; set; }
    }
}
